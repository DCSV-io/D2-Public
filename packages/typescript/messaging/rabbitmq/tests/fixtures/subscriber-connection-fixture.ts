// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import { vi } from "vitest";
import type { D2Connection } from "../../src/connection/managed-connection.js";
import {
  SubscriberConsumerFixture,
  type ConsumerCallback,
} from "./subscriber-consumer-fixture.js";
import { SubscriberPublisherFixture } from "./subscriber-publisher-fixture.js";

const connections = new Map<string, SubscriberConnectionFixture>();
let sequence = 0;
let createHandle: ((url: string) => D2Connection) | undefined;

export function configureSubscriberHandleFactory(
  factory: (url: string) => D2Connection,
): void {
  createHandle = factory;
}

export class SubscriberConnectionFixture extends EventEmitter {
  readonly exchangeDeclare = vi.fn(async () => {});
  queueDeclare = vi.fn(async () => {});
  readonly queueBind = vi.fn(async () => {});
  readonly publisher = new SubscriberPublisherFixture();
  consumer!: SubscriberConsumerFixture;
  readonly handle: D2Connection;
  allocations = 0;
  closed = false;

  constructor() {
    super();
    const url = `amqp://localhost/fixture-${++sequence}`;
    connections.set(url, this);
    if (createHandle === undefined)
      throw new Error("Managed handle factory is not configured.");
    try {
      this.handle = createHandle(url);
    } catch (error) {
      connections.delete(url);
      throw error;
    }
  }

  get connectionCbs(): (() => void)[] {
    return this.listeners("connection") as (() => void)[];
  }
  createConsumer(
    props: Record<string, unknown>,
    callback: ConsumerCallback,
  ): SubscriberConsumerFixture {
    return (this.consumer = new SubscriberConsumerFixture(props, callback));
  }
  fireConnection(): void {
    this.emit("connection");
  }
  async close(): Promise<void> {
    this.closed = true;
  }
}

export function createVendorConnectionFixture(options: {
  url: string;
}): object {
  const fixture = connections.get(options.url);
  if (fixture === undefined)
    throw new Error("Connection fixture is not registered.");
  if (fixture.allocations++ === 0) return fixture;
  return Object.assign(new EventEmitter(), {
    acquire: async () => fixture.publisher,
    close: async () => {},
    unsafeDestroy: () => {
      fixture.publisher.active = false;
    },
  });
}

export async function closeSubscriberFixtures(): Promise<void> {
  try {
    await Promise.all(
      [...connections.values()].map((fixture) => fixture.handle.close()),
    );
  } finally {
    connections.clear();
  }
}

export function releaseClosedSubscriberFixture(
  fixture: SubscriberConnectionFixture,
): void {
  if (!fixture.closed) throw new Error("Fixture transport has not closed.");
  for (const [url, registered] of connections) {
    if (registered === fixture) connections.delete(url);
  }
}
