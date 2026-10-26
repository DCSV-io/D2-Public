// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";

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
  redactAmqpUri,
} from "../src/connection/connection-options.js";

describe("redactAmqpUri - never leaks the broker password", () => {
  it("strips embedded userinfo credentials", () => {
    const redacted = redactAmqpUri(
      "amqps://orders-svc:s3cr3t@rabbitmq.internal:5671/d2",
    );
    expect(redacted).not.toContain("s3cr3t");
    expect(redacted).not.toContain("orders-svc");
    expect(redacted).toContain("rabbitmq.internal");
  });

  it("collapses a malformed URI to its scheme only", () => {
    expect(redactAmqpUri("amqp://:::not a uri")).toBe("amqp://[redacted]");
  });

  it("collapses a scheme-less value to [redacted]", () => {
    expect(redactAmqpUri("garbage")).toBe("[redacted]");
  });
});

describe("createConnection", () => {
  it("CreateConnection_ConfiguredOptions_ConstructsManagedTransport", async () => {
    vendor.options.length = 0;
    const connection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://guest:guest@127.0.0.1:5672/",
        clientProvidedName: "test-svc",
        retryLowMs: 100,
        retryHighMs: 1000,
        heartbeatSeconds: 30,
      }),
      new RecordingLogger(),
    );
    try {
      expect(Object.keys(connection)).toEqual(["close"]);
      expect(vendor.options).toStrictEqual([
        {
          url: "amqp://guest:guest@127.0.0.1:5672/",
          connectionName: "test-svc",
          retryLow: 100,
          retryHigh: 1000,
          heartbeat: 30,
        },
      ]);
    } finally {
      await connection.close();
    }
  });
});
