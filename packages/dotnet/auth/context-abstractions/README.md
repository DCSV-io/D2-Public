<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.AuthContext.Abstractions

This package contains the read-only `IAuthContext` interface, the typed contract that domain code reads for the
caller's identity, organization, scopes and impersonation context. `DcsvIo.D2.Context.SourceGen` generates the
interface from the auth-context contract spec. The package also contains the hand-written `IAuthContextExtensions`
helpers.

This package is the domain-safe slice: it references no DI, ASP.NET Core or Configuration package. The host supplies
the heavier runtime (HTTP middleware, JWT validation, populating the context). The public package
`DcsvIo.D2.Context.Abstractions` contains the request-context interfaces, `MutableRequestContext` and the propagation
codecs.

## Install

```bash
dotnet add package DcsvIo.D2.AuthContext.Abstractions
```

## Public API

| Type / member              | Contents                                                                                                                                                                                            |
| -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IAuthContext` (generated) | Generated interface - re-emitted on every build from the auth-context contract spec; do not hand-edit                                                                                               |
| `IAuthContextExtensions`   | Hand-written convenience helpers - `HasScope`, `HasAnyScope`, `HasAllScopes`, `IsStaff`, `IsAdmin`, `IsForcedImpersonation`, `IsConsentImpersonation`, `IsImpersonatorStaff`, `IsImpersonatorAdmin` |

## Spec -> emitted shape

The auth-context contract spec declares these sections:

- Token + Trust: `IsAuthenticated` (trinary), `Audience` (`IReadOnlyList<string>` per RFC 7519 section 4.1.3),
  `SessionId`, `TokenIssuedAt`, `TokenExpiresAt`, `ActorChain` (RFC 8693 flattened outermost-first)
- Identity: `Subject` (raw `sub`), `UserId` (`sub` parsed as Guid), `Username`, `RequestedByClientId` (RFC 8693
  section 4.3 / RFC 9068 - client that requested THIS token), `ImmediateCallerClientId` (derived - outermost Service in
  chain), `OriginatingClientId` (derived - most-deeply-nested Service in chain, fallback to Subject for pure
  service-identity tokens), `IsServiceIdentity` (derived)
- Organization: `OrgId`, `OrgName`, `OrgType`, `OrgRole`, `RootOrgId`, `AccessibleOrgIds`
- Impersonation: `IsImpersonating` (derived), `ImpersonationKind` (derived), `ImpersonatedBy` (derived),
  `ImpersonationSessionId` (derived), `ImpersonatorOrgId` / `ImpersonatorOrgName` / `ImpersonatorOrgType` /
  `ImpersonatorOrgRole` (derived)
- Scopes: `Scopes`

All D2-custom claim-mapped properties use `d2_`-prefixed claim names. Standard OAuth/OIDC claims (`sub`, `aud`, `iat`,
`exp`, `client_id`, `scope`, `act`) keep their canonical names.

### Which identity property to use

| Property                  | Source                                                                                          | When meaningful                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Subject`                 | `sub` claim (raw)                                                                               | Always (when authenticated). For user tokens: a Guid string. For service-identity tokens: the OAuth client_id of the calling service.                                                                                                                                                                                                                                                                                                                                                                                                      |
| `UserId`                  | `sub` claim parsed as Guid                                                                      | When the token represents a user. Null for pure service-identity tokens.                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `RequestedByClientId`     | `client_id` claim (RFC 8693 section 4.3)                                                        | The client that requested THIS specific token from the AS. Changes only when a token exchange occurs (RFC 8693 section 4.3). An ordinary internal hop forwards the token unchanged and does NOT exchange it, so this is the client that requested the token when it was minted at the system boundary, and it stays the same the whole way down. It changes only at a deliberate exception exchange (e.g. impersonation), where it becomes the client that triggered that exchange - never the originating client (`OriginatingClientId`). |
| `ImmediateCallerClientId` | Outermost Service entry in `ActorChain`                                                         | The service that immediately called this handler. Null when the user is calling directly with no service intermediary.                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `OriginatingClientId`     | Most-deeply-nested Service entry in `ActorChain`, fallback to `Subject` for pure service tokens | The primary audit identifier for end-to-end tracing across multi-hop sync and async chains. The first service that started this call chain.                                                                                                                                                                                                                                                                                                                                                                                                |

## Extension methods (hand-written)

```csharp
auth.HasScope("orders.order.cancel");
auth.HasAnyScope(Scopes.Self.Read, Scopes.Self.Write);
auth.HasAllScopes(Scopes.Self.Read, "orders.order.cancel");

auth.IsStaff();                  // OrgType is Admin or Support
auth.IsAdmin();                  // OrgType is Admin

auth.IsForcedImpersonation();    // ImpersonationKind == Force
auth.IsConsentImpersonation();   // ImpersonationKind == Consent

auth.IsImpersonatorStaff();      // ImpersonatorOrgType is Admin or Support
auth.IsImpersonatorAdmin();      // ImpersonatorOrgType is Admin
```

## Edge cases / gotchas

- `HasAllScopes()` with zero arguments returns `true` (vacuous truth - `[].All(...)` is `true`). Callers that pass a
  runtime-built array should guard the empty-input case if absence-of-scopes should be a denial.
- Scope comparison is case-sensitive per RFC 6749 section 3.3. The runtime `MutableRequestContext` uses
  `StringComparer.Ordinal` when building the set; consumers must pass exact-case scope names.
- The `IsImpersonator*` helpers return `false` when the caller is not impersonating: `ImpersonatorOrgType` is `null`
  outside an impersonation context, so `is OrgType.Admin or OrgType.Support` evaluates to `false`. Callers that want
  "this user is staff (regardless of impersonation)" should use `IsStaff()` instead.
- `IsForcedImpersonation()` and `IsConsentImpersonation()` are mutually exclusive, but neither implies
  `IsImpersonating`. When not impersonating, both return `false`. Combine with `IsImpersonating` if your branch logic
  needs both the kind and the presence of impersonation.

## Telemetry

This package emits no telemetry: it contains read-only abstractions and pure extension methods. Any auth-related span or
counter comes from the calling code (auth middleware, handlers, audit emitters).

## Dependencies

- `DcsvIo.D2.Auth.Abstractions` - `OrgType`, `Role`, `ActorKind`, `ImpersonationKind`, `ActorEntry`
- `DcsvIo.D2.Context.SourceGen` - emits `IAuthContext.g.cs` (analyzer-only)

Related packages: `DcsvIo.D2.Auth.Abstractions`, `DcsvIo.D2.Context.Abstractions`.
