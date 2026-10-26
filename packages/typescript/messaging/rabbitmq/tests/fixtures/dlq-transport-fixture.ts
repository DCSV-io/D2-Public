// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import type { Channel, Connection } from "rabbitmq-client";
import { vi } from "vitest";
import { createCompletion } from "../../src/connection/managed-connection.js";
import { DlqChannelFixture } from "./dlq-channel-fixture.js";

export class DlqTransportFixture extends EventEmitter {
  readonly channel = new DlqChannelFixture();
  readonly acquired = createCompletion<void>();
  readonly pending = createCompletion<Channel>();
  readonly acquire = vi.fn(() => {
    this.acquired.resolve();
    return this.pending.promise;
  });
  readonly close = vi.fn(async () => {});
  readonly unsafeDestroy = vi.fn(() => {});

  connection(): Connection {
    return this as unknown as Connection;
  }

  completeAcquisition(): void {
    this.pending.resolve(this.channel as unknown as Channel);
  }
}
