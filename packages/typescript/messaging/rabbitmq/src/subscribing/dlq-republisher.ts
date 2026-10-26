// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { setImmediate as immediate } from "node:timers/promises";
import type { Connection, Channel } from "rabbitmq-client";
import { sanitizedErrorRender, type ILogger } from "@dcsv-io/d2-logging";
import type { RabbitMqConnectionOptions } from "../connection/connection-options.js";
import {
  createCompletion,
  createVendorConnection,
} from "../connection/managed-connection.js";
import { admitConnectionUri } from "../connection/transport-admission.js";
import type { NormalizedSubscriberOptions } from "./subscriber-options.js";

export type DlqRepublishOutcome =
  | "confirmed"
  | "unroutable"
  | "transport-failure"
  | "deadline-exceeded"
  | "stopping"
  | "cleanup-pending";

export interface DlqRepublishRequest {
  readonly dlxName: string;
  readonly body: Buffer;
  readonly headers: Readonly<Record<string, unknown>>;
  readonly messageId?: string;
  readonly contentType?: string;
  readonly contentEncoding?: string;
  readonly correlationId?: string;
}

interface TimingAndTransport {
  readonly connectionFactory?: () => Connection;
  readonly now?: () => number;
  readonly waitForReturns?: (signal: AbortSignal) => Promise<void>;
}

type RepublisherState =
  | { kind: "empty" }
  | {
      kind: "opening" | "ready" | "retiring";
      generation: DlqTransportGeneration;
    }
  | {
      kind: "closed";
      generation?: DlqTransportGeneration;
      completion: {
        boundedWait: Promise<void>;
        resourceCompletion: Promise<void>;
        work?: Promise<void>;
      };
    };
type GenerationState =
  | { kind: "live" }
  | { kind: "retired"; resourceCompletion: Promise<void> }
  | { kind: "completed" };
type OperationState =
  | {
      kind: "pending";
      phase: "admission" | "acquisition" | "publication" | "return-drain";
    }
  | { kind: "terminal"; outcome: DlqRepublishOutcome };

export class DlqOperation {
  readonly completion = createCompletion<DlqRepublishOutcome>();
  readonly drained = createCompletion<void>();
  readonly controller = new AbortController();
  readonly deadline: number;
  readonly timer: ReturnType<typeof setTimeout>;
  state: OperationState = { kind: "pending", phase: "admission" };
  generation?: DlqTransportGeneration;
  returnObserved = false;

  constructor(
    readonly id: bigint,
    timeoutMs: number,
    private readonly now: () => number,
    private readonly retire: (generation: DlqTransportGeneration) => void,
  ) {
    this.deadline = now() + timeoutMs;
    this.timer = setTimeout(
      () => this.complete("deadline-exceeded"),
      timeoutMs,
    );
  }

  get terminal(): boolean {
    return this.state.kind === "terminal";
  }

  advance(phase: "acquisition" | "publication" | "return-drain"): void {
    if (!this.terminal) this.state = { kind: "pending", phase };
  }

  complete(outcome: DlqRepublishOutcome): void {
    if (this.terminal) return;
    if (
      (outcome === "confirmed" || outcome === "unroutable") &&
      this.now() >= this.deadline
    )
      outcome = "deadline-exceeded";
    this.state = { kind: "terminal", outcome };
    if (outcome !== "confirmed" && outcome !== "unroutable") {
      if (this.generation !== undefined) this.retire(this.generation);
      this.controller.abort();
    }
    this.completion.resolve(outcome);
  }

  dispose(): void {
    clearTimeout(this.timer);
  }
}

export class DlqTransportGeneration {
  readonly resources = createCompletion<void>();
  private state: GenerationState = { kind: "live" };
  get resourceSettled(): boolean {
    return this.state.kind === "completed";
  }
  get retired(): boolean {
    return this.state.kind !== "live";
  }
  connection?: Connection;
  channel?: Channel;
  acquisition?: Promise<Channel>;
  confirmSelection?: Promise<void>;
  send?: Promise<void>;
  returnDrain?: Promise<void>;
  work: Promise<void> = Promise.resolve();
  cleanup?: Promise<void>;
  private readonly onError = (): void => this.failed(this);
  private readonly onClose = (): void => this.failed(this);

  constructor(
    readonly id: bigint,
    private readonly factory: () => Connection,
    private readonly failed: (generation: DlqTransportGeneration) => void,
    private readonly logger: ILogger,
  ) {}

  start(
    operation: DlqOperation,
    request: DlqRepublishRequest,
    ready: () => void,
    waitForReturns: (signal: AbortSignal) => Promise<void>,
  ): void {
    this.work = Promise.resolve().then(async () => {
      let onReturn: (() => void) | undefined;
      try {
        if (this.retired || operation.terminal) return;
        if (this.connection === undefined) {
          this.connection = this.factory();
          this.connection.on("error", this.onError);
          if (this.retired) this.connection.unsafeDestroy();
        }
        if (this.retired || operation.terminal) return;
        if (this.channel === undefined) {
          this.acquisition = this.connection.acquire({
            emitErrorsFromChannel: true,
          });
          this.channel = await this.acquisition;
          if (this.retired || operation.terminal) return;
          this.channel.on("error", this.onError);
          this.channel.on("close", this.onClose);
          this.confirmSelection = this.channel.confirmSelect();
          await this.confirmSelection;
        }
        if (this.retired || operation.terminal) return;
        ready();
        onReturn = () => {
          operation.returnObserved = true;
          try {
            this.logger.error("DLQ publication was returned by the broker", {
              outcome: "unroutable",
            });
          } catch {
            // Diagnostic failure must not escape a transport event callback.
          }
        };
        this.channel.on("basic.return", onReturn);
        operation.advance("publication");
        this.send = this.channel.basicPublish(
          {
            exchange: request.dlxName,
            routingKey: "",
            mandatory: true,
            durable: true,
            headers: { ...request.headers },
            messageId: request.messageId,
            contentType: request.contentType,
            contentEncoding: request.contentEncoding,
            correlationId: request.correlationId,
          },
          request.body,
        );
        await this.send;
        if (this.retired || operation.terminal) return;
        operation.advance("return-drain");
        this.returnDrain = waitForReturns(operation.controller.signal);
        await this.returnDrain;
        if (this.retired || operation.terminal) return;
        operation.complete(
          operation.returnObserved ? "unroutable" : "confirmed",
        );
      } catch {
        operation.complete("transport-failure");
      } finally {
        if (onReturn !== undefined) this.channel?.off("basic.return", onReturn);
        if (!this.retired) {
          this.send = undefined;
          this.returnDrain = undefined;
        }
      }
    });
  }

  terminate(): void {
    if (this.retired) return;
    this.state = {
      kind: "retired",
      resourceCompletion: this.resources.promise,
    };
    this.connection?.unsafeDestroy();
    this.cleanup = this.cleanUp();
  }

  private async cleanUp(): Promise<void> {
    try {
      await this.work;
    } catch (error) {
      this.logCleanup(error);
    }
    try {
      await this.channel?.close();
    } catch (error) {
      this.logCleanup(error);
    }
    try {
      await this.connection?.close();
    } catch (error) {
      this.logCleanup(error);
    } finally {
      this.channel?.off("error", this.onError);
      this.channel?.off("close", this.onClose);
      this.connection?.off("error", this.onError);
      this.state = { kind: "completed" };
      this.resources.resolve();
    }
  }

  private logCleanup(error: unknown): void {
    try {
      this.logger.error("DLQ transport cleanup failed", {
        error: sanitizedErrorRender(error),
      });
    } catch {
      // Diagnostic failure must not interrupt remaining resource cleanup.
    }
  }
}

/**
 * Runs mandatory DLQ publications one at a time and tracks the cleanup of its
 * dedicated transport.
 */
export class DlqRepublisher {
  private state: RepublisherState = { kind: "empty" };
  private readonly resources = createCompletion<void>();
  readonly resourceCompletion = this.resources.promise;
  private readonly operations = new Set<DlqOperation>();
  private readonly waiters: Array<{
    operation: DlqOperation;
    enter: (accepted: boolean) => void;
  }> = [];
  private active?: DlqOperation;
  private operationId = 0n;
  private generationId = 0n;
  private readonly capacity: number;
  private readonly now: () => number;
  private readonly factory: () => Connection;
  private readonly waitForReturns: (signal: AbortSignal) => Promise<void>;
  private readonly options: Readonly<NormalizedSubscriberOptions>;
  private closeWork?: Promise<void>;

  constructor(
    connection: RabbitMqConnectionOptions,
    options: NormalizedSubscriberOptions,
    prefetch: number,
    private readonly logger: ILogger,
    seams: TimingAndTransport = {},
  ) {
    if (!Number.isInteger(prefetch) || prefetch <= 0)
      throw new RangeError("Prefetch must be positive.");
    for (const value of [options.republishMs, options.cleanupMs])
      if (!Number.isInteger(value) || value < 1 || value > 2147483647)
        throw new RangeError(
          "DLQ durations must be positive whole milliseconds.",
        );
    // The dedicated transport connects lazily, so the address is admitted here,
    // before the first republish could open it; options a connection already
    // admitted return that connection's address without a second read.
    const connectionUri = admitConnectionUri(connection);
    this.options = Object.freeze({ ...options });
    this.capacity = Math.min(prefetch, 65535);
    this.now = seams.now ?? (() => performance.now());
    this.factory =
      seams.connectionFactory ??
      (() => createVendorConnection(connectionUri, connection));
    this.waitForReturns =
      seams.waitForReturns ?? ((signal) => immediate(undefined, { signal }));
  }

  async republish(
    request: DlqRepublishRequest,
    signal?: AbortSignal,
  ): Promise<DlqRepublishOutcome> {
    if (this.state.kind === "closed") return "stopping";
    if (this.operations.size >= this.capacity) return "transport-failure";
    const operation = new DlqOperation(
      ++this.operationId,
      this.options.republishMs,
      this.now,
      (generation) => this.retire(generation),
    );
    this.operations.add(operation);
    const stop = (): void => operation.complete("stopping");
    signal?.addEventListener("abort", stop, { once: true });
    if (signal?.aborted) stop();
    let acquired = false;
    try {
      acquired = await this.enter(operation);
      // Shutdown can replace the state while admission is awaited.
      const state = this.state as RepublisherState;
      if (acquired && state.kind !== "closed" && !operation.terminal) {
        const generation = this.acquireGeneration(operation, state);
        if (generation !== undefined) {
          operation.generation = generation;
          operation.advance("acquisition");
          generation.start(
            operation,
            request,
            () => {
              if (
                this.state.kind === "opening" &&
                this.state.generation === generation
              )
                this.state = { kind: "ready", generation };
            },
            this.waitForReturns,
          );
        }
      }
      const outcome = await operation.completion.promise;
      if (operation.generation !== undefined) {
        if (operation.generation.retired)
          await waitForDlqCleanup(
            operation.generation.resources.promise,
            this.options.cleanupMs,
          );
        else await operation.generation.work;
      }
      return outcome;
    } finally {
      operation.dispose();
      signal?.removeEventListener("abort", stop);
      this.operations.delete(operation);
      if (acquired) this.leave();
      operation.drained.resolve();
    }
  }

  close(): Promise<void> {
    if (this.state.kind === "closed") return this.state.completion.boundedWait;
    const generation =
      this.state.kind === "empty" ? undefined : this.state.generation;
    const bounded = createCompletion<void>();
    const completion: {
      boundedWait: Promise<void>;
      resourceCompletion: Promise<void>;
      work?: Promise<void>;
    } = {
      boundedWait: bounded.promise,
      resourceCompletion: this.resources.promise,
    };
    this.state = { kind: "closed", generation, completion };
    for (const operation of this.operations) operation.complete("stopping");
    generation?.terminate();
    this.closeWork = (async () => {
      await Promise.all(
        [...this.operations].map((operation) => operation.drained.promise),
      );
      await generation?.resources.promise;
      this.resources.resolve();
    })();
    completion.work = waitForDlqCleanup(
      this.closeWork,
      this.options.cleanupMs,
    ).then(bounded.resolve, bounded.reject);
    return bounded.promise;
  }

  private acquireGeneration(
    operation: DlqOperation,
    state: Exclude<RepublisherState, { kind: "closed" }>,
  ): DlqTransportGeneration | undefined {
    if (state.kind === "retiring") {
      if (!state.generation.resourceSettled) {
        operation.complete("cleanup-pending");
        return;
      }
      state = { kind: "empty" };
    }
    if (state.kind === "empty")
      state = {
        kind: "opening",
        generation: new DlqTransportGeneration(
          ++this.generationId,
          this.factory,
          (generation) => {
            if (this.active?.generation === generation)
              this.active.complete("transport-failure");
            this.retire(generation);
          },
          this.logger,
        ),
      };
    this.state = state;
    return state.generation;
  }

  private retire(generation: DlqTransportGeneration): void {
    if (
      this.state.kind !== "closed" &&
      this.state.kind !== "empty" &&
      this.state.generation === generation
    )
      this.state = { kind: "retiring", generation };
    generation.terminate();
  }

  private enter(operation: DlqOperation): Promise<boolean> {
    if (operation.terminal) return Promise.resolve(false);
    if (this.active === undefined) {
      this.active = operation;
      return Promise.resolve(true);
    }
    return new Promise((resolve) => {
      const waiter = {
        operation,
        enter: (accepted: boolean): void => {
          operation.controller.signal.removeEventListener("abort", cancel);
          resolve(accepted);
        },
      };
      const cancel = (): void => {
        const index = this.waiters.indexOf(waiter);
        // Admission removes this listener synchronously when it dequeues the waiter.
        this.waiters.splice(index, 1);
        waiter.enter(false);
      };
      this.waiters.push(waiter);
      operation.controller.signal.addEventListener("abort", cancel, {
        once: true,
      });
    });
  }

  private leave(): void {
    const next = this.waiters.shift();
    this.active = next?.operation;
    next?.enter(true);
  }
}

/**
 * Bounds the wait on retained cleanup, passes on the cleanup's rejection, and
 * clears its timer when the cleanup settles.
 */
export function waitForDlqCleanup(
  resources: Promise<void>,
  cleanupMs: number,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, cleanupMs);
    resources.then(
      () => {
        clearTimeout(timer);
        resolve();
      },
      (error) => {
        clearTimeout(timer);
        reject(error);
      },
    );
  });
}
