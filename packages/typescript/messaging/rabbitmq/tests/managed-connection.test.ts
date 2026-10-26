// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("rabbitmq-client", async () => {
  const { ConnectionFixture } =
    await import("./fixtures/managed-connection-fixture.js");
  return { Connection: ConnectionFixture };
});

import { vendor } from "./fixtures/managed-connection-fixture.js";

import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../src/connection/connection-options.js";
import {
  createCompletion,
  registerConnectionResource,
} from "../src/connection/managed-connection.js";
import { createPublisher as createD2Publisher } from "../src/publishing/publisher.js";
import { RecordingLogger } from "./helpers.js";
import { subscribe } from "../src/subscribing/subscriber.js";
import { QueuePattern } from "../src/topology/queue-pattern.js";

beforeEach(() => {
  vendor.connections.length = 0;
  vendor.options.length = 0;
  vendor.construct.mockReset();
  vendor.close.mockReset().mockResolvedValue(undefined);
  vendor.publisherClose.mockReset().mockResolvedValue(undefined);
  vendor.createPublisher.mockReset().mockImplementation(() => ({
    send: async () => {},
    close: vendor.publisherClose,
  }));
});

describe("Managed connection ownership", () => {
  it("Create_ConstructorThrows_PreservesOriginalFailure", () => {
    const failure = new Error("constructor failed");
    vendor.construct.mockImplementation(() => {
      throw failure;
    });
    let caught: unknown;
    try {
      createConnection(
        new RabbitMqConnectionOptions({
          connectionUri: "amqp://fixture",
          allowUnencryptedTransport: true,
        }),
        new RecordingLogger(),
      );
    } catch (error) {
      caught = error;
    }
    expect(caught).toBe(failure);
    expect(vendor.connections).toHaveLength(0);
  });

  it.each([undefined, null, {}, { error: null }, { error: "invalid" }])(
    "Create_InvalidLogger_RejectsBeforeAllocation(%j)",
    (logger) => {
      expect(() =>
        createConnection(
          new RabbitMqConnectionOptions({
            connectionUri: "amqp://fixture",
            allowUnencryptedTransport: true,
          }),
          logger as never,
        ),
      ).toThrow(TypeError);
      expect(vendor.connections).toHaveLength(0);
    },
  );

  it("Error_BeforeRegistration_RecordsOnlySanitizedMetadata", async () => {
    const logger = new RecordingLogger();
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://user:password@fixture",
        allowUnencryptedTransport: true,
      }),
      logger,
    );
    const failure = new Error("amqp://user:password@fixture payload-private");
    failure.stack =
      "Error: amqp://user:password@fixture payload-private\n    at fixture (transport.js:1:2)";
    try {
      vendor.connections[0]!.emit("error", failure);
      expect(logger.entries).toEqual([
        {
          level: "error",
          message: "RabbitMQ connection error",
          bindings: {
            error: {
              name: "Error",
              firstFrame: "at fixture (transport.js:1:2)",
            },
          },
        },
      ]);
      expect(JSON.stringify(logger.entries)).not.toMatch(
        /password|payload-private|amqp:/,
      );
      expect(vendor.connections[0]!.listenerCount("error")).toBe(1);
    } finally {
      await connection.close();
    }
  });

  it.each([false, true])(
    "Error_DuringAndAfterClose_RemainsObserved(failure=%s)",
    async (fails) => {
      const logger = new RecordingLogger();
      const connection = createConnection(
        new RabbitMqConnectionOptions({
          connectionUri: "amqp://fixture",
          allowUnencryptedTransport: true,
        }),
        logger,
      );
      const pending = createCompletion<void>();
      vendor.close.mockReturnValue(pending.promise);
      const closing = connection.close();
      const observed = Promise.allSettled([closing]);
      try {
        vendor.connections[0]!.emit("error", new Error("during close"));
        if (fails) pending.reject(new Error("close failure"));
        else pending.resolve();
        expect((await observed)[0]!.status).toBe(
          fails ? "rejected" : "fulfilled",
        );
        vendor.connections[0]!.emit("error", new Error("after close"));
        expect(logger.entries).toHaveLength(2);
        expect(vendor.connections[0]!.listenerCount("error")).toBe(1);
        expect(connection.close()).toBe(closing);
      } finally {
        pending.resolve();
        await observed;
      }
    },
  );

  it("Error_LoggerThrows_DoesNotEscapeOrRetry", async () => {
    const logger = new RecordingLogger();
    const error = vi.spyOn(logger, "error").mockImplementation(() => {
      throw new Error("logger failed");
    });
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      logger,
    );
    try {
      expect(() =>
        vendor.connections[0]!.emit("error", new Error("transport failed")),
      ).not.toThrow();
      expect(error).toHaveBeenCalledOnce();
      expect(vendor.close).not.toHaveBeenCalled();
    } finally {
      await connection.close();
    }
  });

  it("Error_SanitizationThrows_DoesNotEscapeOrCallLogger", async () => {
    const logger = new RecordingLogger();
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      logger,
    );
    const failure = new Error("private error");
    Object.defineProperty(failure, "name", {
      get() {
        throw new Error("unreadable name");
      },
    });
    try {
      expect(() => vendor.connections[0]!.emit("error", failure)).not.toThrow();
      expect(logger.entries).toEqual([]);
    } finally {
      await connection.close();
    }
  });

  it("Create_TransportErrorBeforeRegistration_DoesNotThrowUncaughtEvent", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    try {
      expect(() =>
        vendor.connections[0]!.emit(
          "error",
          new Error("Transport unavailable."),
        ),
      ).not.toThrow();
    } finally {
      await connection.close();
    }
  });

  it("CreatePublisher_AllocationThrows_DeregistersFailedSetup", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    vendor.createPublisher.mockImplementation(() => {
      throw new Error("Publisher fixture failed.");
    });
    expect(() =>
      createD2Publisher(connection, {
        crypto: {},
        logger: new RecordingLogger(),
      }),
    ).toThrow("Publisher fixture failed.");
    await connection.close();
    expect(vendor.close).toHaveBeenCalledOnce();
  });

  it("Attach_AlreadyAttached_RejectsReplacementAndFailure", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const registration = registerConnectionResource(connection);
    const close = vi.fn(async () => {});
    registration.attach({ close });
    expect(() => registration.attach({ close: async () => {} })).toThrow(
      "Resource registration is already attached.",
    );
    expect(() =>
      registration.failSetup(new Error("Unexpected failure")),
    ).toThrow("Resource registration is already attached.");
    await connection.close();
    expect(close).toHaveBeenCalledOnce();
  });

  it("Close_FailedSetup_ReportsOriginalFailure", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const registration = registerConnectionResource(connection);
    const failure = new Error("Setup fixture failed.");
    registration.failSetup(failure);
    await expect(registration.close()).rejects.toBe(failure);
    await registration.resourceCompletion;
    await connection.close();
  });

  it("Close_SharedTransportThrows_PreservesFailure", async () => {
    const failure = new Error("Shared transport fixture failed.");
    vendor.close.mockRejectedValue(failure);
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    await expect(connection.close()).rejects.toMatchObject({
      errors: [failure],
    });
    expect(vendor.close).toHaveBeenCalledOnce();
  });

  it("Publish_ManagedConnectionClosed_ReturnsServiceUnavailableWithoutSend", async () => {
    const send = vi.fn(async () => {});
    vendor.createPublisher.mockReturnValue({
      send,
      close: vendor.publisherClose,
    });
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const publisher = createD2Publisher(connection, {
      crypto: {},
      logger: new RecordingLogger(),
      descriptors: {
        Fixture: {
          constant: "Fixture",
          messageType: "Fixture",
          exchange: "fixture",
          exchangeType: "fanout",
          encryption: "plaintext",
          encryptionReason: "Fixture plaintext input",
          defaultRoutingKey: "",
        },
      } as const,
    });
    await connection.close();
    const result = await publisher.publish("Fixture", {});
    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
    expect(send).not.toHaveBeenCalled();
  });

  it("Register_IndependentResources_AssignsDistinctMonotonicIds", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const first = registerConnectionResource(connection);
    const second = registerConnectionResource(connection);
    first.attach({ close: async () => {} });
    second.attach({ close: async () => {} });
    try {
      expect(first.id).toBe(1n);
      expect(second.id).toBe(2n);
    } finally {
      await connection.close();
    }
  });

  it("Close_BoundedChild_ReturnsBeforeRetainedResourceCompletion", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const registration = registerConnectionResource(connection);
    const resources = createCompletion<void>();
    registration.attach({
      close: async () => {},
      resourceCompletion: resources.promise,
    });
    try {
      expect(registration.resourceCompletion).toBeInstanceOf(Promise);
      let settled = false;
      const observation = registration.resourceCompletion.then(() => {
        settled = true;
      });
      await connection.close();
      expect(settled).toBe(false);
      resources.resolve();
      await observation;
      expect(settled).toBe(true);
    } finally {
      resources.resolve();
      await connection.close();
    }
  });

  it("CreateConnection_AbsentOptionalSettings_PreservesVendorDefaults", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    expect(vendor.options).toStrictEqual([{ url: "amqp://fixture" }]);
    await connection.close();
  });

  it("CreateConnection_ExplicitSettings_PreservesConfiguredValues", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
        clientProvidedName: "fixture",
        retryLowMs: 10,
        retryHighMs: 20,
        heartbeatSeconds: 0,
      }),
      new RecordingLogger(),
    );
    expect(vendor.options).toStrictEqual([
      {
        url: "amqp://fixture",
        connectionName: "fixture",
        retryLow: 10,
        retryHigh: 20,
        heartbeat: 0,
      },
    ]);
    await connection.close();
  });

  it("Subscribe_ForgedHandle_RejectsBeforeVendorAccess", () => {
    const forged = { close: async () => {} };
    expect(() =>
      subscribe({
        connection: forged as never,
        descriptor: {
          queueName: "fixture.queue",
          exchange: "fixture.exchange",
          exchangeType: "fanout",
          pattern: QueuePattern.DurableShared,
          routingKeyBinding: "",
          prefetch: 1,
          idempotency: false,
        },
        handler: () => {
          throw new Error("No delivery is expected.");
        },
        logger: new RecordingLogger(),
      }),
    ).toThrow("Connection was not created by D2.");
  });

  it("Subscribe_NonpositivePrefetch_RejectsBeforeAllocation", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    expect(() =>
      subscribe({
        connection: connection as never,
        descriptor: {
          queueName: "fixture.queue",
          exchange: "fixture.exchange",
          exchangeType: "fanout",
          pattern: QueuePattern.DurableShared,
          routingKeyBinding: "",
          prefetch: 0,
          idempotency: false,
        },
        handler: () => {
          throw new Error("No delivery is expected.");
        },
        logger: new RecordingLogger(),
      }),
    ).toThrow("Prefetch must be positive.");
    expect(vendor.createPublisher).not.toHaveBeenCalled();
    await connection.close();
  });

  it("CreatePublisher_ForgedHandle_RejectsBeforeVendorAccess", () => {
    const forged = {
      close: async () => {},
      createPublisher: vendor.createPublisher,
    };
    expect(() =>
      createD2Publisher(forged, { crypto: {}, logger: new RecordingLogger() }),
    ).toThrow("Connection was not created by D2.");
    expect(vendor.createPublisher).not.toHaveBeenCalled();
  });

  it("Close_DuringPublisherAllocation_OwnsLateResource", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    let close: Promise<void> | undefined;
    vendor.createPublisher.mockImplementation(() => {
      close = connection.close();
      return { send: async () => {}, close: vendor.publisherClose };
    });
    const publisher = createD2Publisher(connection, {
      crypto: {},
      logger: new RecordingLogger(),
    });
    await close;
    expect(vendor.publisherClose).toHaveBeenCalledOnce();
    await publisher.close();
    expect(vendor.publisherClose).toHaveBeenCalledOnce();
  });

  it("Close_BoundedChildCompletes_RetainsLateResourceOutcome", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const registration = registerConnectionResource(connection);
    const resources = createCompletion<void>();
    registration.attach({
      close: async () => {},
      resourceCompletion: resources.promise,
    });
    await connection.close();
    expect(registration.resourceOutcome).toBeUndefined();
    const failure = new Error("late cleanup failure");
    resources.reject(failure);
    await registration.resourceObservation;
    expect(registration.resourceOutcome).toEqual({
      status: "rejected",
      reason: failure,
    });
  });

  it("Close_StopsEveryRegistrationBeforeWaiting_ClosesAllChildren", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const first = registerConnectionResource(connection);
    const second = registerConnectionResource(connection);
    const release = createCompletion<void>();
    const secondClose = vi.fn(async () => {});
    first.attach({ close: () => release.promise });
    second.attach({ close: secondClose });
    const close = connection.close();
    expect(first.signal.aborted).toBe(true);
    expect(second.signal.aborted).toBe(true);
    release.resolve();
    await close;
    expect(secondClose).toHaveBeenCalledOnce();
  });

  it("CreateConnection_PublicHandle_ExposesOnlyClose", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    expect(Object.keys(connection)).toEqual(["close"]);
    await connection.close();
  });

  it("Close_RepeatedCall_SharesOneCompletion", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    const first = connection.close();
    const second = connection.close();
    expect(second).toBe(first);
    await first;
    expect(vendor.close).toHaveBeenCalledOnce();
  });

  it("Close_RegisteredPublisher_ClosesBeforeSharedConnection", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    createD2Publisher(connection, {
      crypto: {},
      logger: new RecordingLogger(),
    });
    await connection.close();
    expect(vendor.publisherClose).toHaveBeenCalledOnce();
    expect(vendor.publisherClose.mock.invocationCallOrder[0]).toBeLessThan(
      vendor.close.mock.invocationCallOrder[0]!,
    );
  });

  it("CreatePublisher_AfterConnectionClose_RejectsBeforeAllocation", async () => {
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    await connection.close();
    expect(() =>
      createD2Publisher(connection, {
        crypto: {},
        logger: new RecordingLogger(),
      }),
    ).toThrow();
    expect(vendor.createPublisher).not.toHaveBeenCalled();
  });

  it("Close_ChildFailure_StillClosesSharedConnectionAndRejects", async () => {
    const failure = new Error("publisher close failed");
    vendor.publisherClose.mockRejectedValue(failure);
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    createD2Publisher(connection, {
      crypto: {},
      logger: new RecordingLogger(),
    });
    await expect(connection.close()).rejects.toThrow();
    expect(vendor.close).toHaveBeenCalledOnce();
  });
});
