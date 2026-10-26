// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { findPackWorkspaceFixture } from "./packed-package-fixture.js";
import type { DlqRepublishOutcome } from "../../src/subscribing/dlq-republisher.js";

interface DlqLifetimeContractFixture {
  defaults: { republishMs: number; cleanupMs: number };
  outcomes: {
    first: DlqRepublishOutcome;
    later: DlqRepublishOutcome;
    expected: DlqRepublishOutcome;
  }[];
  deliveries: {
    name: string;
    bodyHex: string;
    messageId: string | null;
    contentType: string;
    contentEncoding: string | null;
    correlationId: string;
    header: string;
  }[];
}

const workspace = findPackWorkspaceFixture();
const candidates = [
  join(workspace, "public/contracts/messaging/fixtures/dlq-lifetime.json"),
  join(workspace, "contracts/messaging/fixtures/dlq-lifetime.json"),
];
const path = candidates.find((candidate) => existsSync(candidate));
if (path === undefined)
  throw new Error("Shared DLQ lifetime fixture was not found.");
export const dlqLifetimeContractFixture = JSON.parse(
  readFileSync(path, "utf8"),
) as DlqLifetimeContractFixture;
