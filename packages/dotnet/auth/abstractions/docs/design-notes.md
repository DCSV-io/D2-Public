<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Abstractions - design notes

Why the claim shapes in [`DcsvIo.D2.Auth.Abstractions`](../README.md) are what they are: the `d2_` prefix, the
recursive `act` chain and the obligation it places on a token minter, strict-mode parsing of that chain, and why
every constant catalog is emitted from a spec.

## Why `d2_` prefix on custom claims

Custom JWT claims are namespaced with the `d2_` prefix to avoid collisions with claims that a spec defines. Standard
OAuth / OIDC claims (`sub`, `aud`, `iat`, `exp`, `azp`, `scope`, `act`) keep their canonical names because they are
spec-defined.

## Why `act` chain is recursive

RFC 8693 section 2.1 defines the actor claim recursively: `{ "sub": "...", "act": { "sub": "...", "act": { ... } } }`.
Each link represents one delegation / impersonation step. Flat-list modeling would lose audit fidelity for multi-hop
scenarios (e.g. an inbound boundary forwards an impersonation token through one downstream service onto another).

Per RFC 8693 section 4.1: **the outermost `act` claim represents the current actor; the least recent actor is the most
deeply nested.** The `ActorChainParser` flattens to an `IReadOnlyList<ActorEntry>` ordered outermost-first, so
`chain[0]` is the immediate caller and `chain[chain.Count - 1]` is the originator (the first service that started the
call chain).

## Hard requirement on the auth runtime: preserve nested `act` on every token exchange

RFC 8693 section 4.1 leaves it to the AS's discretion whether to preserve nested `act` history when issuing an exchanged
token. An AS may drop nesting and pass only the immediate caller forward.

This requirement is about what happens *when* a token exchange occurs - not about exchanging on every hop. Under the
forward-unchanged service-to-service model, an ordinary internal hop **forwards the boundary-minted token byte-for-byte
and does not exchange**, so its `act` chain is the one set at the boundary mint and is left untouched the whole way
down. The chain is established (or extended) only where an exchange genuinely happens: the single boundary mint, and the
deliberate exception cases that use token exchange - most relevantly **impersonation**, which adds an actor entry. (The
chain therefore does not grow link-by-link as a request descends the call graph; it is set once at the mint and
forwarded.)

**A host-supplied auth runtime that mints or exchanges a token carrying an actor preserves the
nested `act` history.** Specifically: when an exchange takes a token with `act = { sub: B, act: { sub: C } }` and
adds a new actor A, the resulting token's `act` must be `{ sub: A, act: { sub: B, act: { sub: C } } }` - A is added
as the new immediate actor and the prior chain is preserved unchanged. Dropping nesting would make
`IAuthContext.OriginatingClientId` unrecoverable beyond the most recent actor and break audit traceability across
multi-actor chains.

The exchange-helper API on the auth runtime accepts an optional `prior_actor_chain` parameter for the callers that
forward an existing chain - used only on the exchange path itself (the boundary mint and the exception cases),
never on an ordinary forward-unchanged hop. (Async consumers do not propagate identity claims via the wire - identity
rebuilds from the JWT each sync hop, and the asynchronous path carries only the encrypted operational subset.)

The depth limit lives in `DcsvIo.D2.Context.Abstractions.ActorChainParser.MaxActDepth` (currently 20). The auth runtime
enforces the same limit at mint time so issued tokens never exceed what consumers can parse - a DoS guard on the chain
structure, independent of where the chain is established.

## Strict-mode parsing of the `act` chain

`DcsvIo.D2.Context.Abstractions.ActorChainParser` rejects malformed actor chains by throwing
`MalformedActorChainException`:

- Any entry missing `sub` (RFC 8693 section 2.1 violation)
- Any impersonation entry missing `d2_kind` / `d2_session_id` / `d2_org_id` / `d2_org_type` / `d2_org_role`
- Depth exceeds `MaxActDepth` (DoS protection)
- Invalid JSON or non-object root

Auth middleware catches it and converts to `D2Result.Unauthorized` (HTTP 401) - a malformed actor chain is a
signed-token-with-bad-payload condition that should never reach a handler.

## Why every constant catalog is codegen-emitted

The scope catalog, audience catalog, protocol audience catalog, JWT claim names, HTTP header names, and in-process slot
keys are all emitted by codegen from per-topic specs in `contracts/`. Each spec is the single definition that drives
both the .NET catalog and the corresponding TS catalog (where applicable), so the .NET and TS catalogs cannot differ.

- `Scopes.g.cs` <- `contracts/auth-scopes/scopes.spec.json` via `DcsvIo.D2.Auth.Scopes.SourceGen`
- `Audiences.g.cs` <- `contracts/auth-audiences/audiences.spec.json` via `DcsvIo.D2.Auth.Audiences.SourceGen`
- `JwtClaimTypes.g.cs` <- `contracts/jwt-claims/jwt-claims.spec.json` via `DcsvIo.D2.Auth.JwtClaims.SourceGen`
- `WellKnownAudiences.g.cs` <- `contracts/auth-protocol-audiences/protocol-audiences.spec.json` via
  `DcsvIo.D2.Auth.ProtocolAudiences.SourceGen`
- `Http/D2HttpContextItems.g.cs` <- `contracts/in-process-keys/keys.spec.json` via `DcsvIo.D2.InProcessKeys.SourceGen`
- HTTP / AMQP / gRPC / cross-transport header catalogs <- `contracts/headers/headers.spec.json` via
  `DcsvIo.D2.Headers.SourceGen` (live in their own per-transport csprojs `DcsvIo.D2.Headers.{Http,Amqp,Grpc,Common}`)

---
