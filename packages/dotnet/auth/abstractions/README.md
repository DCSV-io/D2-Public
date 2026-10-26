<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Abstractions

Identity / authorization vocabulary and consumer-side runtime contracts: the value types, string constants and
read-only interfaces that every consumer references when it works with auth. Domain layers, request-context,
handler/abstractions, host-supplied JWT runtime, and issuer-side code all reference this slice; impls live in
host-supplied runtime libs.

The runtime piece - the host's own JWT-auth registration, JWT validation and JWKS HTTP fetcher - is **host-supplied**,
and those implementations satisfy the `IJwksProvider` contract defined here. HTTP-transport binding (middleware +
ProblemDetails) and gRPC-transport binding (server-side interceptor + RpcException trailers) are likewise host-supplied.
Domain code never references any of the runtime libs - only this abstractions slice.

## Install

```bash
dotnet add package DcsvIo.D2.Auth.Abstractions
```

## Types (source map)

| Path                                                                                               | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ActorKind.cs`                                                                                     | Enum - `Service` / `Impersonation`. The kinds an entry in the RFC 8693 act chain can be. Token "kind" is derived from the act chain's shape (no act -> end-user; Service entry -> delegation; Impersonation entry -> impersonation).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `ImpersonationKind.cs`                                                                             | Enum - `Consent` / `Force`. Sub-discriminator for `ActorKind.Impersonation` actors: Consent = authorized by the impersonated user with a one-time code; Force = no consent, not surfaced to the impersonated user. The deploying product's scope catalog decides who may start each.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `OrgType.cs`                                                                                       | Enum - `Admin` / `Support` / `Customer` / `ThirdParty` / `Affiliate`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `Role.cs`                                                                                          | Enum - `Auditor` / `Agent` / `Officer` / `Owner`. Capability is per (role, org type); authority over another member is the total order in `RoleAuthority.cs`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `RoleAuthority.cs`                                                                                 | Static class - `RankOf(Role)`, `TryRankOf(Role, out int)` and `Outranks(Role, Role)`. The total order over `Role` for authority over another member, declared once so no call site re-derives it. Not a capability ordering: an Auditor reads more than an Agent and writes nothing.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `ActionSensitivity.cs`                                                                             | Enum - `Routine` / `Sensitive` / `Critical`. How dangerous an action is if it succeeds, which drives audit verbosity, step-up triggers and alerting thresholds. Declared on the operation and recorded as endpoint metadata; a scope entry declares no sensitivity of its own.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `ScopeMatch.cs`                                                                                    | Enum - `Any` / `All`. How an endpoint's declared scope set is compared with the caller's `IRequestContext.Scopes`: `Any` admits a caller holding at least one declared scope, `All` only a caller holding every declared scope. Stored beside the scope set in HTTP endpoint and gRPC method metadata; the host-supplied HTTP middleware and gRPC interceptor read it when they check scopes.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `RequestOrigin.cs`                                                                                 | Enum - `Unestablished` / `EdgeInbound` / `CrossProcessHop` / `InProcessModule` / `System`. What kind of trust boundary produced the current request hop's context. Recomputed locally by the receiving boundary from its own unforgeable transport facts every hop; never propagated. `Unestablished` (the zero member) is the fail-closed default - a host capability authority that consumes `RequestOrigin` denies when the origin has not been positively established. See package docs for `RequestOrigin` / establishment helpers.                                                                                                                                                                                                                                                                                                                                                                                            |
| `CallPathKind.cs`                                                                                  | Enum - `Edge` / `WorkloadHop` / `ModuleHop` / `System`. The kind of hop a `CallPathEntry` records.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `CallPathEntry.cs`                                                                                 | `sealed record CallPathEntry(string Id, CallPathKind Kind, DateTimeOffset Timestamp)`. One hop in the propagated call-path - the identity of the workload/module that handled the request, the hop kind, and when it was reached. Operational telemetry only; authority code never reads the call-path.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `D2WorkloadIdentityOptions.cs`                                                                     | Options class - `ServiceId` (the host's own workload identity, the same value its mTLS leaf SAN encodes). Read by every establishment boundary (external-inbound HTTP middleware, cross-process gRPC interceptor, System-worker bootstrap) so each appends the same self-id to the call-path.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `ActorEntry.cs`                                                                                    | `sealed record ActorEntry(ActorKind Kind, string Subject, string? ClientId, ImpersonationKind?, Guid? SessionId, Guid? OrgId, string? OrgName, OrgType?, Role? OrgRole, ActorEntry? Act)`. The recursive `Act` field models RFC 8693 section 2.1 nested chains; the `ImpersonationKind` / `SessionId` / `OrgId` / `OrgName` / `OrgType` / `OrgRole` fields apply when `Kind == Impersonation` (they describe the agent / impersonator's own context).                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `Scopes.g.cs` (codegen, in `Generated/DcsvIo.D2.Auth.Scopes.SourceGen/...`)                        | Static partial class - OAuth-canonical scope string constants emitted from `contracts/auth-scopes/scopes.spec.json` by the sibling `DcsvIo.D2.Auth.Scopes.SourceGen` analyzer. The single definition of the platform's scope catalog.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `Audiences.g.cs` (codegen, in `Generated/DcsvIo.D2.Auth.Audiences.SourceGen/...`)                  | Static partial class - JWT `aud`-claim audience constants emitted from `contracts/auth-audiences/audiences.spec.json` by the sibling `DcsvIo.D2.Auth.Audiences.SourceGen` analyzer. The single definition of the audiences for the inbound `aud`-claim check at every hop (under the forward-unchanged model that check is the broad internal audience every internal service accepts) and for the `targetAudience` argument of the boundary-mint / exception token exchanges (`TokenExchangeClient.ExchangeAsync`). Provides `IsKnown(url)`, `Resolve(name)`, `ResolveByUrl(url)` helpers, plus `AllUrls` (read-only set of every audience URL) and `ByName` (read-only name -> URL map) collection projections for enumeration.                                                                                                                                                                                                   |
| `WellKnownAudiences.g.cs` (codegen, in `Generated/DcsvIo.D2.Auth.ProtocolAudiences.SourceGen/...`) | Static partial class - well-known JWT `aud` protocol audiences emitted from `contracts/auth-protocol-audiences/protocol-audiences.spec.json` by the sibling `DcsvIo.D2.Auth.ProtocolAudiences.SourceGen` analyzer. They are absent from `audiences.spec.json` because they are bare-token audiences and not token-exchange *targets* like the spec-generated `Audiences` entries. Contains `D2_INTERNAL_AUDIENCE` (`"d2.internal"`), the single broad internal *receive* audience every hop validates and the boundary minter sets under the forward-unchanged model, and `D2_EDGE_SELF_AUDIENCE` (`"d2-edge"`), the audience the boundary accepts on tokens minted for itself.                                                                                                                                                                                                                                                     |
| `ForwardedJwt.cs`                                                                                  | `readonly struct` - redacting wrapper around the raw internal transaction-token (an RS256 JWT bearer string) a hop retains to replay byte-for-byte on an outbound cross-process gRPC hop. Unloggable by construction (forward-unchanged realization): has `[RedactData(SecretInformation)]`, a self-redacting `ToString()`, the raw bytes in a private field (invisible to the Serilog destructuring policy, which reflects only public properties), and a single reveal seam `RevealForForwarding()`. `Create(string?) -> D2Result<ForwardedJwt>` validates a blank credential away (no JWT-format re-check - the token is pre-validated at the capture site).                                                                                                                                                                                                                                                                     |
| `IForwardedJwtAccessor.cs`                                                                         | Interface - request-scoped holder for the inbound forwarded JWT (`Current` read + `Capture(rawBearer)` write + `Clear()`, which a gate composed after the capture calls when it refuses, so a credential no gate accepted is not forwardable). Structurally isolated from `IRequestContext` (a distinct type with a distinct DI registration, never projected by the request-context enricher) so the live credential cannot leak through the broadly-projected log/telemetry surface. Registered request-scoped by the HTTP and gRPC transport packages; populated by the HTTP middleware / gRPC interceptor after successful validation; read by the outbound forwarding credential.                                                                                                                                                                                                                                              |
| `MutableForwardedJwtAccessor.cs`                                                                   | `public sealed class` - default `IForwardedJwtAccessor` impl, a plain per-request mutable cell with no static state (request isolation = the scoped DI lifetime). Public because the DI registration sites live in the separate transport assemblies. Capture routes through `ForwardedJwt.Create` (blank input validated away, never stored); a second capture in one scope is last-write-wins.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `IAmbientRequestScopeAccessor.cs`                                                                  | Framework-free port - `IServiceProvider? Current { get; }` - that the outbound forwarding credential (supplied by the host's outbound runtime) depends on to reach the current request's request-scoped `IForwardedJwtAccessor` per outbound RPC. Lives here (rather than in the outbound lib) so both the outbound lib and the inbound transport libs reference the port from a shared, framework-free home. This avoids an `auth/http -> auth/outbound` package reference, which would pull the entire outbound gRPC stack into every HTTP-inbound consumer. Each transport lib supplies its own `IHttpContextAccessor`-backed adapter (a deliberate tiny duplicate, since the HTTP and gRPC transport libs have no inter-csproj dep): one adapter registered by the HTTP transport lib, and one registered by the gRPC one.                                                                                                      |
| `JwtClaimTypes.g.cs` (codegen, in `Generated/DcsvIo.D2.Auth.JwtClaims.SourceGen/...`)              | Static class - claim name constants emitted from `contracts/jwt-claims/jwt-claims.spec.json` by the sibling `DcsvIo.D2.Auth.JwtClaims.SourceGen` analyzer. Standard claims (`sub`, `aud`, `act`, `scope`, ...) keep canonical names; D2 custom claims use the `d2_` prefix. The `act.d2_kind` claim discriminates impersonation flavor (Consent vs Force) - see `ImpersonationKind` for the values. Same spec drives the TS-side `@dcsv-io/d2-auth-abstractions` `JwtClaimTypes` catalog.                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `Jwks/IJwksProvider.cs`                                                                            | Interface - `GetKeysAsync` / `RefreshAsync` returning `D2Result<JwksKeySetSnapshot>` / `D2Result`. The contract every consumer-side service uses to look up JWT verify keys by `kid`. Implementations are host-supplied (this package is the port only).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `Jwks/JwksKeySetSnapshot.cs`                                                                       | Sealed record - immutable snapshot of the verify-key set (kid -> `SecurityKey` dictionary + fetched-at timestamp + source URL). Returned by `IJwksProvider.GetKeysAsync`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `Sessions/SessionLivenessCacheKey.cs`                                                              | Static class - the single declaration of the session-liveness cache key: `DEFAULT_PREFIX` (`"session:"`), `Compose(prefix, sessionId)`, `HasPrefix(key, prefix)` and `TryParse(key, prefix, out sessionId)`, with the shape `{prefix}{sessionId:N}`. Composing and parsing live together so they cannot drift apart. **There is no read-side liveness port here, and no consumer asks whether a session is alive.** The boundary that owns the session store decides revocation itself. The key exists for the invalidation broadcast that a revocation produces, and the token-exchange cache filters on that broadcast to purge the exchanged tokens it holds for that session.                                                                                                                                                                                                                                                   |
| `Http/D2HttpContextItems.g.cs` (codegen, in `Generated/DcsvIo.D2.InProcessKeys.SourceGen/...`)     | Static class - string-key constants for cross-transport `HttpContext.Items` slots emitted from `contracts/in-process-keys/keys.spec.json` by the sibling `DcsvIo.D2.InProcessKeys.SourceGen` analyzer. Defines `REQUEST_CONTEXT` (`"D2.RequestContext"`), the slot under which the inbound auth runtime writes the populated `IRequestContext` on successful auth. Lives in the abstractions slice so both host-supplied transport-binding projects (HTTP middleware + gRPC interceptor) can write to the same slot without an inter-csproj dep. The same spec drives the gRPC-side `DcsvIo.D2.Auth.Grpc.Interceptors.D2GrpcUserStateKeys` catalog at identical wire values.                                                                                                                                                                                                                                                        |

The `Generated/` directory is tracked in git and contains the SourceGen-emitted output - committed for inspection, IDE
navigation, and PR diff review. Files are re-emitted on every `dotnet build` from the spec contracts; never hand-edit.
The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by
default.

---

## Public API surface

### `ActorKind` + `ImpersonationKind` - RFC 8693 / RFC 6749 section 4.4 token taxonomy

```csharp
ActorKind.Service           // act entry is a service identity (RFC 6749 section 4.4 client_credentials)
ActorKind.Impersonation     // act entry is a user impersonating another user

ImpersonationKind.Consent   // the impersonated user authorized it with a one-time code
ImpersonationKind.Force     // no consent, and not surfaced to the impersonated user
```

**Token shape** (derived from act chain):

| Act chain shape                             | Effective token kind                                                              |
| ------------------------------------------- | --------------------------------------------------------------------------------- |
| Empty (no `act` claim)                      | End-user direct token (only at the external HTTP boundary, from browser requests) |
| Outermost entry has `Kind == Service`       | Service-on-behalf-of-user delegation OR pure service-identity token               |
| Outermost entry has `Kind == Impersonation` | User impersonation; `entry.ImpersonationKind` says Consent or Force               |

### `OrgType` / `Role` enums

Used as typed properties on `IAuthContext` (auth/context-abstractions). Wire format: lowercase string in JWT claims
(`d2_org_type`, `d2_org_role`). The codegen-emitted `MutableRequestContext.FromClaims(...)` handles parse / format.

The JWT carries a single org context - during impersonation, the JWT's org claim is the impersonated user's org. The
agent's own org is recorded inside the act chain for audit and for authz rules that key on the agent's home org.

### `RoleAuthority` - who may act on whom

```csharp
RoleAuthority.RankOf(Role.Auditor);              // 0; Agent 1, Officer 2, Owner 3
RoleAuthority.Outranks(Role.Officer, Role.Agent); // true
RoleAuthority.Outranks(Role.Owner, Role.Owner);   // false - the relation is irreflexive
RoleAuthority.Outranks((Role)9, Role.Agent);      // false - undeclared carries no authority
RoleAuthority.TryRankOf((Role)9, out _);          // false - the non-throwing rank
```

The package keeps two questions apart. **Capability** - what a role may do - is declared per (role, org type)
by each scope's `grantedTo` and is not ordered: an `Auditor` holds more read scopes than an `Agent` and no write scopes.
**Authority over another member** - whether one member may change, remove or invite another - is the total order above.
Reading the order as a capability ordering would grant an auditor a write. `RankOf` throws for a value outside the
enumeration rather than inventing a rank; `TryRankOf` is the non-throwing form, and `Outranks` returns false when either
side is undeclared, because an authority decision must deny rather than throw.

### `ActorEntry` record

```csharp
public sealed record ActorEntry(
    ActorKind Kind,
    string Subject,                            // act.sub - user id (Impersonation) or service client_id (Service)
    string? ClientId = null,                   // when Service: OAuth client_id (often == Subject)
    ImpersonationKind? ImpersonationKind = null,  // when Impersonation: Consent or Force
    Guid? SessionId = null,                    // when Impersonation: act.d2_session_id (the impersonation session, distinct from the user's own session)
    Guid? OrgId = null,                        // when Impersonation: act.d2_org_id - the agent's own org
    string? OrgName = null,                    // when Impersonation: act.d2_org_name
    OrgType? OrgType = null,                   // when Impersonation: act.d2_org_type
    Role? OrgRole = null,                      // when Impersonation: act.d2_org_role - agent's role in their own org
    ActorEntry? Act = null);                   // RFC 8693 section 2.1 nested chain
```

`SessionId` is masked on the logging and printing paths: `[RedactData(SecretInformation)]` masks it under `{@x}`
destructuring, and the record's `ToString` - which a plain `{x}` capture and an interpolation call - prints it as
`[REDACTED: SecretInformation]`. A serializer is not one of those paths: `System.Text.Json` writes the raw value when
it serializes an `ActorEntry` or `IAuthContext.ActorChain`, so do not serialize either into a log field. Reading the
property returns the value.

`IAuthContext.ActorChain` exposes this as `IReadOnlyList<ActorEntry>` for enumeration. Each entry's `Act`
field walks the nested chain when delegation-of-delegation occurs (e.g. an inbound boundary -> a downstream service -> a
further service, all carrying user identity).

The `Org*` fields on Impersonation entries hold the agent's own organizational context - useful for audit ("Alice
from Customer Support impersonated Bob") and for authz rules that key on the agent's home org. `IAuthContext` exposes
these as derived top-level convenience properties (`ImpersonatorOrgId`, `ImpersonatorOrgName`, `ImpersonatorOrgType`,
`ImpersonatorOrgRole`) - null when not impersonating.

### Constant classes

```csharp
JwtClaimTypes.SUB                          // "sub"
JwtClaimTypes.SCOPE                        // "scope"
JwtClaimTypes.ACT                          // "act"
JwtClaimTypes.CLIENT_ID                    // "client_id"      (RFC 8693 section 4.3 / RFC 9068 section 2.2)
JwtClaimTypes.SESSION_ID                   // "d2_session_id"
JwtClaimTypes.ORG_ID                       // "d2_org_id"
JwtClaimTypes.ORG_ROLE                     // "d2_org_role"
JwtClaimTypes.ROOT_ORG_ID                  // "d2_root_org_id"
JwtClaimTypes.ACCESSIBLE_ORG_IDS           // "d2_accessible_org_ids"
JwtClaimTypes.ACT_KIND                     // "d2_kind"        (lookup path: act.d2_kind)
JwtClaimTypes.ACT_SESSION_ID               // "d2_session_id"  (lookup path: act.d2_session_id)
// ... plus more

// Wire-protocol header constants live in the per-transport catalogs
// (DcsvIo.D2.Headers.{Common,Http,Amqp,Grpc}). See ../README.md section Libraries
// for the full per-transport enumeration.

Scopes.Self.Read                           // "self.read"   (codegen-emitted)
Scopes.Anon.Public.Health                  // "anon.public.health"
// (full catalog lives in contracts/auth-scopes/scopes.spec.json)

WellKnownAudiences.D2_INTERNAL_AUDIENCE    // "d2.internal" (spec-derived from contracts/auth-protocol-audiences -
                                           //  the universal internal receive audience every hop validates)
```

### `RequestOrigin` + `CallPath` - local establishment facts vs propagated telemetry

What kind of trust boundary produced the current request hop's context, and what
sequence of hops the request has traversed so far, are two structurally distinct
kinds of fact:

```csharp
RequestOrigin.Unestablished    // no boundary has positively established this yet - fail-closed
RequestOrigin.EdgeInbound      // the external HTTP boundary; starts the call-path
RequestOrigin.CrossProcessHop  // a gRPC hop authenticated by a validated mTLS client certificate
RequestOrigin.InProcessModule  // an in-host module call through the generated I<Module>Api leaf
RequestOrigin.System           // an in-host background worker with no inbound user request

CallPathKind.Edge          // external HTTP / API-gateway inbound boundary (public enum member name)
CallPathKind.WorkloadHop   // a cross-process mTLS-authenticated hop
CallPathKind.ModuleHop     // an in-process module hop
CallPathKind.System        // an in-host system worker
```

`IRequestContext.Origin` and its paired `ImmediateCaller` are **never propagated**: every establishment boundary (the
external-inbound HTTP middleware, the cross-process gRPC interceptor, the in-process-module boundary and the
System-worker bootstrap, all in `DcsvIo.D2.Context.Abstractions` and the transport-binding libs) recomputes both fresh
from its own local, unforgeable transport evidence every hop. `RequestOrigin.Unestablished` (the enum's zero member) is
the scoped default, so a context no boundary has touched fails closed by construction - a host capability authority
that consumes `RequestOrigin` denies rather than assume a plane.

`CallPath` (`IReadOnlyList<CallPathEntry>`) is the opposite shape on purpose: it **does** propagate on `x-d2-context`
(depth-bounded - see `CallPathOps` in `DcsvIo.D2.Context.Abstractions`), accumulating each hop's own `CallPathEntry` as
the request travels. It is operational telemetry only - authority code never takes a `CallPath` parameter.

### Forwarded-JWT credential - `ForwardedJwt` + `IForwardedJwtAccessor`

The in-process realization of the forward-unchanged rule - how a hop *holds* the forwarded transaction-token so it can
re-attach it outbound.

```csharp
// Smart constructor - validates a blank credential away; no JWT-format re-check
// (the token is already validated at the capture site).
D2Result<ForwardedJwt> result = ForwardedJwt.Create(rawBearer);

// THE sole reveal seam - the only path to the raw bytes (used by the outbound
// forwarding credential). Throws on an empty wrapper.
string raw = forwardedJwt.RevealForForwarding();

// Unloggable by construction - every log/serialization path yields the placeholder.
forwardedJwt.ToString();                   // "[REDACTED: ForwardedJwt]"
// {@x} destructuring -> "[REDACTED: SecretInformation]" via the [RedactData] policy.

// Request-scoped holder - populated by the inbound auth surface, read by the
// outbound forwarding credential. The host's HTTP and gRPC transport registrations
// each register it request-scoped.
ForwardedJwt? current = accessor.Current;
accessor.Capture(rawBearer);

// A gate composed AFTER the capture - a boundary's own revocation check - drops
// the credential it refused rather than leaving it forwardable.
accessor.Clear();
```

**Never-logged guarantee** (proven by tests, not asserted), in these layers: self-redacting (`ToString` +
`[RedactData]` + private backing field), single reveal seam (`RevealForForwarding`), enrichment-isolated (held off
`IRequestContext`, so the request-context enricher cannot reach it), and never a `[LoggerMessage]` parameter.

---

## Configuration

The package reads no configuration and registers no services. It declares one options class,
`D2WorkloadIdentityOptions`, whose single property `ServiceId` (default: empty) is the host's own workload id - a
lowercase label such as `orders`, the same value the host's mutual-TLS leaf certificate encodes. The host's
establishment-boundary registrations bind it and refuse an empty `ServiceId` at startup, and
`ISystemWorkScopeFactory.BeginAsync` refuses an empty one when a System-worker scope begins.

`SessionLivenessCacheKey` takes its key prefix as an argument rather than reading it from configuration.
`SessionLivenessCacheKey.DEFAULT_PREFIX` (`"session:"`) is the default, and a host that overrides it overrides it for
every component in the process.

## Dependencies

- `DcsvIo.D2.Result` - `D2Result<T>` returns on the consumer-side contract (`IJwksProvider`) and `ForwardedJwt.Create`.
  Transitively re-exposes `ErrorCategory` (used by `ForwardedJwt.Create`'s generic `ValidationFailed`).
- `DcsvIo.D2.I18n.Abstractions` - `TKMessage` parameters via `D2Result.Messages` / `InputErrors` shape.
- `DcsvIo.D2.Utilities` - `[RedactData]` + `RedactReason` for the `ForwardedJwt` redacting wrapper, and
  `ToNullIfEmpty()` for its smart constructor. A leaf (references only result/i18n), so no cycle.
- `Microsoft.IdentityModel.Tokens` - `SecurityKey` type used on `JwksKeySetSnapshot.Keys`. The de-facto BCL-adjacent
  abstraction (ASP.NET Core's `TokenValidationParameters.IssuerSigningKeys` takes it;
  `ConfigurationManager<OpenIdConnectConfiguration>` emits it). No deeper abstraction layer adds value here.
- Analyzer-only project references to `DcsvIo.D2.Auth.Scopes.SourceGen` and `DcsvIo.D2.Auth.Audiences.SourceGen` for the
  codegen output.

The package contains the consumer-side runtime contract surface (interfaces and result-shaped types) but no
implementation logic. Consumer-side runtime libs and an issuer's own server-side code both target these contracts.

## Usage examples

Worked examples of `RoleAuthority`, `SessionLivenessCacheKey` and the forwarded-JWT holder are in `usage.md`, in this
package's documentation folder in the repository.

## Telemetry

None. The package declares no `ActivitySource`, `Meter` or logger, and no type in it writes a span, a metric or a log
line.

## Edge cases

How each type responds to input it refuses - an undeclared `Role`, a blank bearer, an empty `ForwardedJwt`, a null or
empty key prefix - is listed in `usage.md`, in this package's documentation folder in the repository.

---

## Design notes

The reasoning behind the claim shapes above is in `design-notes.md`, in this package's documentation folder in the
repository: the `d2_` prefix on custom claims, the recursive `act` chain and what a token minter must put in it,
strict-mode parsing of that chain, and why every constant catalog is emitted from a spec rather than hand-written.

## Tests

The test files that pin each part of this surface, and the command that runs them, are listed in `tests.md`, in this
package's documentation folder in the repository.

---

## Reference

- `DcsvIo.D2.Messaging.RabbitMq` - context propagation across AMQP
- RFC 8693 - OAuth 2.0 Token Exchange (`act` chain semantics): https://datatracker.ietf.org/doc/html/rfc8693
- RFC 6749 section 4.4 - Client Credentials grant: https://datatracker.ietf.org/doc/html/rfc6749#section-4.4
