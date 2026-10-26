// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { Connection } from "rabbitmq-client";
import { sanitizedErrorRender, type ILogger } from "@dcsv-io/d2-logging";
import type { RabbitMqConnectionOptions } from "./connection-options.js";
import { admitConnectionUri } from "./transport-admission.js";

/** A connection whose registered D2 resources are closed before its shared transport. */
export interface D2Connection {
  close(): Promise<void>;
}

/** Internal resource ownership separates bounded close from eventual resource completion. */
export interface ManagedResource {
  close(): Promise<void>;
  readonly resourceCompletion?: Promise<void>;
}

type Setup =
  | { kind: "ready"; resource: ManagedResource }
  | { kind: "failed"; error: unknown };
type RegistrationState =
  | { kind: "open" }
  | { kind: "closing"; work: Promise<void> };
type ConnectionState =
  | { kind: "open"; registrations: Map<bigint, ManagedRegistration> }
  | {
      kind: "closing" | "closed";
      registrations: Map<bigint, ManagedRegistration>;
      completion: Promise<void>;
      resourceCompletion: Promise<void>;
      work?: Promise<void>;
    };

interface ConnectionOwner {
  readonly vendor: Connection;
  readonly options: RabbitMqConnectionOptions;
  nextRegistrationId: bigint;
  state: ConnectionState;
}

const owners = new WeakMap<D2Connection, ConnectionOwner>();

/** A synchronous registration made before vendor resource allocation or callbacks. */
export class ManagedRegistration {
  readonly vendor: Connection;
  readonly options: RabbitMqConnectionOptions;
  private readonly controller = new AbortController();
  private readonly setup = createCompletion<Setup>();
  private readonly completion = createCompletion<void>();
  private readonly resources = createCompletion<void>();
  readonly resourceCompletion = this.resources.promise;
  private state: RegistrationState = { kind: "open" };
  private attached = false;
  resourceObservation?: Promise<void>;
  resourceOutcome?: PromiseSettledResult<void>;

  constructor(
    private readonly owner: ConnectionOwner,
    readonly id: bigint,
  ) {
    this.vendor = owner.vendor;
    this.options = owner.options;
  }

  get signal(): AbortSignal {
    return this.controller.signal;
  }

  stopAdmission(): void {
    this.controller.abort();
  }

  attach(resource: ManagedResource): void {
    if (this.attached)
      throw new Error("Resource registration is already attached.");
    this.attached = true;
    this.setup.resolve({ kind: "ready", resource });
    const resources = resource.resourceCompletion ?? this.completion.promise;
    this.resourceObservation = resources.then(
      () => {
        this.resourceOutcome = { status: "fulfilled", value: undefined };
        this.owner.state.registrations.delete(this.id);
        this.resources.resolve();
      },
      (reason: unknown) => {
        this.resourceOutcome = { status: "rejected", reason };
        this.owner.state.registrations.delete(this.id);
        this.resources.resolve();
      },
    );
  }

  failSetup(error: unknown): void {
    if (this.attached)
      throw new Error("Resource registration is already attached.");
    this.attached = true;
    this.setup.resolve({ kind: "failed", error });
    this.owner.state.registrations.delete(this.id);
    this.resources.resolve();
  }

  close(): Promise<void> {
    this.stopAdmission();
    if (this.state.kind === "open") {
      const work = this.closeResource();
      this.state = { kind: "closing", work };
    }
    return this.completion.promise;
  }

  private async closeResource(): Promise<void> {
    const setup = await this.setup.promise;
    try {
      if (setup.kind === "failed") throw setup.error;
      await setup.resource.close();
      this.completion.resolve();
    } catch (error) {
      this.completion.reject(error);
    }
  }
}

/**
 * Constructs the vendor connection from the frozen connection options, after
 * refusing options whose transport is not admitted.
 */
export function createManagedConnection(
  options: RabbitMqConnectionOptions,
  logger: ILogger,
): D2Connection {
  if (logger == null || typeof logger.error !== "function")
    throw new TypeError("Connection logger must provide an error method.");
  const connectionUri = admitConnectionUri(options);
  const vendor = createVendorConnection(connectionUri, options);
  vendor.on("error", (error: unknown) => {
    try {
      logger.error("RabbitMQ connection error", {
        error: sanitizedErrorRender(error),
      });
    } catch {
      // Diagnostic failure must not escape a transport event callback.
    }
  });
  const owner: ConnectionOwner = {
    vendor,
    options,
    nextRegistrationId: 0n,
    state: { kind: "open", registrations: new Map() },
  };
  const handle: D2Connection = Object.freeze({
    close: () => closeOwner(owner),
  });
  owners.set(handle, owner);
  return handle;
}

/**
 * Maps only configured values so absent settings retain the vendor defaults.
 * The address is the one {@link admitConnectionUri} returned for these options.
 */
export function createVendorConnection(
  connectionUri: string,
  options: RabbitMqConnectionOptions,
): Connection {
  return new Connection({
    url: connectionUri,
    ...(options.clientProvidedName === undefined
      ? {}
      : { connectionName: options.clientProvidedName }),
    ...(options.retryLowMs === undefined
      ? {}
      : { retryLow: options.retryLowMs }),
    ...(options.retryHighMs === undefined
      ? {}
      : { retryHigh: options.retryHighMs }),
    ...(options.heartbeatSeconds === undefined
      ? {}
      : { heartbeat: options.heartbeatSeconds }),
  });
}

/** Rejects forged or closing handles before admitting a new owned resource. */
export function registerConnectionResource(
  handle: D2Connection,
): ManagedRegistration {
  const owner = owners.get(handle);
  if (owner === undefined)
    throw new TypeError("Connection was not created by D2.");
  if (owner.state.kind !== "open") throw new Error("Connection is closing.");
  const registration = new ManagedRegistration(
    owner,
    ++owner.nextRegistrationId,
  );
  owner.state.registrations.set(registration.id, registration);
  return registration;
}

function closeOwner(owner: ConnectionOwner): Promise<void> {
  if (owner.state.kind !== "open") return owner.state.completion;
  const completion = createCompletion<void>();
  const registrations = [...owner.state.registrations.values()];
  const state: ConnectionState = {
    kind: "closing",
    completion: completion.promise,
    registrations: new Map(owner.state.registrations),
    resourceCompletion: Promise.all([
      completion.promise.then(
        () => {},
        () => {},
      ),
      ...registrations.map((registration) => registration.resourceCompletion),
    ]).then(() => {}),
  };
  owner.state = state;
  for (const registration of registrations) registration.stopAdmission();
  state.work = (async () => {
    const results = await Promise.allSettled(
      registrations.map((registration) => registration.close()),
    );
    const errors = results.flatMap((result) =>
      result.status === "rejected" ? [result.reason] : [],
    );
    try {
      await owner.vendor.close();
    } catch (error) {
      errors.push(error);
    }
    state.kind = "closed";
    if (errors.length !== 0)
      completion.reject(
        new AggregateError(errors, "Connection cleanup failed."),
      );
    else completion.resolve();
  })();
  return completion.promise;
}

/** Retains completion callbacks without requiring a newer ECMAScript promise API. */
export function createCompletion<T>(): {
  readonly promise: Promise<T>;
  readonly resolve: (value: T | PromiseLike<T>) => void;
  readonly reject: (reason: unknown) => void;
} {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}
