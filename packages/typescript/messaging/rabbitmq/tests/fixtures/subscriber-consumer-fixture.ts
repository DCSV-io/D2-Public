// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import type { AsyncMessage, ConsumerStatus } from "rabbitmq-client";

export type ConsumerCallback = (
  msg: AsyncMessage,
) => Promise<ConsumerStatus | void> | ConsumerStatus | void;

export class SubscriberConsumerFixture extends EventEmitter {
  started = false;
  closed = false;
  constructor(
    readonly props: Record<string, unknown>,
    readonly handlerCb: ConsumerCallback,
  ) {
    super();
  }
  start(): void {
    this.started = true;
  }
  close(): Promise<void> {
    this.closed = true;
    return Promise.resolve();
  }
}
