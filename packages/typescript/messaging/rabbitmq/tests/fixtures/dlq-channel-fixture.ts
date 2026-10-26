// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import { vi } from "vitest";

export class DlqChannelFixture extends EventEmitter {
  active = true;
  confirmSelect = vi.fn(async () => {});
  basicPublish = vi.fn(async () => {});
  close = vi.fn(async () => {
    this.active = false;
  });
}
