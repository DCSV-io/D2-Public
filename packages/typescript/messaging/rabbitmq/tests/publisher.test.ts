// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  PayloadCrypto,
  PayloadCryptoKeyring,
  PayloadOpener,
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import type { MqMessageDescriptor } from "@dcsv-io/d2-messaging-abstractions";
import { afterEach, describe, expect, it, vi } from "vitest";

// With no metrics SDK registered every counter is one shared no-op instance, so
// publishesCounter and publishFailuresCounter are replaced by recording doubles
// that a case can tell apart.
vi.mock("../src/telemetry.js", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/telemetry.js")>()),
  publishesCounter: { add: vi.fn() },
  publishFailuresCounter: { add: vi.fn() },
}));

vi.mock("rabbitmq-client", async () => {
  const { ConnectionFixture } =
    await import("./fixtures/managed-connection-fixture.js");
  return { Connection: ConnectionFixture };
});

import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../src/connection/connection-options.js";
import type { D2Connection } from "../src/connection/managed-connection.js";
import { vendor } from "./fixtures/managed-connection-fixture.js";

import { resolveDomainModes } from "../src/encryption-domain-modes.js";
import { composeBody } from "../src/publishing/body-composer.js";
import { readEncryptionKid } from "../src/publishing/encryption-kid.js";
import {
  createPublisher,
  type PublishEnvelope,
  type PublishSender,
  publishVia,
} from "../src/publishing/publisher.js";
import {
  CryptoBodyOpener,
  assertOpenerMatchesDomain,
} from "../src/subscribing/crypto-body-opener.js";
import { publishesCounter, publishFailuresCounter } from "../src/telemetry.js";
import { RecordingLogger } from "./helpers.js";

const utf8 = new TextEncoder();
const connections = new Set<D2Connection>();

afterEach(async () => {
  await Promise.all([...connections].map((connection) => connection.close()));
  connections.clear();
});

async function makeSealerOpener(
  serviceId: string,
  kid: string,
): Promise<{ sealer: PayloadSealer; opener: PayloadOpener }> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const spki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const pkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );
  const pub = await RecipientPublicKeyring.create(
    serviceId,
    kid,
    new Map([[kid, spki]]),
  );
  const priv = await RecipientPrivateKeyring.create(
    serviceId,
    new Map([[kid, pkcs8]]),
  );

  return { sealer: new PayloadSealer(pub), opener: new PayloadOpener(priv) };
}

function makeSymmetric(kid: string): PayloadCrypto {
  return new PayloadCrypto(
    new PayloadCryptoKeyring(
      kid,
      new Map([[kid, new Uint8Array(32).fill(7)]]),
      utf8.encode("d2/orders"),
    ),
  );
}

function recordingSender(): {
  sender: PublishSender;
  calls: { envelope: PublishEnvelope; body: Buffer }[];
} {
  const calls: { envelope: PublishEnvelope; body: Buffer }[] = [];
  const sender: PublishSender = async (envelope, body) => {
    calls.push({ envelope, body });
  };

  return { sender, calls };
}

const ARCHIVED_DESCRIPTOR: MqMessageDescriptor = {
  constant: "OrderArchivedFixture",
  messageType: "Example.Orders.Events.OrderArchivedFixture",
  exchange: "example.orders.archived",
  exchangeType: "fanout",
  encryption: "payload-fixture-sealed",
};

// A fixture catalog rather than a registry lookup: the framework catalog
// contains no entry of its own, and a test that needs one descriptor should not
// depend on which entries a catalog happens to hold. Declared `as const` so
// binding it as `descriptors` binds the compile-time catalog too, which is
// what makes "RotatedFixture" a publishable key.
const FIXTURE_CATALOG = {
  RotatedFixture: {
    constant: "RotatedFixture",
    messageType: "DcsvIo.D2.Fixture.Events.RotatedFixtureEvent",
    exchange: "example.orders.placed",
    exchangeType: "fanout",
    encryption: "plaintext",
    encryptionReason: "Publisher fixture exercising the plaintext code path.",
    defaultRoutingKey: "",
  },
} as const;

const PLAINTEXT_DESCRIPTOR: MqMessageDescriptor =
  FIXTURE_CATALOG.RotatedFixture;

const FIXTURE_REGISTRY: Readonly<Record<string, MqMessageDescriptor>> =
  FIXTURE_CATALOG;

describe("composeBody (runtime default-deny second lock)", () => {
  const plainDescriptor = PLAINTEXT_DESCRIPTOR;

  it("passes a plaintext body through unencrypted with no kid", async () => {
    const json = utf8.encode('{"a":1}');
    const composed = await composeBody(plainDescriptor, json, {});
    expect(composed.body).toBe(json);
    expect(composed.kid).toBeUndefined();
  });

  it("seals a sealed-domain message via the wired sealer", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "payload-fixture-sealed",
      "seal-1",
    );
    const json = utf8.encode('{"secret":"x"}');
    const composed = await composeBody(ARCHIVED_DESCRIPTOR, json, {
      "payload-fixture-sealed": sealer,
    });
    expect(composed.body[0]).toBe(2);
    expect(composed.kid).toBe("seal-1");
    expect(new Uint8Array(await opener.open(composed.body))).toEqual(json);
  });

  it("encrypts a symmetric-mode domain via the wired crypto", async () => {
    // The production catalog has no symmetric-mode domain; inject a fixture
    // mode map so the test exercises the symmetric compose arm.
    const crypto = makeSymmetric("sym-1");
    const symDescriptor: MqMessageDescriptor = {
      ...ARCHIVED_DESCRIPTOR,
      encryption: "payload-fixture-a",
    };
    const composed = await composeBody(
      symDescriptor,
      utf8.encode('{"n":1}'),
      { "payload-fixture-a": crypto },
      { "payload-fixture-a": "symmetric" },
    );
    expect(composed.body[0]).toBe(1);
    expect(composed.kid).toBe("sym-1");
    expect(new Uint8Array(await crypto.decrypt(composed.body))).toEqual(
      utf8.encode('{"n":1}'),
    );
  });

  it("default-denies an encrypted domain with no wired composer", async () => {
    await expect(
      composeBody(ARCHIVED_DESCRIPTOR, utf8.encode("{}"), {}),
    ).rejects.toThrow(/No composer wired/);
  });

  it("default-denies an unknown encryption domain", async () => {
    const bogus: MqMessageDescriptor = {
      ...ARCHIVED_DESCRIPTOR,
      encryption: "not-a-domain",
    };
    await expect(composeBody(bogus, utf8.encode("{}"), {})).rejects.toThrow(
      /not a known encryption domain/,
    );
  });
});

describe("publishVia", () => {
  it("default-denies an unknown message constant", async () => {
    const { sender, calls } = recordingSender();
    const logger = new RecordingLogger();
    const result = await publishVia(
      sender,
      FIXTURE_REGISTRY,
      {},
      logger,
      "NoSuchMessage",
      {},
    );
    expect(result.failed).toBe(true);
    expect(calls).toHaveLength(0);
    expect(logger.logged("unknown message constant")).toBe(true);
  });

  it("publishes a plaintext message with the right envelope + no kid header", async () => {
    const { sender, calls } = recordingSender();
    const result = await publishVia(
      sender,
      FIXTURE_REGISTRY,
      {},
      new RecordingLogger(),
      "RotatedFixture",
      { domain: "payload-fixture-sealed", kid: "orders-1" },
    );
    expect(result.failed).toBe(false);
    expect(calls).toHaveLength(1);
    const { envelope, body } = calls[0]!;
    expect(envelope.exchange).toBe("example.orders.placed");
    expect(envelope.routingKey).toBe("");
    expect(envelope.contentType).toBe("application/octet-stream");
    expect(envelope.headers[AmqpHeaders.PROTO_TYPE]).toBe(
      "DcsvIo.D2.Fixture.Events.RotatedFixtureEvent",
    );
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBeUndefined();
    expect(envelope.messageId).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
    expect(JSON.parse(body.toString("utf8"))).toEqual({
      domain: "payload-fixture-sealed",
      kid: "orders-1",
    });
  });

  it("seals + publishes a sealed message with the encryption-kid header (compose-once)", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "payload-fixture-sealed",
      "seal-1",
    );
    const { sender, calls } = recordingSender();
    const registry = {
      ...FIXTURE_REGISTRY,
      OrderArchivedFixture: ARCHIVED_DESCRIPTOR,
    };

    const result = await publishVia(
      sender,
      registry,
      { "payload-fixture-sealed": sealer },
      new RecordingLogger(),
      "OrderArchivedFixture",
      { secret: "value" },
    );

    expect(result.failed).toBe(false);
    expect(calls).toHaveLength(1);
    const { envelope, body } = calls[0]!;
    expect(envelope.exchange).toBe("example.orders.archived");
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBe("seal-1");
    expect(body[0]).toBe(2);
    // Compose-once: the single composed body opens to the message.
    expect(
      JSON.parse(Buffer.from(await opener.open(body)).toString("utf8")),
    ).toEqual({ secret: "value" });
  });

  it("maps a broker send failure to a service-unavailable result", async () => {
    const logger = new RecordingLogger();
    const failing: PublishSender = () =>
      Promise.reject(new Error("broker down"));
    const result = await publishVia(
      failing,
      FIXTURE_REGISTRY,
      {},
      logger,
      "RotatedFixture",
      {},
    );
    expect(result.failed).toBe(true);
    expect(logger.logged("publish failed")).toBe(true);
  });

  it("bounds a wedged broker confirm and maps it to a transient failure", async () => {
    const logger = new RecordingLogger();
    // A never-resolving send models a broker confirm that never acks.
    const wedged: PublishSender = () => new Promise<void>(() => {});
    const result = await publishVia(
      wedged,
      FIXTURE_REGISTRY,
      {},
      logger,
      "RotatedFixture",
      {},
      20, // 20ms confirm bound - a wedged confirm must not hang forever
    );

    expect(result.failed).toBe(true);
    expect(logger.logged("publish confirm timed out")).toBe(true);
  });

  describe("counters", () => {
    // Each case counts the calls its own publishVia makes on the shared counters.
    async function countFor(
      sender: PublishSender,
      key: string,
      composers: Readonly<Record<string, never>> = {},
    ): Promise<{ publishes: number; failures: number }> {
      const publishes = vi.mocked(publishesCounter.add);
      const failures = vi.mocked(publishFailuresCounter.add);
      publishes.mockClear();
      failures.mockClear();
      await publishVia(
        sender,
        { ...FIXTURE_REGISTRY, OrderArchivedFixture: ARCHIVED_DESCRIPTOR },
        composers,
        new RecordingLogger(),
        key,
        {},
      );
      return {
        publishes: publishes.mock.calls.length,
        failures: failures.mock.calls.length,
      };
    }

    it("counts nothing for an unknown message constant", async () => {
      const counts = await countFor(recordingSender().sender, "NoSuchMessage");
      expect(counts).toEqual({ publishes: 0, failures: 0 });
    });

    it("counts nothing for a compose failure", async () => {
      // The sealed domain has no wired sealer, so composeBody refuses it.
      const counts = await countFor(
        recordingSender().sender,
        "OrderArchivedFixture",
      );
      expect(counts).toEqual({ publishes: 0, failures: 0 });
    });

    it("returns a service-unavailable failure and sends nothing for a compose failure", async () => {
      const recording = recordingSender();
      const result = await publishVia(
        recording.sender,
        { ...FIXTURE_REGISTRY, OrderArchivedFixture: ARCHIVED_DESCRIPTOR },
        {},
        new RecordingLogger(),
        "OrderArchivedFixture",
        {},
      );

      expect([
        result.failed,
        result.statusCode,
        recording.calls.length,
      ]).toEqual([true, 503, 0]);
    });

    it("counts one attempt and no failure for a confirmed send", async () => {
      const counts = await countFor(recordingSender().sender, "RotatedFixture");
      expect(counts).toEqual({ publishes: 1, failures: 0 });
    });

    it("counts one attempt and one failure for a failed send", async () => {
      const failing: PublishSender = () =>
        Promise.reject(new Error("broker down"));
      const counts = await countFor(failing, "RotatedFixture");
      expect(counts).toEqual({ publishes: 1, failures: 1 });
    });
  });

  it("createPublisher wires a confirm-publisher and publishes + closes through it", async () => {
    const sent: { envelope: PublishEnvelope; body: Buffer }[] = [];
    let closed = false;
    const fakePublisher = {
      send: async (envelope: PublishEnvelope, body: Buffer) => {
        sent.push({ envelope, body });
      },
      close: async () => {
        closed = true;
      },
    };
    vendor.createPublisher.mockReturnValue(fakePublisher);
    const fakeConnection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://publisher.fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    connections.add(fakeConnection);

    const publisher = createPublisher(fakeConnection, {
      crypto: {},
      logger: new RecordingLogger(),
      descriptors: FIXTURE_CATALOG,
    });
    const result = await publisher.publish("RotatedFixture", {
      domain: "payload-fixture-sealed",
    });

    expect(result.failed).toBe(false);
    expect(sent).toHaveLength(1);
    expect(sent[0]!.envelope.exchange).toBe("example.orders.placed");

    await publisher.close();
    expect(closed).toBe(true);
  });

  it("binds the framework catalog when none is supplied, and refuses every constant", async () => {
    const sent: { envelope: PublishEnvelope; body: Buffer }[] = [];
    const fakePublisher = {
      send: async (envelope: PublishEnvelope, body: Buffer) => {
        sent.push({ envelope, body });
      },
      close: async () => {},
    };
    vendor.createPublisher.mockReturnValue(fakePublisher);
    const fakeConnection = createConnection(
      new RabbitMqConnectionOptions({
        connectionUri: "amqp://publisher.fixture",
        allowUnencryptedTransport: true,
      }),
      new RecordingLogger(),
    );
    connections.add(fakeConnection);
    const logger = new RecordingLogger();

    const publisher = createPublisher(fakeConnection, { crypto: {}, logger });

    // The compile-time constraint already refuses every key here, because the
    // framework catalog contains no entry. The cast reaches the runtime lock
    // behind it, which is what a JavaScript caller or a dynamically built key
    // meets.
    const publish = publisher.publish as unknown as (
      key: string,
      message: object,
    ) => Promise<{ failed: boolean }>;
    const result = await publish("RotatedFixture", {});

    expect(result.failed).toBe(true);
    expect(sent).toHaveLength(0);
    expect(logger.logged("unknown message constant")).toBe(true);
  });
});

// An application catalog whose messages publish on domains the framework
// catalog does not contain: one sealed, one symmetric, one never registered.
const APP_CATALOG = {
  OrderArchivedFixture: {
    constant: "OrderArchivedFixture",
    messageType: "D2.Test.OrderArchivedFixture",
    exchange: "d2.test.app-orders",
    exchangeType: "fanout",
    encryption: "app-orders-fixture",
  },
  LedgerPostedFixture: {
    constant: "LedgerPostedFixture",
    messageType: "D2.Test.LedgerPostedFixture",
    exchange: "d2.test.app-ledger",
    exchangeType: "fanout",
    encryption: "app-ledger-fixture",
  },
  UnregisteredFixture: {
    constant: "UnregisteredFixture",
    messageType: "D2.Test.UnregisteredFixture",
    exchange: "d2.test.app-unregistered",
    exchangeType: "fanout",
    encryption: "app-unregistered-fixture",
  },
} as const;

const APP_MODES = {
  "app-orders-fixture": "sealed",
  "app-ledger-fixture": "symmetric",
} as const;

function fakePublisherConnection(): {
  connection: D2Connection;
  sent: { envelope: PublishEnvelope; body: Buffer }[];
} {
  const sent: { envelope: PublishEnvelope; body: Buffer }[] = [];
  vendor.createPublisher.mockReturnValue({
    send: async (envelope: PublishEnvelope, body: Buffer) => {
      sent.push({ envelope, body });
    },
    close: async () => {},
  });
  const connection = createConnection(
    new RabbitMqConnectionOptions({
      connectionUri: "amqp://publisher.fixture",
      allowUnencryptedTransport: true,
    }),
    new RecordingLogger(),
  );
  connections.add(connection);

  return { connection, sent };
}

describe("application-defined encryption domains", () => {
  it("publishes to a registered sealed domain and the consumer opener opens it", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "app-orders-fixture",
      "app-seal-1",
    );
    const { connection, sent } = fakePublisherConnection();
    const publisher = createPublisher(connection, {
      crypto: { "app-orders-fixture": sealer },
      modes: APP_MODES,
      logger: new RecordingLogger(),
      descriptors: APP_CATALOG,
    });

    const result = await publisher.publish("OrderArchivedFixture", {
      actor: "a",
    });

    expect(result.failed).toBe(false);
    expect(sent).toHaveLength(1);
    const { envelope, body } = sent[0]!;
    expect(envelope.exchange).toBe("d2.test.app-orders");
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBe("app-seal-1");
    expect(body[0]).toBe(2);

    const bodyOpener = CryptoBodyOpener.sealed("app-orders-fixture", opener);
    expect(() =>
      assertOpenerMatchesDomain("app-orders-fixture", bodyOpener, APP_MODES),
    ).not.toThrow();
    expect(await bodyOpener.open(body)).toEqual({ actor: "a" });
  });

  it("publishes to a registered symmetric domain and the consumer opener opens it", async () => {
    const crypto = makeSymmetric("app-sym-1");
    const { connection, sent } = fakePublisherConnection();
    const publisher = createPublisher(connection, {
      crypto: { "app-ledger-fixture": crypto },
      modes: APP_MODES,
      logger: new RecordingLogger(),
      descriptors: APP_CATALOG,
    });

    const result = await publisher.publish("LedgerPostedFixture", { n: 3 });

    expect(result.failed).toBe(false);
    expect(sent).toHaveLength(1);
    const { envelope, body } = sent[0]!;
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBe("app-sym-1");
    expect(body[0]).toBe(1);

    const bodyOpener = CryptoBodyOpener.symmetric("app-ledger-fixture", crypto);
    expect(() =>
      assertOpenerMatchesDomain("app-ledger-fixture", bodyOpener, APP_MODES),
    ).not.toThrow();
    expect(await bodyOpener.open(body)).toEqual({ n: 3 });
  });

  it("refuses a message on a domain that is neither in the catalog nor registered", async () => {
    const crypto = makeSymmetric("app-sym-1");
    const logger = new RecordingLogger();
    const { connection, sent } = fakePublisherConnection();
    const publisher = createPublisher(connection, {
      crypto: { "app-ledger-fixture": crypto },
      modes: APP_MODES,
      logger,
      descriptors: APP_CATALOG,
    });

    // The compile-time constraint refuses this key (its domain has no wired
    // composer); the cast reaches the runtime lock behind it.
    const publish = publisher.publish as unknown as (
      key: string,
      message: object,
    ) => Promise<{ failed: boolean }>;
    const result = await publish("UnregisteredFixture", {});

    expect(result.failed).toBe(true);
    expect(sent).toHaveLength(0);
    expect(logger.logged("publish failed")).toBe(true);
    await expect(
      composeBody(
        APP_CATALOG.UnregisteredFixture,
        utf8.encode("{}"),
        { "app-unregistered-fixture": crypto },
        resolveDomainModes(APP_MODES),
      ),
    ).rejects.toThrow(
      "Message domain 'app-unregistered-fixture' is not a known encryption domain",
    );
  });

  it("refuses to publish when a registered sealed domain is wired with a symmetric crypto", async () => {
    const logger = new RecordingLogger();
    const { connection, sent } = fakePublisherConnection();
    const publisher = createPublisher(connection, {
      // The compile-time slot requires a sealer; the cast models a JavaScript
      // caller that wires the wrong composer kind anyway.
      crypto: {
        "app-orders-fixture": makeSymmetric("app-sym-1") as unknown as Awaited<
          ReturnType<typeof makeSealerOpener>
        >["sealer"],
      },
      modes: APP_MODES,
      logger,
      descriptors: APP_CATALOG,
    });

    const result = await publisher.publish("OrderArchivedFixture", {});

    expect(result.failed).toBe(true);
    expect(sent).toHaveLength(0);
    expect(logger.logged("publish failed")).toBe(true);
  });

  it("refuses a registration that re-maps a catalog domain to another mode, before opening any publisher", () => {
    const { connection } = fakePublisherConnection();
    vendor.createPublisher.mockClear();

    expect(() =>
      createPublisher(connection, {
        crypto: {},
        modes: { "payload-fixture-sealed": "symmetric" },
        logger: new RecordingLogger(),
        descriptors: APP_CATALOG,
      }),
    ).toThrow(
      "Encryption domain 'payload-fixture-sealed' was already registered as sealed; cannot re-register as symmetric.",
    );
    expect(vendor.createPublisher).not.toHaveBeenCalled();
  });

  it("accepts a registration that repeats a catalog domain's own mode", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "payload-fixture-sealed",
      "seal-1",
    );
    const { connection, sent } = fakePublisherConnection();
    const publisher = createPublisher(connection, {
      crypto: { "payload-fixture-sealed": sealer },
      modes: { "payload-fixture-sealed": "sealed" },
      logger: new RecordingLogger(),
      descriptors: { OrderArchivedFixture: ARCHIVED_DESCRIPTOR },
    });

    const publish = publisher.publish as unknown as (
      key: string,
      message: object,
    ) => Promise<{ failed: boolean }>;
    const result = await publish("OrderArchivedFixture", { x: 1 });

    expect(result.failed).toBe(false);
    expect(sent[0]!.body[0]).toBe(2);
    expect(
      JSON.parse(Buffer.from(await opener.open(sent[0]!.body)).toString()),
    ).toEqual({ x: 1 });
  });
});

describe("resolveDomainModes", () => {
  it("returns the framework catalog when nothing is registered", () => {
    const modes = resolveDomainModes();

    expect(modes["payload-fixture-sealed"]).toBe("sealed");
    expect(modes["plaintext"]).toBe("symmetric");
    expect(Object.keys(modes)).toHaveLength(2);
  });

  it("adds registered application domains to the framework catalog", () => {
    const modes = resolveDomainModes(APP_MODES);

    expect(modes["app-orders-fixture"]).toBe("sealed");
    expect(modes["app-ledger-fixture"]).toBe("symmetric");
    expect(modes["payload-fixture-sealed"]).toBe("sealed");
    expect(Object.isFrozen(modes)).toBe(true);
  });

  it("resolves no mode for an inherited object member name", () => {
    const modes = resolveDomainModes(APP_MODES);

    expect(modes["constructor"]).toBeUndefined();
    expect(modes["toString"]).toBeUndefined();
  });

  it("refuses a mode for the plaintext sentinel", () => {
    expect(() =>
      resolveDomainModes({ plaintext: "sealed" } as Record<string, "sealed">),
    ).toThrow(
      "Encryption domain 'plaintext' is the cleartext sentinel; it cannot be registered with an encryption mode.",
    );
  });

  it.each(["", "   "])(
    "refuses an empty or whitespace domain name (%j)",
    (domain) => {
      expect(() => resolveDomainModes({ [domain]: "sealed" })).toThrow(
        "An application encryption domain name is empty or whitespace.",
      );
    },
  );

  it("refuses a mode value outside sealed and symmetric", () => {
    expect(() =>
      resolveDomainModes({
        "app-orders-fixture": "open",
      } as unknown as Record<string, "sealed">),
    ).toThrow(
      "Encryption domain 'app-orders-fixture' has mode 'open'; expected 'sealed' or 'symmetric'.",
    );
  });
});

describe("readEncryptionKid", () => {
  it("reads the kid from v1 and v2 frames and fails loud otherwise", async () => {
    const crypto = makeSymmetric("v1-kid");
    expect(readEncryptionKid(await crypto.encrypt(utf8.encode("x")))).toBe(
      "v1-kid",
    );

    const { sealer } = await makeSealerOpener(
      "payload-fixture-sealed",
      "v2-kid",
    );
    expect(readEncryptionKid(await sealer.seal(utf8.encode("x")))).toBe(
      "v2-kid",
    );

    expect(() => readEncryptionKid(new Uint8Array([2]))).toThrow(/too short/);
    expect(() => readEncryptionKid(new Uint8Array([9, 1, 65]))).toThrow(
      /Unknown encryption frame version/,
    );
    expect(() => readEncryptionKid(new Uint8Array([1, 40, 65]))).toThrow(
      /too short for declared kid/,
    );
  });
});
