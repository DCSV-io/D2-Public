// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  RabbitMQContainer,
  type StartedRabbitMQContainer,
} from "@testcontainers/rabbitmq";
import { Connection } from "rabbitmq-client";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { DlqRepublisher } from "../../src/subscribing/dlq-republisher.js";
import { RecordingLogger } from "../helpers.js";
import {
  AmqpReplyFilterFixture,
  type SuppressedReply,
  type ReturnDelivery,
} from "./fixtures/amqp-reply-filter-fixture.js";
import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../../src/connection/connection-options.js";
import { createCompletion } from "../../src/connection/managed-connection.js";
import { subscribe } from "../../src/subscribing/subscriber.js";
import { QueuePattern } from "../../src/topology/queue-pattern.js";
import { randomUUID } from "node:crypto";
import type { AsyncMessage, Consumer } from "rabbitmq-client";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import {
  PackedPackageFixture,
  findMessagingPackageFixture,
  loadPackHelperFixture,
} from "../fixtures/packed-package-fixture.js";
import { join } from "node:path";
import { dlqLifetimeContractFixture } from "../fixtures/dlq-lifetime-contract-fixture.js";

it("Pack_LinuxConsumer_UsesBundledRuntimeAndTypes", async () => {
  const fixture = await PackedPackageFixture.create();
  try {
    const helper = await loadPackHelperFixture();
    const packed = await helper.packPatched({
      packageRoot: await findMessagingPackageFixture(),
      destination: join(fixture.directory, "first"),
    });
    const consumer = await fixture.installConsumer(packed.artifact);
    await fixture.probeConsumer(consumer);
    await fixture.typeCheckConsumer(consumer);
    expect(await fixture.probeLinuxConsumer(consumer)).toContain(
      "Packed D2 runtime and raw-byte mapping passed.",
    );
  } finally {
    await fixture.dispose();
  }
}, 180000);

describe("Dedicated DLQ transport against RabbitMQ", () => {
  let container: StartedRabbitMQContainer;
  let shared: Connection;

  beforeAll(async () => {
    container = await new RabbitMQContainer(
      "rabbitmq:4.1.7-management",
    ).start();
    shared = new Connection(
      container.getAmqpUrl().replace("amqp://", "amqp://guest:guest@"),
    );
    await shared.onConnect();
  }, 180000);

  afterAll(async () => {
    try {
      await shared?.close();
    } finally {
      await container?.stop();
    }
  });

  it.each<ReturnDelivery>(["return-confirm-together", "return-confirm-split"])(
    "Republish_ReturnOrdering_%s_RejectsOnlyReturnedSend",
    async (order) => {
      const proxy = new AmqpReplyFilterFixture(
        container.getHost(),
        container.getMappedPort(5672),
        order,
      );
      const exchange = `fixture.return.${randomUUID()}`;
      const logger = new RecordingLogger();
      const returned = createCompletion<void>();
      const record = logger.error.bind(logger);
      vi.spyOn(logger, "error").mockImplementation((message, bindings) => {
        record(message, bindings);
        if (bindings?.["outcome"] === "unroutable") returned.resolve();
      });
      let publisher: DlqRepublisher | undefined;
      let queue: string | undefined;
      let declared = false;
      try {
        await proxy.start();
        await shared.exchangeDeclare({
          exchange,
          type: "fanout",
          autoDelete: false,
        });
        declared = true;
        publisher = new DlqRepublisher(
          new RabbitMqConnectionOptions({ connectionUri: proxy.uri }),
          { republishMs: 5000, cleanupMs: 1000 },
          1,
          logger,
        );
        const outcome = publisher.republish({
          dlxName: exchange,
          body: Buffer.from([9]),
          headers: {},
        });
        if (order === "return-confirm-split") {
          await returned.promise;
          proxy.releaseHeld();
        }
        expect(await outcome).toBe("unroutable");
        const created = await shared.queueDeclare({
          exclusive: true,
          autoDelete: true,
        });
        queue = created.queue;
        await shared.queueBind({ queue, exchange, routingKey: "" });
        expect(
          await publisher.republish({
            dlxName: exchange,
            body: Buffer.from([8]),
            headers: {},
          }),
        ).toBe("confirmed");
        expect(
          logger.entries.filter(
            (entry) => entry.bindings?.["outcome"] === "unroutable",
          ),
        ).toHaveLength(1);
      } finally {
        proxy.releaseHeld();
        try {
          await publisher?.close();
        } finally {
          try {
            await proxy.close();
          } finally {
            try {
              await publisher?.resourceCompletion;
            } finally {
              try {
                if (queue !== undefined) await shared.queueDelete(queue);
              } finally {
                if (declared) await shared.exchangeDelete({ exchange });
              }
            }
          }
        }
      }
    },
    15000,
  );

  it.each(
    dlqLifetimeContractFixture.deliveries.map((row) => ({
      ...row,
      body: Buffer.from(row.bodyHex, "hex"),
      messageId: row.messageId ?? undefined,
      contentEncoding: row.contentEncoding ?? undefined,
    })),
  )(
    "Consume_OriginalBytesAndId_PreservesDlqTransport($contentType)",
    async (input) => {
      const queueName = `fixture.raw.${randomUUID()}`;
      const exchange = `${queueName}.source`;
      const managed = createConnection(
        new RabbitMqConnectionOptions({
          connectionUri: container
            .getAmqpUrl()
            .replace("amqp://", "amqp://guest:guest@"),
          allowUnencryptedTransport: true,
        }),
        new RecordingLogger(),
      );
      let inspector: Consumer | undefined;
      const received = createCompletion<AsyncMessage>();
      const inspectorReady = createCompletion<void>();
      const observations = Promise.allSettled([
        received.promise,
        inspectorReady.promise,
      ]);
      const logger = new RecordingLogger();
      try {
        const subscription = subscribe({
          connection: managed,
          logger,
          descriptor: {
            queueName,
            exchange,
            exchangeType: "fanout",
            pattern: QueuePattern.DurableShared,
            routingKeyBinding: "",
            prefetch: 1,
            idempotency: false,
            nackedBy: "fixture",
          },
          opener: {
            open: (body) => {
              body.fill(0);
              throw new Error("fixture decode failure");
            },
          },
          handler: () => {
            throw new Error("Handler must not follow a failed opener.");
          },
        });
        await subscription.ready;
        inspector = shared.createConsumer(
          {
            queue: `${queueName}.dlq`,
            queueOptions: { durable: true },
            lazy: true,
          },
          (message) => {
            received.resolve(message);
          },
        );
        inspector.on("error", (error) => {
          inspectorReady.reject(error);
          received.reject(error);
        });
        inspector.on("ready", () => inspectorReady.resolve());
        inspector.start();
        await inspectorReady.promise;
        const channel = await shared.acquire();
        try {
          await channel.basicPublish(
            {
              exchange,
              routingKey: "",
              contentType: input.contentType,
              contentEncoding: input.contentEncoding,
              messageId: input.messageId,
              correlationId: input.correlationId,
              headers: {
                [AmqpHeaders.MESSAGE_ID]: "processing-fallback",
                fixture: input.header,
              },
            },
            input.body,
          );
          const message = await received.promise;
          expect(message.rawBody).toEqual(input.body);
          expect(message.messageId).toBe(input.messageId);
          expect(message.contentType).toBe(input.contentType);
          expect(message.contentEncoding).toBe(input.contentEncoding);
          expect(message.correlationId).toBe(input.correlationId);
          expect(message.headers?.[AmqpHeaders.MESSAGE_ID]).toBe(
            "processing-fallback",
          );
          expect(message.headers?.["fixture"]).toBe(input.header);
        } finally {
          await channel.close();
        }
      } finally {
        inspectorReady.resolve();
        received.reject(new Error("Fixture delivery closed."));
        await observations;
        try {
          await managed.close();
        } finally {
          try {
            await inspector?.close();
          } finally {
            await shared.queueDelete(queueName);
            await shared.queueDelete(`${queueName}.dlq`);
            await shared.exchangeDelete({ exchange });
            await shared.exchangeDelete({ exchange: `${queueName}.dlx` });
          }
        }
      }
    },
    15000,
  );

  it.each<SuppressedReply>(["channel-open", "confirm-select", "confirmation"])(
    "Republish_Withheld_%s_TerminatesOnlyDedicatedTransport",
    async (reply) => {
      const proxy = new AmqpReplyFilterFixture(
        container.getHost(),
        container.getMappedPort(5672),
        reply,
      );
      await proxy.start();
      let dedicated: Connection | undefined;
      let terminated = false;
      const publisher = new DlqRepublisher(
        new RabbitMqConnectionOptions({ connectionUri: proxy.uri }),
        { republishMs: 500, cleanupMs: 1000 },
        1,
        new RecordingLogger(),
        {
          connectionFactory: () => {
            dedicated = new Connection(proxy.uri);
            const terminate = dedicated.unsafeDestroy.bind(dedicated);
            dedicated.unsafeDestroy = () => {
              terminated = true;
              terminate();
            };
            return dedicated;
          },
        },
      );
      const exchange = `fixture.dlx.${reply}`;
      let exchangeDeclared = false;
      let queueName: string | undefined;
      try {
        await shared.exchangeDeclare({
          exchange,
          type: "fanout",
          autoDelete: false,
        });
        exchangeDeclared = true;
        const queue = await shared.queueDeclare({
          exclusive: true,
          autoDelete: true,
        });
        queueName = queue.queue;
        await shared.queueBind({ queue: queueName, exchange, routingKey: "" });
        const outcome = publisher.republish({
          dlxName: exchange,
          body: Buffer.from([1]),
          headers: {},
        });
        await proxy.replySuppressed;
        expect(await outcome).toBe("deadline-exceeded");
        expect(terminated).toBe(true);
        await proxy.clientDisconnected;
        await publisher.close();
        await publisher.resourceCompletion;
        expect(shared.ready).toBe(true);
        await shared.queueDeclare({ queue: queueName, passive: true });
      } finally {
        try {
          await publisher.close();
        } finally {
          try {
            await proxy.close();
          } finally {
            dedicated?.unsafeDestroy();
            try {
              await publisher.resourceCompletion;
            } finally {
              try {
                if (queueName !== undefined)
                  await shared.queueDelete(queueName);
              } finally {
                if (exchangeDeclared) await shared.exchangeDelete({ exchange });
              }
            }
          }
        }
      }
    },
    15000,
  );
});
