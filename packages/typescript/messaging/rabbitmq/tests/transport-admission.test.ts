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
import { RecordingLogger } from "./helpers.js";

import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../src/connection/connection-options.js";
import { UnencryptedTransportError } from "../src/connection/transport-admission.js";
import { DlqRepublisher } from "../src/subscribing/dlq-republisher.js";

const PASSWORD = "broker-secret-7f3a";
const REMOTE_PLAIN = `amqp://svc:${PASSWORD}@rabbitmq.internal:5672/d2`;
const REMOTE_ENCRYPTED = `amqps://svc:${PASSWORD}@rabbitmq.internal:5671/d2`;

const LOOPBACK_ADDRESSES = [
  `amqp://svc:${PASSWORD}@localhost:5672/`,
  `amqp://svc:${PASSWORD}@LOCALHOST:5672/`,
  `amqp://svc:${PASSWORD}@127.0.0.1:5672/`,
  `amqp://svc:${PASSWORD}@127.255.255.254:5672/`,
  `amqp://svc:${PASSWORD}@[::1]:5672/`,
  `amqp://svc:${PASSWORD}@[0:0:0:0:0:0:0:1]:5672/`,
];

// Each address uses a host that a resolver can send off the machine, or a host
// that no parser shows to be loopback, so each is refused without the flag.
const REMOTE_LOOKALIKES = [
  `amqp://svc:${PASSWORD}@localhost.example.com:5672/`,
  `amqp://svc:${PASSWORD}@127.0.0.1.example.com:5672/`,
  `amqp://svc:${PASSWORD}@128.0.0.1:5672/`,
  `amqp://svc:${PASSWORD}@126.255.255.255:5672/`,
  `amqp://svc:${PASSWORD}@127.1:5672/`,
  `amqp://svc:${PASSWORD}@127.0.0.01:5672/`,
  `amqp://svc:${PASSWORD}@127.0.0.256:5672/`,
  `amqp://svc:${PASSWORD}@[::2]:5672/`,
  `amqp://svc:${PASSWORD}@/d2`,
];

const DLQ_OPTIONS = { republishMs: 100, cleanupMs: 10 };

function connect(options: RabbitMqConnectionOptions) {
  return createConnection(options, new RecordingLogger());
}

function republisher(
  options: RabbitMqConnectionOptions,
  connectionFactory: () => never,
) {
  return new DlqRepublisher(options, DLQ_OPTIONS, 1, new RecordingLogger(), {
    connectionFactory,
  });
}

beforeEach(() => {
  vendor.connections.length = 0;
  vendor.options.length = 0;
  vendor.construct.mockReset();
  vendor.close.mockReset().mockResolvedValue(undefined);
});

describe("createConnection - encrypted transport", () => {
  it("CreateConnection_PlainAmqpToARemoteHost_ThrowsBeforeAnyConnectionAttempt", () => {
    let caught: unknown;
    try {
      connect(new RabbitMqConnectionOptions({ connectionUri: REMOTE_PLAIN }));
    } catch (error) {
      caught = error;
    }
    expect(caught).toBeInstanceOf(UnencryptedTransportError);
    expect((caught as Error).name).toBe("UnencryptedTransportError");
    expect((caught as Error).message).toContain("amqps");
    expect((caught as Error).message).not.toContain(PASSWORD);
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  it("CreateConnection_PlainAmqpWithAllowUnencryptedTransportFalse_Throws", () => {
    expect(() =>
      connect(
        new RabbitMqConnectionOptions({
          connectionUri: REMOTE_PLAIN,
          allowUnencryptedTransport: false,
        }),
      ),
    ).toThrow(UnencryptedTransportError);
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  it("CreateConnection_AllowUnencryptedTransportNotExactlyTrue_Throws", () => {
    expect(() =>
      connect(
        new RabbitMqConnectionOptions({
          connectionUri: REMOTE_PLAIN,
          allowUnencryptedTransport: "true" as never,
        }),
      ),
    ).toThrow(UnencryptedTransportError);
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  it.each(REMOTE_LOOKALIKES)(
    "CreateConnection_PlainAmqpToANonLoopbackHost_Throws(%s)",
    (connectionUri) => {
      expect(() =>
        connect(new RabbitMqConnectionOptions({ connectionUri })),
      ).toThrow(UnencryptedTransportError);
      expect(vendor.construct).not.toHaveBeenCalled();
    },
  );

  it.each(["", "not a uri", "//rabbitmq.internal:5672/d2"])(
    "CreateConnection_AddressThatIsNotAUrl_ThrowsEvenWithTheFlag(%j)",
    (connectionUri) => {
      expect(() =>
        connect(
          new RabbitMqConnectionOptions({
            connectionUri,
            allowUnencryptedTransport: true,
          }),
        ),
      ).toThrow(UnencryptedTransportError);
      expect(vendor.construct).not.toHaveBeenCalled();
    },
  );

  it.each(LOOPBACK_ADDRESSES)(
    "CreateConnection_PlainAmqpToALoopbackHost_ConnectsWithoutTheFlag(%s)",
    async (connectionUri) => {
      const connection = connect(
        new RabbitMqConnectionOptions({ connectionUri }),
      );
      expect(vendor.options).toStrictEqual([{ url: connectionUri }]);
      await connection.close();
    },
  );

  it("CreateConnection_PlainAmqpToARemoteHostWithAllowUnencryptedTransport_Connects", async () => {
    const connection = connect(
      new RabbitMqConnectionOptions({
        connectionUri: REMOTE_PLAIN,
        allowUnencryptedTransport: true,
      }),
    );
    // The flag is a D2 decision and never reaches the vendor client.
    expect(vendor.options).toStrictEqual([{ url: REMOTE_PLAIN }]);
    await connection.close();
  });

  it("CreateConnection_AmqpsToARemoteHost_ConnectsWithoutTheFlag", async () => {
    const connection = connect(
      new RabbitMqConnectionOptions({ connectionUri: REMOTE_ENCRYPTED }),
    );
    expect(vendor.options).toStrictEqual([{ url: REMOTE_ENCRYPTED }]);
    await connection.close();
  });

  it("CreateConnection_UppercaseAmqpsScheme_ConnectsWithoutTheFlag", async () => {
    const connectionUri = `AMQPS://svc:${PASSWORD}@rabbitmq.internal:5671/d2`;
    const connection = connect(
      new RabbitMqConnectionOptions({ connectionUri }),
    );
    expect(vendor.construct).toHaveBeenCalledOnce();
    await connection.close();
  });
});

describe("DlqRepublisher - encrypted transport", () => {
  it("Create_PlainAmqpToARemoteHost_ThrowsBeforeAnyConnectionAttempt", () => {
    const factory = vi.fn<() => never>();
    let caught: unknown;
    try {
      republisher(
        new RabbitMqConnectionOptions({ connectionUri: REMOTE_PLAIN }),
        factory,
      );
    } catch (error) {
      caught = error;
    }
    expect(caught).toBeInstanceOf(UnencryptedTransportError);
    expect((caught as Error).message).not.toContain(PASSWORD);
    expect(factory).not.toHaveBeenCalled();
  });

  it.each([
    { connectionUri: `amqp://svc:${PASSWORD}@localhost:5672/` },
    { connectionUri: REMOTE_PLAIN, allowUnencryptedTransport: true },
    { connectionUri: REMOTE_ENCRYPTED },
  ])("Create_AdmittedTransport_Constructs(%j)", async (options) => {
    const created = republisher(
      new RabbitMqConnectionOptions(options),
      vi.fn<() => never>(),
    );
    await created.close();
    await created.resourceCompletion;
  });
});
