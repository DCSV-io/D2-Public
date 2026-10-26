<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Context.Abstractions

Single-lib home for every spec-driven context primitive. The request-context contract spec is the source of truth -
`DcsvIo.D2.Context.SourceGen` reads it at build time and emits its files into this assembly (re-emitted on every
`dotnet build`; do not hand-edit):

| Type                          | Kind          | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ----------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `IRequestContext`             | interface     | Read-only contract that domain code consumes (extends `IAuthContext`).                                                                                                                                                                                                                                                                                                                                                                                                               |
| `MutableRequestContext`       | sealed class  | Settable concrete class; per-scope DI registration; HTTP / messaging middleware populates it. Implements `IRequestContext`. Includes `FromClaims` + `FromJwtPayloadNoValidation` factories.                                                                                                                                                                                                                                                                                          |
| `PropagatedContext`           | sealed record | Cross-hop subset - every property the spec marks `propagate: true` (`RequestId`, `RequestPath`, `RequestStartedAt`, `IdempotencyKey`, `SessionFingerprint`, `CurrentFingerprint`, `RiskScore`, `DeviceKey`, `EdgeNodeId`, `LocaleIetfBcp47Tag`, `TimezoneIanaName`, `CurrencyIso4217Code`, `OrgPlanTier`, `FeatureFlagsCsv`, `WhoIsHashId`, `CallPath`). Identity (`UserId` / `OrgId` / `Scopes` / `ActorChain`) is never propagated - it is rebuilt from the JWT at every sync hop. |
| `PropagatedContextExtensions` | static class  | Projections in both directions: `IRequestContext.ToPropagatedContext()` (snapshot) and `MutableRequestContext.ApplyPropagatedContext(PropagatedContext?)` (apply).                                                                                                                                                                                                                                                                                                                   |
| `PropagatedContextSerializer` | static class  | Wire codec - base64url of JSON for the `x-d2-context` header (AMQP / gRPC / HTTP). `MAX_HEADER_LENGTH = 2048` is the global cap; per-field length validation is generated from each propagatable field's `maxLength` annotation in the spec. `TryDecode` returns null on any failure - propagation is opportunistic and never required.                                                                                                                                              |

## Install

```bash
dotnet add package DcsvIo.D2.Context.Abstractions
```

## Hand-written RFC helpers

This package also contains hand-written helpers that implement RFC rules. The spec does not describe JWT-claim parsing
semantics (the RFCs do), so these are hand-written code:

| Type                           | RFC                  | Purpose                                                                                                                                                                |
| ------------------------------ | -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ActorChainParser`             | RFC 8693 section 2.1 | Parses the `act` claim into `IReadOnlyList<ActorEntry>`, in strict mode with a depth limit. Used by `MutableRequestContext.FromClaims` / `FromJwtPayloadNoValidation`. |
| `ScopeClaimParser`             | RFC 6749 section 3.3 | Parses `scope` claim - SP-only string OR JSON array - into `IReadOnlySet<string>`.                                                                                     |
| `MalformedActorChainException` | -                    | Exception for actor-chain parse failures.                                                                                                                              |

## In-host establishment boundaries

This package also contains the in-host establishment boundaries and their shared call-path helper:

| Type                                                                      | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `InProcessModuleBoundary`                                                 | Extension method `IRequestContext.EstablishInProcessModule(callingModuleId, targetModuleId, IClock)` - the generated in-host module facade (the `I<Module>Api` leaf) calls this before dispatching into another module inside the same host. Sets `Origin = RequestOrigin.InProcessModule`, `ImmediateCaller` = the calling module's own id, and appends a `CallPathKind.ModuleHop` entry. Does nothing when the context is not a `MutableRequestContext` (e.g. a read-only test double).                                                                                                                                                                                 |
| `SystemRequestContextBootstrap`                                           | Extension method `IServiceProvider.EstablishSystemContext(hostServiceId, IClock)` - low-level bootstrap used by `ISystemWorkScopeFactory` (not for direct module use). Resolves the scope's `MutableRequestContext` (throws `InvalidOperationException` if the scope does not register one), sets `Origin = RequestOrigin.System`, `ImmediateCaller` = the host's own service id, and starts a fresh single-entry `CallPath` with a `CallPathKind.System` entry.                                                                                                                                                                                                          |
| `ISystemWorkScope` / `ISystemWorkScopeFactory` + `AddD2SystemWorkPlane()` | Platform System work plane - the only sanctioned entry for hosted or background work that carries authority. `BeginAsync` creates a DI scope, always calls `EstablishSystemContext` (host service id from `D2WorkloadIdentityOptions`), and returns a disposable scope with `Services`. Modules use the factory and never register `IRequestContext` / `MutableRequestContext` themselves. Hosts wire `AddD2SystemWorkPlane()` once at the composition root. HTTP and gRPC hosts replace the plain Mutable default with their own dual-path resolvers, so inbound requests still prefer `HttpContext.Items[REQUEST_CONTEXT]` while System workers use the scoped Mutable. |
| `CallPathOps`                                                             | Pure static helper `Append(existing, id, kind, timestamp) -> IReadOnlyList<CallPathEntry>` shared by every establishment boundary (in this lib and in host-supplied HTTP/gRPC transport bindings). Trims the oldest entries so the accumulated call path never exceeds `MAX_CALL_PATH_DEPTH` (16), which keeps the field bounded. Throws `ArgumentException` on a null, empty or whitespace `id`, because a missing self-identity is a misconfiguration and the entry is not silently dropped.                                                                                                                                                                            |

`Origin` / `ImmediateCaller` are never propagated (recomputed locally by every establishment boundary);
`CallPath` alone is propagated here (`propagate: true`, depth-bounded) - see
`DcsvIo.D2.Auth.Abstractions` for the full local-fact-vs-propagated-telemetry model.

## Spec annotations driving the codegen

These annotations on each property in the spec control the propagated subset:

- `propagate: true | false` (default false) - whether the property flows across hops in `x-d2-context`.
- `maxLength: <int>` (optional) - wire-level per-field length cap; the codegen-emitted `TryDecode` rejects oversized
  values.

Identity fields (UserId / OrgId / Scopes / ActorChain) MUST NOT be marked `propagate: true`. Those rebuild from the JWT
at every sync hop; for async events the consumer-side handler has no JWT and should not assume caller identity.

## Cross-language use

The wire format (base64url of canonical JSON) is language-neutral; per-field caps come from the same JSON spec; the
projection extensions are mechanical given the field set. Any language consumer that mirrors the spec produces the same
wire behavior. One JSON spec yields one abstractions library per language.

## Spec -> IRequestContext shape

The spec declares these sections (the WhoIs data is split into four sub-sections):

- Tracing: `TraceId`, `RequestId`, `RequestPath`, `HttpMethod`, `RequestStartedAt`, `IdempotencyKey`
- Network: `ClientIp`
- Fingerprints: `SessionFingerprint`, `CurrentFingerprint`, `RiskScore`, `DeviceKey`, `DeviceConfidence`
- Infrastructure: `EdgeNodeId`
- User Preferences: `LocaleIetfBcp47Tag`, `TimezoneIanaName`, `CurrencyIso4217Code`
- Entitlements: `OrgPlanTier`, `FeatureFlagsCsv`
- WhoIs - Admin Location: `WhoIsHashId`, `AdminLocationHashId`, `City`, `SubdivisionIso31662Code`,
  `CountryIso31661Alpha2Code`, `PostalCode`
- WhoIs - Coordinates: `Latitude`, `Longitude`, `Geohash`
- WhoIs - Network Privacy: `IsVpn`, `IsProxy`, `IsTor`, `IsHosting`
- WhoIs - ASN: `Asn`, `AsnName`, `AsnType`
- Establishment: `Origin`, `ImmediateCaller`, `CallPath`

Plus everything from `IAuthContext` (token / identity / organization / impersonation / scopes).

## Dependencies

- `DcsvIo.D2.AuthContext.Abstractions` - `IAuthContext` base interface + `IAuthContextExtensions`.
- `DcsvIo.D2.Auth.Abstractions` - `ActorEntry`, enums (`ActorKind`, `ImpersonationKind`, `OrgType`, `Role`,
  `RequestOrigin`, `CallPathKind`), and `CallPathEntry` - the establishment vocabulary `InProcessModuleBoundary` /
  `SystemRequestContextBootstrap` / `CallPathOps` operate on.
- `DcsvIo.D2.Utilities` - `Falsey()` / `Truthy()` / `TryParseTruthyNull` extensions used by parsers; `ThrowIfFalsey()`
  guards on the establishment boundaries.
- `DcsvIo.D2.Time` - `IClock` injection seam the `InProcessModuleBoundary` + `SystemRequestContextBootstrap`
  establishment boundaries use to timestamp the call-path entry they append.
- `DcsvIo.D2.Context.SourceGen` - analyzer-only.

Related packages: `DcsvIo.D2.AuthContext.Abstractions`, `DcsvIo.D2.Auth.Abstractions`.
