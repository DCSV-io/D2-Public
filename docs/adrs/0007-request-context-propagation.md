<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0007: Request/auth context - spec-driven `IRequestContext`/`IAuthContext`, rebuild-from-JWT each hop, `x-d2-context` propagation

- **Status**: Accepted
- **Date**: 2026-05-30

## Context

Every handler, repository, and middleware needs answers to these questions about the in-flight request: *who is acting*
(identity, impersonation, scopes), *what transport delivered it* (tracing, fingerprints, geo, entitlements), and *what
did the ingress observe that downstream services cannot recompute* (risk score, session fingerprint, idempotency key).
These constraints shaped the design:

- D2 spans two runtimes - .NET services, the public ingress among them, and a TypeScript server-side consumer. Context definitions must be authoritative in one place and derivable in both runtimes without hand-maintaining parallel types.
- Services communicate over HTTP, gRPC, and AMQP. Context encoding must be transport-agnostic.
- Propagating full identity across hops via headers would put plaintext `UserId`/`OrgId`/`Scopes` in RabbitMQ header
  frames at-rest - the broker would carry credential material it cannot interpret. The threat model does not accept
  this.
- Domain-layer code must read caller identity without pulling in ASP.NET Core or DI, so the domain assembly stays testable in isolation.
- The RFC 8693 section 2.1 `act` claim is a recursive JSON object; impersonation introduces additional required sub-claims. The parsing rules are specific enough that silent degraded-mode fallbacks would mask upstream mint bugs.

The spec-driven codegen decision (ADR-0002) and the abstractions/implementation split (ADR-0006) both apply directly here.

## Decision

These interlocking choices are read as a unit.

### 1. Context interfaces are spec-driven and codegen-emitted

`contracts/auth-context/IAuthContext.spec.json` and `contracts/request-context/IRequestContext.spec.json` are the single source of truth for every property: name, type, JWT claim name, propagation flag (`propagate: true/false`), per-field wire-length cap (`maxLength`), PII redaction annotation (`redact: true`), and doc comment.

The Roslyn generator `DcsvIo.D2.Context.SourceGen` reads both specs via `AdditionalFiles` and dispatches by consuming
assembly: into `DcsvIo.D2.AuthContext.Abstractions` it emits `IAuthContext.g.cs`; into `DcsvIo.D2.Context.Abstractions`
it emits `IRequestContext.g.cs` (which extends `IAuthContext` per the spec), `MutableRequestContext.g.cs` (the settable
concrete, with `FromClaims` / `FromJwtPayloadNoValidation` factories), `PropagatedContext.g.cs`,
`PropagatedContextExtensions.g.cs`, and `PropagatedContextSerializer.g.cs`. Build-time diagnostics (`D2CTX*`) report
spec violations at build (most as errors; `D2CTX005` is a warning).

The wire encoding used by `PropagatedContextSerializer` - canonical JSON (camelCase, omit-null, no whitespace) ->
UTF-8 -> base64url - is language-neutral; a TypeScript server-side consumer reads the same field names and
`propagate`/`maxLength`/`redact` annotations from the same specs. The parsing helpers are hand-written (stable RFC text,
not spec-variable behavior): `ActorChainParser` (RFC 8693 section 2.1, strict mode, depth limit, throws
`MalformedActorChainException` on any structural failure) and `ScopeClaimParser` (RFC 6749 section 3.3, space-separator
grammar, defensive JSON-array path).

### 2. Full identity rebuilds from the JWT at every hop; only a small operational subset propagates

Every sync hop (HTTP, gRPC) carries a bearer JWT. The receiving service's auth middleware validates it (signature, expiry, audience, issuer) and calls `MutableRequestContext.FromClaims` to populate all identity fields - `UserId`, `OrgId`, `OrgType`, `OrgRole`, `Scopes`, `ActorChain`, `SessionId`, token timestamps, `AuthMethod`, etc. - fresh from the JWT. **None of these appears in `PropagatedContext`.**

No hop checks session liveness. The ingress mints the bearer JWT once per request after it has resolved the session, so
a later hop could only catch a revocation that landed during that one request. The ingress that resolves the session
checks revocation against its session store and fails closed when the store cannot respond. A downstream hop honors a
revoked session's token for at most the token's lifetime, and still validates the token's signature, expiry, audience
and issuer.

`PropagatedContext` contains only the fields whose `propagate: true` annotation appears in the request-context spec -
operational data a downstream service cannot recompute (`RequestId`, `RequestPath`, `RequestStartedAt`,
`IdempotencyKey`, session/current fingerprints, `RiskScore`, `EdgeNodeId`, locale/timezone/currency, `OrgPlanTier`,
feature flags, `WhoIsHashId`) - none of which is bearer identity. `PropagatedContextSerializer` enforces a global header
cap plus per-field caps derived from the spec's `maxLength`; `TryDecode` returns `null` on any failure path -
propagation is opportunistic, never required. The header name is `x-d2-context` on all transports. AMQP consumers
receive no JWT and do not reconstruct identity - they decode only the operational subset.

### 3. The domain-safe `IAuthContext` slice

`IAuthContext` (namespace `DcsvIo.D2.AuthContext.Abstractions`) is a read-only identity/org/impersonation/scopes interface with no dependency on ASP.NET Core or `HttpContext`. Domain assemblies reference only `DcsvIo.D2.AuthContext.Abstractions` + `DcsvIo.D2.Auth.Abstractions` (vocabulary: `ActorEntry`, `OrgType`, `Role`, `ImpersonationKind`). `IRequestContext` extends `IAuthContext` with transport fields; only transport middleware and integration code reference `DcsvIo.D2.Context.Abstractions`. Hand-written `IAuthContextExtensions` (`HasScope`, `IsStaff`, `IsForcedImpersonation`, ...) provide the domain predicate surface - stable domain logic, not spec-variable, so not generated. `ActorChainParser` strict mode deliberately throws on any malformed `act` claim (auth middleware catches it -> 401) rather than returning a degraded result: a signed token with a malformed actor chain is a broken upstream mint condition.

### 4. Establishment fields - `Origin` / `ImmediateCaller` / `CallPath`

`IRequestContext` has the spec section `Establishment`, which contains the fields every trust boundary populates fresh
on its own request context: `Origin` (`RequestOrigin`, non-nullable - which kind of boundary produced this hop's
context: external ingress, a cross-process mTLS-authenticated hop, an in-process module call, or an in-host system
worker), `ImmediateCaller` (`string?` - who called this hop, sourced from the validated mTLS client certificate on a
cross-process hop or the calling module's own id in-process), and `CallPath` (`IReadOnlyList<CallPathEntry>`,
`propagate: true`, depth-bounded - the accumulated sequence of hops the request has traversed).

`Origin` and `ImmediateCaller` are deliberately **not** `propagate: true` - the generator's `propagate`-gated filter
(the same mechanism that already keeps full identity out of `PropagatedContext`, described in section 2) structurally
excludes both from ever reaching the wire; every boundary recomputes them locally instead of trusting an inbound value.
`CallPath` is the opposite shape: it is a `propagate: true` list-of-records field, and it is operational telemetry
only - no authority decision anywhere reads it. This is the same rebuild-fresh-per-hop discipline this ADR already
applies to identity, extended to a second, narrower class of fact (which kind of boundary, not which user) that a
capability authority needs and that must be at least as unforgeable as identity itself.

## Consequences

**Positive.**

- A one-line spec change updates the .NET interface, the mutable concrete + JWT factories, the propagated subset, the wire-length validator, and (via the same spec consumed by TS codegen) the TS redact paths. Cross-language drift is a build error, not a review artifact.
- Full identity is never in headers at-rest or in-flight except as a signed JWT blob. Brokers, gRPC proxies, and AMQP storage contain no credential material beyond the `x-d2-context` operational subset.
- Domain code has no transitive ASP.NET Core dependency; handlers take `IAuthContext`/`IRequestContext` and unit-test against a plain `MutableRequestContext`.
- Strict actor-chain parsing reports malformed `act` claims as 401 at the validation boundary, not as silent nulls deep
  in a handler; impersonation audit accuracy is enforced structurally.
- `TryDecode` returns null on failure and `ApplyPropagatedContext` is a no-op on null - the system degrades without
  failing when the header is absent or corrupted.

**Negative / risks.**

- Adding a `propagate: true` field is a cross-language coordination event (the .NET serializer and TS decoder must both
  rebuild). In one monorepo this is a single CI run, but it still requires coordination.
- Every inbound sync hop re-validates the bearer JWT (RS256 signature against the **cached** JWKS key + claims) rather
  than trusting a pre-authenticated identity asserted by the transport. That per-hop CPU cost is overhead a
  **transport-trust shortcut** would avoid - internal services mutually authenticated by mTLS, passing a plaintext
  identity header that downstream accepts *in place of* re-validating the token. We deliberately do not rely on mTLS
  that way: using the channel as a reason to skip token re-validation moves the trust anchor from the token (bound to
  the issuer's signature at every hop) to the network, so one compromised or misconfigured internal service could forge
  any identity. The threat model accepts the (cache-warm) re-validation cost to keep every hop zero-trust. **mTLS itself
  is adopted** - as an *additive* workload-identity and channel layer that sits alongside per-hop token re-validation
  and never replaces it; what is declined is using mTLS to *skip* that validation. (Identity-via-JWT and the operational
  subset are both still *propagated* - what we decline is *trusting* a propagated identity without re-verifying its
  token.)
- The closed type vocabulary (enforced by codegen diagnostics) means adding a new property type touches the type vocabulary + emitters in lockstep; a propagated `IReadOnlySet<string>` is currently blocked (hence `FeatureFlagsCsv` is a CSV string).
- Strict parsing throws on malformed tokens: if upstream token exchange introduces a new `act` structure without a coordinated `ActorChainParser` update, valid tokens are rejected 401 - intended fail-closed behavior, but a deployment-sequencing constraint.

## Alternatives considered

**Transport-trust shortcut (use mTLS to skip per-hop validation - trust a plaintext identity header *because* the
channel is mTLS-authenticated).** Pass `UserId`/`OrgId`/`Scopes` in a header that internal services accept *in place of*
re-validating the token, because of the mTLS channel alone. This is the *only* variant that actually avoids the
validation cost - but it anchors trust in the network rather than the token, so any compromised or misconfigured
internal service can forge identity, and the identity material is in plaintext headers at-rest (RabbitMQ frame stores,
log shippers, broker management APIs). (Propagating identity in a header that is *still* integrity-protected - HMAC or
nested JWT - saves nothing: you re-validate that instead of the bearer.) Rejected: identity stays cryptographically
bound to the issuer's signature at every trust boundary. **This rejects the *role*, not the technology** - mTLS *is*
adopted, as an additive workload-identity and channel layer that authenticates the calling workload and protects the
wire *alongside* per-hop token re-validation; what is declined is relying on it to skip that validation. The
service-auth model that builds on this re-validate-every-hop decision is: mint one internal transaction-token at the
edge boundary, forward it unchanged, and re-validate at every hop.

**Hand-written context types per language.** Define the C# interfaces and a parallel TS interface, kept in sync by
review. Rejected: independent pull requests cause divergence within weeks - the exact problem the spec-driven codegen
decision (ADR-0002) exists to solve.

**Thread identity through method parameters.** Pass `UserId`/`OrgId`/`Scopes` explicitly through every call. Rejected:
does not compose with cross-cutting concerns (audit, logging, scope checks) without parameter-object proliferation or an
ad-hoc ambient-context reinvention. A scoped `IAuthContext` DI service achieves the ambient effect with explicit
testability, and ADR-0006 keeps the interface free of DI dependencies.

## References

- `contracts/auth-context/IAuthContext.spec.json`, `contracts/request-context/IRequestContext.spec.json` - the source-of-truth specs (incl. `propagate`/`maxLength`/`redact`).
- `packages/dotnet/context/source-gen/ContextGenerator.cs` - the Roslyn generator (assembly dispatch, spec loading, emitter orchestration).
- `packages/dotnet/context/abstractions/Generated/` - emitted `IRequestContext`, `MutableRequestContext` (`FromClaims` / `FromJwtPayloadNoValidation`), `PropagatedContext`, `PropagatedContextExtensions`, `PropagatedContextSerializer` (global + per-field length caps).
- `packages/dotnet/auth/context-abstractions/Generated/` (emitted `IAuthContext`) + hand-written `IAuthContextExtensions.cs`.
- `packages/dotnet/context/abstractions/ActorChainParser.cs` (RFC 8693 section 2.1 strict), `ScopeClaimParser.cs` (RFC 6749 section 3.3).
- RFC 8693 section 2.1/section 4.1, RFC 6749 section 3.3, RFC 7519 section 4.1.3.
- [ADR-0002](0002-spec-driven-codegen.md) - the codegen pattern this applies. [ADR-0006](0006-abstractions-implementation-split.md) - the domain-safe abstractions slice.
- Service-to-service auth model that builds on this decision's rebuild-from-JWT-each-hop choice: one token minted at the edge boundary and forwarded unchanged, re-validated at every hop.
