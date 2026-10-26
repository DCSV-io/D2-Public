// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";

export class SubscriberPublisherFixture extends EventEmitter {
  active = true;
  closed = false;
  readonly sent: unknown[] = [];
  sendImpl: () => Promise<void> = () => Promise.resolve();
  confirmSelect(): Promise<void> {
    return Promise.resolve();
  }
  basicPublish(envelope: unknown): Promise<void> {
    this.sent.push(envelope);
    return this.sendImpl();
  }
  close(): Promise<void> {
    this.closed = true;
    this.active = false;
    return Promise.resolve();
  }
}
