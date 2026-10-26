// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import { vi } from "vitest";

export const vendor = {
  connections: [] as ConnectionFixture[],
  options: [] as unknown[],
  construct: vi.fn(),
  close: vi.fn(async () => {}),
  publisherClose: vi.fn(async () => {}),
  createPublisher: vi.fn(),
};

export class ConnectionFixture extends EventEmitter {
  constructor(options: unknown) {
    super();
    vendor.construct();
    vendor.options.push(options);
    vendor.connections.push(this);
  }
  close = vendor.close;
  createPublisher = vendor.createPublisher;
}
