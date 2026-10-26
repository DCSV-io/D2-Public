<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Abstractions - tests

For a contributor changing [`DcsvIo.D2.Auth.Abstractions`](../README.md): which test files pin each part of the
package's surface, and the command that runs them.

## Test files

`packages/dotnet/tests/Unit/Auth/`:

- `ActorKindTests.cs` / `ImpersonationKindTests.cs` / `OrgTypeTests.cs` / `RoleTests.cs` / `ActionSensitivityTests.cs` -
  enum value stability (rename = breaking change gate).
- `RoleAuthorityTests.cs` - the total order: per-role rank, irreflexivity, comparability of every distinct pair in
  exactly one direction, the throw from `RankOf` on a value outside the enumeration, and the false that `Outranks` and
  `TryRankOf` return on the same value.
- `ActorEntryTests.cs` - record equality including nested `Act` chains; null-Act default; multi-level chain traversal;
  ImpersonationKind / SessionId only-meaningful-when-Impersonation discipline; `ToString` prints `SessionId` as
  `[REDACTED: SecretInformation]`, in a nested actor too, and prints every other member by name.
- `JwtClaimTypesTests.cs` - per-value pin against the codegen-emitted catalog; D2-prefix validation.
- `WellKnownAudiencesTests.cs` - value pins on `D2_INTERNAL_AUDIENCE` and `D2_EDGE_SELF_AUDIENCE`; the check that
  `D2_INTERNAL_AUDIENCE` is absent from every `Audiences` projection (the token-exchange target catalog); an
  adversarial no-duplicate-constant walk; and parity of the generated constants with
  `contracts/auth-protocol-audiences/protocol-audiences.spec.json` and with the TypeScript `protocol-audiences.g.ts`.
- HTTP / AMQP / gRPC / common header per-value pins live in `tests/Unit/Headers/HeaderCatalogPinTests.cs`.
- `tests/Unit/SpecsConsistency/JwtClaimsVsIAuthContextConsistencyTests.cs` - every `claim:` annotation in
  `IAuthContext.spec.json` references a valid jwt-claims spec entry (forward direction; reverse intentionally not
  enforced - `ACT_KIND` / `IAT` / `EXP` etc. live outside top-level IAuthContext properties).

`packages/dotnet/tests/Unit/Auth/Jwks/`:

- `JwksKeySetSnapshotTests.cs` - defensive-copy guarantee on the `Keys` init setter, per-property record equality,
  empty-key-set tolerance.
- `IJwksProviderContractTests.cs` - reflection-based shape pinning (return type, parameter count, ct default, public +
  interface, exact method count).

`packages/dotnet/tests/Unit/Logging/Internal/ForwardedJwtEnrichmentExclusionTests.cs` - structural-isolation pin: no
`ForwardedJwt`-typed / credential-named property on `IRequestContext` / `IAuthContext`, and the request-context enricher
records no credential field/bytes even with a populated holder in scope.

`packages/dotnet/tests/Unit/Logging/Destructuring/ActorEntrySessionIdRedactionTests.cs` - `ActorEntry.SessionId` is
masked as `[REDACTED: SecretInformation]` under `{@x}` destructuring by the real policy.

Run: `dotnet test packages/dotnet/tests`.
