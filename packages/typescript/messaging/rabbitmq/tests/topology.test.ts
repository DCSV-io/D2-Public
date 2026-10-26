// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { DlqNaming } from "../src/topology/dlq-naming.js";
import { QueuePattern, queueFlagsFor } from "../src/topology/queue-pattern.js";
import { planTopology } from "../src/topology/topology-plan.js";
import {
  resolveQueueName,
  type SubscriptionDescriptor,
} from "../src/topology/subscription-descriptor.js";

const baseDescriptor: SubscriptionDescriptor = {
  queueName: "orders.order-placed",
  exchange: "example.orders.placed",
  exchangeType: "fanout",
  pattern: QueuePattern.DurableShared,
  routingKeyBinding: "",
  prefetch: 8,
  idempotency: false,
};

describe("DlqNaming - byte-identical to the .NET convention", () => {
  it("derives dlx / dlq / retry names from the queue name", () => {
    expect(DlqNaming.dlxFor("q")).toBe("q.dlx");
    expect(DlqNaming.dlqFor("q")).toBe("q.dlq");
    expect(DlqNaming.retryTierExchangeFor("q", 0)).toBe("q.retry.0");
    expect(DlqNaming.retryTierQueueFor("q", 2)).toBe("q.retry.2");
    expect(DlqNaming.retryReturnExchangeFor("q")).toBe("q.retry.return");
  });
});

describe("queueFlagsFor - mirrors DefaultTopologyDeclarer.QueueFlagsFor", () => {
  it("CompetingConsumer + DurableShared are durable, non-exclusive", () => {
    expect(queueFlagsFor(QueuePattern.CompetingConsumer)).toEqual({
      durable: true,
      exclusive: false,
      autoDelete: false,
    });
    expect(queueFlagsFor(QueuePattern.DurableShared)).toEqual({
      durable: true,
      exclusive: false,
      autoDelete: false,
    });
  });

  it("FanoutExclusiveAutoDelete is non-durable, exclusive, auto-delete", () => {
    expect(queueFlagsFor(QueuePattern.FanoutExclusiveAutoDelete)).toEqual({
      durable: false,
      exclusive: true,
      autoDelete: true,
    });
  });

  it("throws on an unknown pattern", () => {
    expect(() => queueFlagsFor("bogus" as QueuePattern)).toThrow(
      /Unknown queue pattern/,
    );
  });
});

describe("resolveQueueName - per-process suffix for fanout-exclusive only", () => {
  it("leaves non-fanout patterns unchanged", () => {
    expect(resolveQueueName(baseDescriptor)).toBe("orders.order-placed");
  });

  it("appends an explicit suffix for fanout-exclusive", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    expect(resolveQueueName(d, "abcd1234")).toBe(
      "orders.order-placed.abcd1234",
    );
  });

  it("generates an 8-char hex suffix when none supplied", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    const resolved = resolveQueueName(d);
    expect(resolved).toMatch(/^orders\.order-placed\.[0-9a-f]{8}$/);
  });
});

describe("planTopology - the DLQ/DLX wire-contract vs .NET DefaultTopologyDeclarer", () => {
  it("declares primary exchange + DLX + DLQ + primary queue with matching args", () => {
    const plan = planTopology(baseDescriptor, "orders.order-placed");

    // Exactly two exchanges: primary + DLX (fanout).
    expect(plan.exchanges).toEqual([
      {
        exchange: "example.orders.placed",
        type: "fanout",
        durable: true,
        autoDelete: false,
      },
      {
        exchange: "orders.order-placed.dlx",
        type: "fanout",
        durable: true,
        autoDelete: false,
      },
    ]);

    // Exactly two queues: DLQ + primary (primary carries the DLX args).
    expect(plan.queues).toEqual([
      {
        queue: "orders.order-placed.dlq",
        durable: true,
        exclusive: false,
        autoDelete: false,
      },
      {
        queue: "orders.order-placed",
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: {
          "x-dead-letter-exchange": "orders.order-placed.dlx",
          "x-dead-letter-routing-key": "",
        },
      },
    ]);

    // Exactly two bindings: DLQ->DLX and primary->exchange.
    expect(plan.bindings).toEqual([
      {
        queue: "orders.order-placed.dlq",
        exchange: "orders.order-placed.dlx",
        routingKey: "",
      },
      {
        queue: "orders.order-placed",
        exchange: "example.orders.placed",
        routingKey: "",
      },
    ]);

    expect(plan.primaryQueue.queue).toBe("orders.order-placed");
  });

  it("applies the fanout-exclusive durability flags to the primary queue", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    const plan = planTopology(d, "orders.order-placed.abcd1234");

    expect(plan.primaryQueue).toMatchObject({
      queue: "orders.order-placed.abcd1234",
      durable: false,
      exclusive: true,
      autoDelete: true,
    });
  });

  it("uses the descriptor routing key for a topic binding", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      exchangeType: "topic",
      routingKeyBinding: "orders.*",
    };
    const plan = planTopology(d, "orders.order-placed");
    expect(plan.exchanges[0]).toMatchObject({ type: "topic" });
    expect(plan.bindings[1]).toEqual({
      queue: "orders.order-placed",
      exchange: "example.orders.placed",
      routingKey: "orders.*",
    });
  });

  it("stands up the retry-tier topology when tieredRetry is declared", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      tieredRetry: { maxAttempts: 3, tiersMs: [1000, 5000] },
    };
    const plan = planTopology(d, "orders.order-placed");

    // + return exchange + 2 tier exchanges (retry.0 and retry.1).
    expect(plan.exchanges.map((e) => e.exchange)).toEqual([
      "example.orders.placed",
      "orders.order-placed.dlx",
      "orders.order-placed.retry.return",
      "orders.order-placed.retry.0",
      "orders.order-placed.retry.1",
    ]);

    // + 2 tier queues = 4 total (dlq, primary, tier.0, tier.1).
    const tier0 = plan.queues.find(
      (q) => q.queue === "orders.order-placed.retry.0",
    );
    expect(tier0?.arguments).toEqual({
      "x-message-ttl": 1000,
      "x-dead-letter-exchange": "orders.order-placed.retry.return",
      "x-dead-letter-routing-key": "",
    });
    const tier1 = plan.queues.find(
      (q) => q.queue === "orders.order-placed.retry.1",
    );
    expect(tier1?.arguments).toMatchObject({ "x-message-ttl": 5000 });

    // The primary queue is bound back to the return exchange for the retry cycle.
    expect(plan.bindings).toContainEqual({
      queue: "orders.order-placed",
      exchange: "orders.order-placed.retry.return",
      routingKey: "",
    });
    expect(plan.bindings).toContainEqual({
      queue: "orders.order-placed.retry.0",
      exchange: "orders.order-placed.retry.0",
      routingKey: "",
    });
  });
});
