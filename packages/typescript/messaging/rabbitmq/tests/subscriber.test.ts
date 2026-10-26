// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { setImmediate as immediate } from "node:timers/promises";
import { type AsyncMessage, ConsumerStatus } from "rabbitmq-client";
import { fail, ok } from "@dcsv-io/d2-result";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("rabbitmq-client", async (importOriginal) => {
  const original = await importOriginal<typeof import("rabbitmq-client")>();
  const { createVendorConnectionFixture } =
    await import("./fixtures/subscriber-connection-fixture.js");
  return {
    ...original,
    Connection: function (options: { url: string }) {
      return createVendorConnectionFixture(options);
    },
  };
});

import {
  SubscriberConnectionFixture as FakeConnection,
  closeSubscriberFixtures,
  configureSubscriberHandleFactory,
  releaseClosedSubscriberFixture,
} from "./fixtures/subscriber-connection-fixture.js";
import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../src/connection/connection-options.js";
import { createCompletion } from "../src/connection/managed-connection.js";
import {
  createChangingOptionsFixture,
  type ChangingOptionsFixture,
} from "./fixtures/changing-options-fixture.js";

beforeEach(() =>
  configureSubscriberHandleFactory((url) =>
    createConnection(
      new RabbitMqConnectionOptions({ connectionUri: url }),
      new RecordingLogger(),
    ),
  ),
);
afterEach(closeSubscriberFixtures);

import { subscribe } from "../src/subscribing/subscriber.js";
import { QueuePattern } from "../src/topology/queue-pattern.js";
import { type SubscriptionDescriptor } from "../src/topology/subscription-descriptor.js";
import { RecordingLogger } from "./helpers.js";

const descriptor: SubscriptionDescriptor = {
  queueName: "orders.order-placed",
  exchange: "example.orders.placed",
  exchangeType: "fanout",
  pattern: QueuePattern.DurableShared,
  routingKeyBinding: "",
  prefetch: 8,
  idempotency: false,
};

function makeAsyncMessage(overrides: Partial<AsyncMessage> = {}): AsyncMessage {
  return {
    body: Buffer.from(JSON.stringify({ ok: true }), "utf8"),
    rawBody: Buffer.from(JSON.stringify({ ok: true }), "utf8"),
    messageId: "0192f8c1-1111-7000-8000-000000000001",
    headers: {},
    deliveryTag: 1,
    redelivered: false,
    exchange: "example.orders.placed",
    routingKey: "",
    consumerTag: "ct",
    ...overrides,
  } as AsyncMessage;
}

async function start(
  conn: FakeConnection,
  opts: Partial<Parameters<typeof subscribe>[0]> = {},
): Promise<{ sub: ReturnType<typeof subscribe>; logger: RecordingLogger }> {
  const logger = opts.logger ?? new RecordingLogger();
  const sub = subscribe({
    connection: conn.handle,
    descriptor,
    handler: () => ok(),
    logger,
    ...opts,
  });
  await vi.waitFor(() => expect(conn.consumer.started).toBe(true));
  conn.consumer.emit("ready");
  await sub.ready;
  return { sub, logger: logger as RecordingLogger };
}

describe("subscribe - consumer host wiring", () => {
  it("Close_RepeatedSubscriberAndOwnerCalls_DisposesConsumerOnce", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    const close = vi.spyOn(conn.consumer, "close");
    const first = sub.close();
    const second = sub.close();
    expect(second).toBe(first);
    await Promise.all([first, second, conn.handle.close()]);
    expect(close).toHaveBeenCalledOnce();
  });

  it("Subscribe_CloseDuringOptionRead_DoesNotAllocateConsumer", async () => {
    const conn = new FakeConnection();
    const allocate = vi.spyOn(conn, "createConsumer");
    const close = vi.spyOn(conn, "close");
    let closed: Promise<void> | undefined;
    const options = {
      connection: conn.handle,
      descriptor,
      handler: () => ok(),
      logger: new RecordingLogger(),
      get store() {
        closed = conn.handle.close();
        return undefined;
      },
    };
    expect(() => subscribe(options)).toThrow("Subscription is stopping.");
    await closed;
    expect(allocate).not.toHaveBeenCalled();
    expect(close).toHaveBeenCalledOnce();
  });

  it("Reconnect_CapturedCallbackAfterClose_DoesNotDeclareTopology", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    const reconnect = conn.connectionCbs[0];
    expect(reconnect).toBeDefined();
    await sub.close();
    const declarations = conn.queueDeclare.mock.calls.length;
    reconnect!();
    expect(conn.queueDeclare).toHaveBeenCalledTimes(declarations);
  });
  it.each([undefined, "decoded", new Uint8Array([1])])(
    "Consume_InvalidRawBody_%s_DropsWithoutInvokingHandler",
    async (rawBody) => {
      const conn = new FakeConnection();
      const handler = vi.fn(() => ok());
      const { logger } = await start(conn, { handler });
      const status = await conn.consumer.handlerCb(
        makeAsyncMessage({ rawBody } as Partial<AsyncMessage>),
      );
      expect(status).toBe(ConsumerStatus.DROP);
      expect(handler).not.toHaveBeenCalled();
      expect(conn.publisher.sent).toHaveLength(0);
      expect(logger.logged("consumer raw body unavailable")).toBe(true);
    },
  );

  it("Subscribe_ConsumerAllocationThrows_RemovesReconnectListener", async () => {
    const conn = new FakeConnection();
    vi.spyOn(conn, "createConsumer").mockImplementation(() => {
      throw new Error("Consumer fixture failed.");
    });
    expect(() =>
      subscribe({
        connection: conn.handle,
        descriptor,
        handler: () => ok(),
        logger: new RecordingLogger(),
      }),
    ).toThrow("Consumer fixture failed.");
    await conn.handle.close();
    expect(conn.connectionCbs).toHaveLength(0);
    expect(conn.closed).toBe(true);
  });

  it("Subscribe_StartupAndCleanupThrow_ObservesBothFailures", async () => {
    const conn = new FakeConnection();
    const logger = new RecordingLogger();
    const allocate = conn.createConsumer.bind(conn);
    vi.spyOn(conn, "createConsumer").mockImplementation((props, callback) => {
      const consumer = allocate(props, callback);
      consumer.close = async () => {
        throw new Error("Consumer cleanup fixture failed.");
      };
      return consumer;
    });
    vi.spyOn(logger, "info").mockImplementation(() => {
      throw new Error("Startup fixture failed.");
    });
    expect(() =>
      subscribe({
        connection: conn.handle,
        descriptor,
        handler: () => ok(),
        logger,
      }),
    ).toThrow("Startup fixture failed.");
    await expect(conn.handle.close()).rejects.toThrow(
      "Connection cleanup failed.",
    );
    expect(logger.logged("subscription cleanup failed")).toBe(true);
    expect(conn.closed).toBe(true);
    releaseClosedSubscriberFixture(conn);
  });

  it("Close_StartupTopologyPending_PreventsConsumerStartAndAwaitsSetup", async () => {
    const conn = new FakeConnection();
    const entered = createCompletion<void>();
    const release = createCompletion<void>();
    conn.queueDeclare.mockImplementation(async () => {
      entered.resolve();
      await release.promise;
    });
    const sub = subscribe({
      connection: conn.handle,
      descriptor,
      handler: () => ok(),
      logger: new RecordingLogger(),
    });
    const readiness = expect(sub.ready).rejects.toThrow(
      "Subscription is stopping.",
    );
    await entered.promise;
    try {
      const closed = sub.close();
      expect(sub.close()).toBe(closed);
      await readiness;
      expect(conn.consumer.started).toBe(false);
      release.resolve();
      await closed;
      expect(conn.consumer.started).toBe(false);
      expect(conn.consumer.closed).toBe(true);
    } finally {
      release.resolve();
      await sub.close();
    }
  });

  it("Close_ManagedOwner_ClosesConsumerBeforeSharedTransport", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    const originalClose = conn.consumer.close.bind(conn.consumer);
    conn.consumer.close = async () => {
      expect(conn.closed).toBe(false);
      await originalClose();
    };
    await conn.handle.close();
    expect(conn.consumer.closed).toBe(true);
    expect(conn.closed).toBe(true);
    await sub.close();
  });

  it("Subscribe_TopologyFailure_RejectsReadinessAndDisposesConsumer", async () => {
    const conn = new FakeConnection();
    conn.queueDeclare.mockRejectedValue(new Error("Topology fixture failed."));
    const sub = subscribe({
      connection: conn.handle,
      descriptor,
      handler: () => ok(),
      logger: new RecordingLogger(),
    });
    await expect(sub.ready).rejects.toThrow("Topology fixture failed.");
    await expect(sub.close()).rejects.toThrow("Subscription cleanup failed.");
    expect(conn.consumer.started).toBe(false);
    expect(conn.consumer.closed).toBe(true);
    await conn.handle.close();
    expect(conn.closed).toBe(true);
  });

  it("passes the .NET topology into ConsumerProps and starts lazily", async () => {
    const conn = new FakeConnection();
    await start(conn);

    const props = conn.consumer.props;
    expect(props["queue"]).toBe("orders.order-placed");
    expect(props["lazy"]).toBe(true);
    expect(props["requeue"]).toBe(false);
    expect(props["concurrency"]).toBe(8);
    expect(props["qos"]).toEqual({ prefetchCount: 8 });
    expect((props["queueOptions"] as { queue: string }).queue).toBe(
      "orders.order-placed",
    );
    expect((props["exchanges"] as unknown[]).length).toBe(2);
    const bindings = props["queueBindings"] as { queue: string }[];
    expect(bindings.every((b) => b.queue === "orders.order-placed")).toBe(true);
    expect(conn.consumer.started).toBe(true);
  });

  it("declares the aux (DLQ) topology up front and again on reconnect", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const afterStart = conn.queueDeclare.mock.calls.length;
    expect(afterStart).toBeGreaterThan(0); // DLQ declared

    conn.fireConnection();
    await vi.waitFor(() =>
      expect(conn.queueDeclare.mock.calls.length).toBeGreaterThan(afterStart),
    );
  });

  it("logs (never throws) when reconnect topology re-declaration fails", async () => {
    const conn = new FakeConnection();
    const { logger } = await start(conn);
    conn.queueDeclare = vi.fn(() =>
      Promise.reject(new Error("declare failed")),
    );
    conn.fireConnection();
    await vi.waitFor(() =>
      expect(logger.logged("aux topology declaration failed")).toBe(true),
    );
  });

  it("maps a successful delivery to ConsumerStatus.ACK", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const status = await conn.consumer.handlerCb(makeAsyncMessage());
    expect(status).toBe(ConsumerStatus.ACK);
  });

  it("maps a republish-failed delivery to ConsumerStatus.DROP", async () => {
    const conn = new FakeConnection();
    await start(conn, { handler: () => fail({ errorCode: "X" }) });
    conn.publisher.sendImpl = () => Promise.reject(new Error("broker down"));
    const status = await conn.consumer.handlerCb(makeAsyncMessage());
    expect(status).toBe(ConsumerStatus.DROP);
  });

  it("normalizes every AsyncMessage body shape (buffer / string / object / bytes)", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const json = JSON.stringify({ v: 1 });
    for (const body of [
      Buffer.from(json, "utf8"),
      json,
      { v: 1 },
      new Uint8Array(Buffer.from(json, "utf8")),
    ]) {
      const status = await conn.consumer.handlerCb(makeAsyncMessage({ body }));
      expect(status).toBe(ConsumerStatus.ACK);
    }
  });

  it("tolerates an AsyncMessage with no headers", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const status = await conn.consumer.handlerCb(
      makeAsyncMessage({ headers: undefined }),
    );
    expect(status).toBe(ConsumerStatus.ACK);
  });

  it("logs a consumer error event", async () => {
    const conn = new FakeConnection();
    const { logger } = await start(conn);
    conn.consumer.emit("error", new Error("channel gone"));
    expect(logger.logged("consumer error")).toBe(true);
  });

  it("closes the consumer + publisher on close()", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn, {
      handler: () => fail({ errorCode: "X" }),
    });
    await conn.consumer.handlerCb(makeAsyncMessage());
    await sub.close();
    expect(conn.consumer.closed).toBe(true);
    expect(conn.publisher.closed).toBe(true);
  });

  it("deregisters the reconnect listener on close (no re-fire after close)", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    expect(conn.connectionCbs.length).toBe(1); // one reconnect listener registered

    await sub.close();
    expect(conn.connectionCbs.length).toBe(0); // deregistered by close()

    // A reconnect after close must not redeclare the aux topology of a closed
    // subscriber.
    const exchangesBefore = conn.exchangeDeclare.mock.calls.length;
    conn.fireConnection();
    expect(conn.exchangeDeclare.mock.calls.length).toBe(exchangesBefore);
  });

  it("resolves the fanout queue name with a per-process suffix", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn, {
      descriptor: {
        ...descriptor,
        pattern: QueuePattern.FanoutExclusiveAutoDelete,
      },
      queueSuffix: "abcd1234",
    });
    expect(sub.queueName).toBe("orders.order-placed.abcd1234");
  });
});

/**
 * Reports whether a promise settles within a budget of event-loop turns, so a
 * close that never settles fails the assertion instead of the test timeout.
 */
async function settlesWithinTurns(
  promise: Promise<unknown>,
  turns: number,
): Promise<boolean> {
  let settled = false;
  promise.then(
    () => (settled = true),
    () => (settled = true),
  );
  for (let turn = 0; turn < turns && !settled; turn++) await immediate();
  return settled;
}

describe("subscribe - one read of the connection address", () => {
  // Each reveal after the first returns a plain address to a remote host, which
  // the transport check refuses and the connection fixture does not know.
  const REFUSED = "amqp://svc:changed@rabbitmq.internal:5672/d2";

  it("Subscribe_ChangingRevealConnection_RevealsOnceDeadLettersToTheCheckedAddressAndCloses", async () => {
    let fixture: ChangingOptionsFixture | undefined;
    configureSubscriberHandleFactory((url) => {
      fixture = createChangingOptionsFixture(url, REFUSED);
      return createConnection(fixture.options, new RecordingLogger());
    });
    const conn = new FakeConnection();
    const { sub } = await start(conn, {
      handler: () => fail({ errorCode: "X" }),
    });
    const status = await conn.consumer.handlerCb(makeAsyncMessage());
    expect(status).toBe(ConsumerStatus.ACK);
    expect(conn.publisher.sent).toHaveLength(1);
    expect(fixture?.reads()).toBe(1);
    await sub.close();
    const closing = conn.handle.close();
    expect(await settlesWithinTurns(closing, 100)).toBe(true);
    await expect(closing).resolves.toBeUndefined();
  });

  it("Subscribe_ThrowBeforeTheResourceIsAttached_ConnectionCloseResolves", async () => {
    const conn = new FakeConnection();
    const options = {
      connection: conn.handle,
      descriptor,
      handler: () => ok(),
      logger: new RecordingLogger(),
      get store(): undefined {
        throw new Error("Store fixture failed.");
      },
    };
    expect(() => subscribe(options)).toThrow("Store fixture failed.");
    const closing = conn.handle.close();
    expect(await settlesWithinTurns(closing, 100)).toBe(true);
    await expect(closing).resolves.toBeUndefined();
    expect(conn.closed).toBe(true);
  });
});
