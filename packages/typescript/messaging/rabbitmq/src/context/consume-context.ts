// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  type IPropagatedContext,
  PropagatedContextSerializer,
} from "@dcsv-io/d2-request-context-abstractions";

/**
 * Mutable per-message context the consumer builds fresh for every delivery.
 * Its shape is exactly {@link IPropagatedContext}: the propagated operational
 * subset (request id / path / fingerprints / WhoIs hash / locale-tier fields /
 * `callPath`). It structurally has no identity slots (UserId / OrgId / Scopes)
 * and no `RequestOrigin` slot: those are not fields of `IPropagatedContext`, so
 * the wire can never populate them here (origin/identity are never
 * reconstructed from a propagated wire value; identity rebuilds from a JWT on a
 * sync hop, and an async consumer must not claim caller identity).
 */
export type MutablePropagatedContext = {
  -readonly [K in keyof IPropagatedContext]?: IPropagatedContext[K];
};

/**
 * The established per-message consume context passed to a message handler. It
 * contains only the propagated operational subset (including the telemetry-only
 * `callPath` hop-trace, which is never an authority input).
 */
export interface ConsumeContext {
  readonly propagated: IPropagatedContext;
}

/**
 * Applies the decoded propagated subset onto a fresh per-message context. This
 * is the TS mirror of the .NET `MutableRequestContext.ApplyPropagatedContext` /
 * generated `PropagatedContextExtensions` projection. Copies the full
 * operational subset (all `IPropagatedContext` fields, including `callPath`);
 * because the source type is `IPropagatedContext` it structurally cannot contain
 * identity or origin, so neither can be applied.
 *
 * @param target The fresh per-message context to populate.
 * @param source The decoded propagated subset.
 */
export function applyPropagatedContext(
  target: MutablePropagatedContext,
  source: IPropagatedContext,
): void {
  Object.assign(target, source);
}

/**
 * Establishes a fresh per-message {@link ConsumeContext} from the raw
 * `x-d2-context` header value. Decodes via the shared, cap-enforcing
 * `PropagatedContextSerializer.tryDecode`; an absent, undecodable or
 * cap-exceeding header yields an empty context (never a reject: propagation is
 * opportunistic, matching the .NET TryDecode posture). A crafted header that
 * adds extra keys (a forged `userId` / `origin`) is ignored because `tryDecode`
 * reads only its closed field set, so those keys can never reach the
 * established context.
 *
 * The wire value is base64url-encoded JSON (the header spec and the .NET
 * `PropagatedContextSerializer.Encode`); the base64url wrapping is decoded here
 * before delegating to `tryDecode` (which parses the inner JSON and enforces
 * the per-field caps). `Buffer.from(x, "base64url")` never
 * throws on garbage input; `tryDecode` then rejects any non-JSON payload.
 *
 * @param rawContextHeader The raw `x-d2-context` header value (or undefined).
 */
export function establishConsumeContext(
  rawContextHeader: string | undefined,
): ConsumeContext {
  const decoded = decodePropagated(rawContextHeader);
  const fresh: MutablePropagatedContext = {};
  if (decoded !== undefined) applyPropagatedContext(fresh, decoded);

  return { propagated: fresh as IPropagatedContext };
}

function decodePropagated(
  rawContextHeader: string | undefined,
): IPropagatedContext | undefined {
  if (
    rawContextHeader === undefined ||
    rawContextHeader.length === 0 ||
    /[\r\n]/.test(rawContextHeader)
  ) {
    return undefined;
  }

  const json = Buffer.from(rawContextHeader, "base64url").toString("utf8");
  return PropagatedContextSerializer.tryDecode(json);
}
