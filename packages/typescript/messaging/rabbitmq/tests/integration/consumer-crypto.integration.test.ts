// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  RabbitMQContainer,
  type StartedRabbitMQContainer,
} from "@testcontainers/rabbitmq";
import {
  PayloadCrypto,
  PayloadCryptoKeyring,
  PayloadOpener,
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import { RecordingLogger, waitUntil } from "../helpers.js";
import type { MqMessageDescriptor } from "@dcsv-io/d2-messaging-abstractions";
import { ok } from "@dcsv-io/d2-result";
import { uuidv7 } from "@dcsv-io/d2-utilities";
import { Connection } from "rabbitmq-client";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

import {
  createConnection,
  RabbitMqConnectionOptions,
  type D2Connection,
  CryptoBodyOpener,
  QueuePattern,
  type SubscriptionDescriptor,
  composeBody,
  createPublisher,
  subscribe,
} from "../../src/index.js";

// -----------------------------------------------------------------------
// The shipped TypeScript consumer pipeline decrypts a real encrypted frame
// delivered by a real broker. Unit round-trips (crypto-body-opener.test.ts and
// publisher.test.ts) and the cross-runtime golden and KAT gates cover the
// codecs. The symmetric and sealed rows below cover the full delivery path:
// publish (the shipped compose or createPublisher fusion), live RabbitMQ, the
// shipped `subscribe` with `CryptoBodyOpener` decrypting, and the handler
// receiving the plaintext object.
// -----------------------------------------------------------------------

const utf8 = new TextEncoder();

let uniqueCounter = 0;
function unique(prefix: string): string {
  uniqueCounter += 1;
  return `${prefix}.${Date.now().toString(36)}.${uniqueCounter}`;
}

function makeSubscription(
  queueName: string,
  exchange: string,
): SubscriptionDescriptor {
  return {
    queueName,
    exchange,
    exchangeType: "fanout",
    pattern: QueuePattern.DurableShared,
    routingKeyBinding: "",
    prefetch: 4,
    idempotency: false,
    nackedBy: "test-consumer",
  };
}

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

describe("@dcsv-io/d2-messaging-rabbitmq consumer - real-broker encrypted decrypt", () => {
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

  async function publishRaw(
    exchange: string,
    body: Uint8Array,
    messageId: string,
  ): Promise<void> {
    const publisher = connection.createPublisher({ confirm: true });
    try {
      await publisher.send(
        {
          exchange,
          routingKey: "",
          durable: true,
          contentType: "application/octet-stream",
          messageId,
        },
        Buffer.from(body),
      );
    } finally {
      await publisher.close();
    }
  }

  it("row 1 - symmetric: consumes + decrypts a real v1 frame off the broker via CryptoBodyOpener.symmetric", async () => {
    // A fixture domain that the domain leaf provides for tests. The production
    // catalog contains no symmetric domain, so this is a test-seam domain
    // (Symmetric by default).
    const domain = "payload-fixture-symmetric";
    const kid = "sym-fixture-1";
    const crypto = new PayloadCrypto(
      new PayloadCryptoKeyring(
        kid,
        new Map([[kid, new Uint8Array(32).fill(11)]]),
        utf8.encode("d2/" + domain),
      ),
    );
    const exchange = unique("d2.test.sym");
    const queueName = unique("q.sym");
    const payload = { marker: "sym-" + uuidv7(), n: 42 };

    // Publish side - the shipped symmetric compose arm builds the real v1 frame.
    // The frame is composed through `composeBody`'s mode-override argument and
    // put on the wire by a raw confirm-publisher.
    const descriptor: MqMessageDescriptor = {
      constant: "SymFixture",
      messageType: "D2.Test.SymFixture",
      exchange,
      exchangeType: "fanout",
      encryption: domain,
    };
    const composed = await composeBody(
      descriptor,
      utf8.encode(JSON.stringify(payload)),
      { [domain]: crypto },
      { [domain]: "symmetric" },
    );
    expect(composed.body[0]).toBe(1); // v1 symmetric frame version byte

    // Consume side - the shipped subscriber pipeline + the real decrypting opener.
    let received: unknown;
    const sub = subscribe({
      connection: managedConnection,
      descriptor: makeSubscription(queueName, exchange),
      logger: silentLogger(),
      opener: CryptoBodyOpener.symmetric(domain, crypto),
      handler: (message) => {
        received = message;
        return ok();
      },
    });
    await sub.ready;

    await publishRaw(exchange, composed.body, uuidv7());
    await waitUntil(() => received !== undefined);

    expect(received).toEqual(payload);
    await sub.close();
  });

  it("row 2 - sealed: consumes + opens a real v2 sealed frame off the broker via CryptoBodyOpener.sealed", async () => {
    // Public catalog sealed domain (`payload-fixture-sealed`): it seals to the
    // recipient public keyring, and only the holder of the private keyring can
    // open the frame.
    const domain = "payload-fixture-sealed";
    const kid = "seal-fixture-1";
    const { sealer, opener } = await makeSealerOpener(domain, kid);
    const exchange = unique("d2.test.sealed");
    const queueName = unique("q.sealed");
    const payload = { marker: "sealed-" + uuidv7(), secret: "top" };

    // Publish side - the shipped createPublisher fusion over the live connection.
    // The production catalog contains no sealed message, so the documented
    // `descriptors` fixture-registry option supplies one. The publish key is cast
    // because the compile-time type witness covers only the production catalog;
    // fixture messages are outside it by design, and here the runtime
    // default-deny and the injected registry enforce the restriction.
    // The crypto map key must equal descriptor.encryption (composeBody looks up
    // the domain by it).
    const fixtureRegistry: Readonly<Record<string, MqMessageDescriptor>> = {
      SealedOrderFixture: {
        constant: "SealedOrderFixture",
        messageType: "D2.Test.SealedOrderFixture",
        exchange,
        exchangeType: "fanout",
        encryption: domain,
      },
    };
    const publisher = createPublisher(managedConnection, {
      crypto: { "payload-fixture-sealed": sealer },
      logger: silentLogger(),
      descriptors: fixtureRegistry,
    });

    // Consume side - the shipped subscriber pipeline + the real sealed opener.
    let received: unknown;
    const sub = subscribe({
      connection: managedConnection,
      descriptor: makeSubscription(queueName, exchange),
      logger: silentLogger(),
      opener: CryptoBodyOpener.sealed(domain, opener),
      handler: (message) => {
        received = message;
        return ok();
      },
    });
    await sub.ready;

    const publish = await (
      publisher.publish as (
        key: string,
        message: object,
      ) => Promise<{ failed: boolean }>
    )("SealedOrderFixture", payload);
    expect(publish.failed).toBe(false);

    await waitUntil(() => received !== undefined);
    expect(received).toEqual(payload);

    await publisher.close();
    await sub.close();
  });
});

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
