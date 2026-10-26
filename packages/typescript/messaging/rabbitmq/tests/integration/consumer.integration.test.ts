// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  RabbitMQContainer,
  type StartedRabbitMQContainer,
} from "@testcontainers/rabbitmq";
import { Connection } from "rabbitmq-client";
import type { ILogger } from "@dcsv-io/d2-logging";
import {
  RecordingLogger,
  waitForSettled,
  waitForValue,
  waitUntil,
} from "../helpers.js";
import { fail, ok } from "@dcsv-io/d2-result";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import {
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@dcsv-io/d2-messaging-abstractions";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

import {
  createConnection,
  RabbitMqConnectionOptions,
  type D2Connection,
  type ConsumeContext,
  DlqNaming,
  InMemoryMessageIdempotencyStore,
  QueuePattern,
  type SubscriptionDescriptor,
  type Subscription,
  subscribe,
} from "../../src/index.js";

// -----------------------------------------------------------------------
// Loading of the goldens that .NET emits. A .NET test emitter writes them into
// the contract-tests/fixtures/mq-consumer-golden/ directory that loadGolden
// reads.
// -----------------------------------------------------------------------

interface GoldenData {
  readonly bodyBase64: string;
  readonly headers: Readonly<Record<string, string>>;
  readonly exchange?: string;
  readonly routingKey?: string;
  readonly expectedDecoded?: Readonly<Record<string, unknown>>;
  readonly expectedRequestId?: string;
  readonly expectedRequestPath?: string;
  readonly producerTraceId?: string;
}

function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    if (existsSync(join(dir, "pnpm-workspace.yaml"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) throw new Error("repo root not found");
    dir = parent;
  }
}

function loadGolden(scenario: string): GoldenData {
  const root = findRepoRoot(dirname(fileURLToPath(import.meta.url)));
  // The monorepo keeps the package tree under public/; the public clone keeps
  // it at the root.
  const monorepoFixtures = join(
    root,
    "public",
    "packages",
    "typescript",
    "contract-tests",
    "fixtures",
  );
  const fixtures = existsSync(monorepoFixtures)
    ? monorepoFixtures
    : join(root, "packages", "typescript", "contract-tests", "fixtures");
  const path = join(fixtures, "mq-consumer-golden", `${scenario}.json`);
  const raw = JSON.parse(readFileSync(path, "utf8")) as { data: GoldenData };
  return raw.data;
}

function deferred<T>(): { promise: Promise<T>; resolve: (v: T) => void } {
  let resolve!: (v: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

async function delay(ms: number): Promise<void> {
  await new Promise((r) => setTimeout(r, ms));
}

let uniqueCounter = 0;
function unique(prefix: string): string {
  uniqueCounter += 1;
  return `${prefix}.${Date.now().toString(36)}.${uniqueCounter}`;
}

describe("@dcsv-io/d2-messaging-rabbitmq consumer - Testcontainer wire-contract proof", () => {
  let container: StartedRabbitMQContainer;
  let connection: Connection;
  let managedConnection: D2Connection;

  beforeAll(async () => {
    container = await new RabbitMQContainer(
      "rabbitmq:4.1.7-management",
    ).start();
    // getAmqpUrl() omits credentials; the container's default user is guest/guest.
    const url = container
      .getAmqpUrl()
      .replace("amqp://", "amqp://guest:guest@");
    connection = new Connection(url);
    managedConnection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: url,
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    await connection.onConnect();
  }, 180_000);

  afterAll(async () => {
    try {
      await managedConnection?.close();
    } finally {
      try {
        await connection?.close();
      } finally {
        await container?.stop();
      }
    }
  });

  function makeDescriptor(
    overrides: Partial<SubscriptionDescriptor> = {},
  ): SubscriptionDescriptor {
    return {
      queueName: unique("q.test"),
      exchange: unique("d2.test"),
      exchangeType: "fanout",
      pattern: QueuePattern.DurableShared,
      routingKeyBinding: "",
      prefetch: 4,
      idempotency: false,
      nackedBy: "test-consumer",
      ...overrides,
    };
  }

  async function publishTo(
    exchange: string,
    golden: GoldenData,
    overrideMessageId?: string,
  ): Promise<void> {
    const publisher = connection.createPublisher({ confirm: true });
    try {
      const body = Buffer.from(golden.bodyBase64, "base64");
      const headers: Record<string, string> = {};
      for (const [k, v] of Object.entries(golden.headers)) {
        if (k === AmqpHeaders.CONTENT_TYPE || k === AmqpHeaders.MESSAGE_ID)
          continue;
        headers[k] = v;
      }
      await publisher.send(
        {
          exchange,
          routingKey: "",
          durable: true,
          contentType:
            golden.headers[AmqpHeaders.CONTENT_TYPE] ??
            "application/octet-stream",
          messageId:
            overrideMessageId ?? golden.headers[AmqpHeaders.MESSAGE_ID],
          headers,
        },
        body,
      );
    } finally {
      await publisher.close();
    }
  }

  async function readDlq(dlqName: string): Promise<Record<string, unknown>> {
    const msg = await waitForValue(() =>
      connection.basicGet({ queue: dlqName, noAck: true }),
    );
    const reason = msg.headers?.[AmqpHeaders.FAILURE_REASON];
    const reasonStr =
      typeof reason === "string"
        ? reason
        : Buffer.from(reason as Uint8Array).toString("utf8");
    return {
      metadata: JSON.parse(reasonStr) as Record<string, unknown>,
      body: msg.body as Buffer,
      headers: msg.headers ?? {},
    };
  }

  it("replays a real .NET plaintext golden: decodes the body + establishes context", async () => {
    const golden = loadGolden("plaintext-message");
    const descriptor = makeDescriptor();
    const received = deferred<{ message: unknown; ctx: ConsumeContext }>();

    const sub = subscribe({
      connection: managedConnection,
      descriptor,
      logger: silentLogger(),
      handler: (message, ctx) => {
        received.resolve({ message, ctx });
        return ok();
      },
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const { message, ctx } = await waitForSettled(received.promise);

    // The camelCase .NET body decodes to the expected object.
    expect(message).toEqual(golden.expectedDecoded);
    // The base64url x-d2-context establishes the per-message context.
    expect(ctx.propagated.requestId).toBe(golden.expectedRequestId);
    expect(ctx.propagated.requestPath).toBe(golden.expectedRequestPath);

    await sub.close();
  });

  it("routes a real encrypted-domain frame to the DLQ with DECRYPT_FAILURE (fail-loud)", async () => {
    const golden = loadGolden("encrypted-frame");
    const descriptor = makeDescriptor();

    const sub = subscribe({
      connection: managedConnection,
      descriptor,
      logger: silentLogger(),
      handler: () => ok(),
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const dlq = await readDlq(DlqNaming.dlqFor(sub.queueName));
    const meta = dlq.metadata as Record<string, unknown>;
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DECRYPT_FAILURE,
    );
    // Original ciphertext body is byte-identical in the DLQ.
    expect(
      (dlq.body as Buffer).equals(Buffer.from(golden.bodyBase64, "base64")),
    ).toBe(true);

    await sub.close();
  });

  it("dead-letters a handler failure with metadata + carries producer headers forward", async () => {
    const golden = loadGolden("plaintext-message");
    const descriptor = makeDescriptor();

    const sub = subscribe({
      connection: managedConnection,
      descriptor,
      logger: silentLogger(),
      handler: () => fail({ errorCode: "DOM_SAMPLE_ORDER_UNAVAILABLE" }),
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const dlq = await readDlq(DlqNaming.dlqFor(sub.queueName));
    const meta = dlq.metadata as Record<string, unknown>;
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_RESULT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "DOM_SAMPLE_ORDER_UNAVAILABLE",
    );
    expect(meta[DlqFailureMetadataFields.TRACE_ID]).toBe(
      golden.producerTraceId,
    );

    // The DLQ copy keeps the producer headers.
    const dlqHeaders = dlq.headers as Record<string, unknown>;
    expect(headerString(dlqHeaders, AmqpHeaders.TRACEPARENT)).toBe(
      golden.headers[AmqpHeaders.TRACEPARENT],
    );
    expect(headerString(dlqHeaders, AmqpHeaders.PROPAGATED_CONTEXT)).toBe(
      golden.headers[AmqpHeaders.PROPAGATED_CONTEXT],
    );

    await sub.close();
  });

  it("deduplicates a repeated message-id (ack-and-skip, handler invoked once)", async () => {
    const golden = loadGolden("plaintext-message");
    const descriptor = makeDescriptor({ idempotency: true });
    let calls = 0;

    const sub = subscribe({
      connection: managedConnection,
      descriptor,
      logger: silentLogger(),
      store: new InMemoryMessageIdempotencyStore(),
      handler: () => {
        calls += 1;
        return ok();
      },
    });
    await sub.ready;

    const id = "dedup-0192f8c1-1111-7000-8000-0000000000cc";
    await publishTo(descriptor.exchange, golden, id);
    await waitUntil(() => calls === 1);
    await publishTo(descriptor.exchange, golden, id);
    await delay(1500); // give the duplicate time to be ack-skipped

    expect(calls).toBe(1);
    await sub.close();
  });

  it("splits deliveries across competing consumers (each message processed once)", async () => {
    const descriptor = makeDescriptor();
    const golden = loadGolden("plaintext-message");
    let total = 0;

    const subs: Subscription[] = [];
    for (let i = 0; i < 2; i++) {
      subs.push(
        subscribe({
          connection: managedConnection,
          descriptor, // SAME queueName -> competing consumers on one queue
          logger: silentLogger(),
          handler: () => {
            total += 1;
            return ok();
          },
        }),
      );
    }
    await Promise.all(subs.map((s) => s.ready));

    const count = 6;
    for (let i = 0; i < count; i++)
      await publishTo(descriptor.exchange, golden, `${unique("m")}`);

    await waitUntil(() => total === count);
    expect(total).toBe(count);

    await Promise.all(subs.map((s) => s.close()));
  });
});

function headerString(
  headers: Record<string, unknown>,
  name: string,
): string | undefined {
  const raw = headers[name];
  if (typeof raw === "string") return raw;
  if (raw instanceof Uint8Array) return Buffer.from(raw).toString("utf8");
  return undefined;
}

function silentLogger(): ILogger {
  const noop = (): void => undefined;
  const logger: ILogger = {
    trace: noop,
    debug: noop,
    info: noop,
    warn: noop,
    error: noop,
    fatal: noop,
    child: () => logger,
  };
  return logger;
}
