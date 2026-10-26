<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Logging - field policy

Which request-context fields [`DcsvIo.D2.Logging`](../README.md) writes to a sink, which it suppresses, how a host
opts a suppressed one back in, and how the network and IP enrichment treats addresses.

## Network and IP enrichment

The middleware does not log the request's connection-remote IP address (`HttpContext.Connection.RemoteIpAddress`):

- Behind a gateway or reverse proxy, that address is the upstream hop's IP, not the original client's. Operators
  reading logs would take it for the user's IP, which it is not.
- At the ingress boundary, the resolved client IP is PII. Logging it as a structured-context field would bypass
  the `[RedactData]` destructuring policy entirely (diagnostic-context `Set` writes a `ScalarValue` directly).

`D2RequestContextEnricher` copies the geo, network-privacy, ASN, identity, impersonation and tracing fields from the
spec-driven `IRequestContext` onto the request-completion log line. The enricher is always on, and it does nothing
when `IRequestContext` is not in the DI scope. A null check guards each per-field `Set`, so a null field is not emitted
at all, instead of being emitted as a null structured property. `Truthy()` guards the collection fields (`Audience`,
`ActorChain`, `Scopes`), so an empty collection does not add an empty array to the log on every request.

## Design rationale: identity in the request-completion log line

Operators who read internal logs already have tenant access to query org and user metadata through authenticated APIs.
Writing identity into the request-completion log line lets them follow one user's requests across a trace ("show me
Alice's requests across this trace") and discloses nothing beyond what those operators can already query directly. The
trade-off is intentional: every log line written inside the user's own access boundary already carries identity through
trace and correlation IDs, and the enricher shortens that link by one hop.

## Precedence note - `TraceId`, `RequestId`, `RequestPath`

The Tracing-axis fields below collide with values that the request-logging middleware emits independently. The override
behavior differs per field because of how each upstream value reaches the log event.

- `TraceId` - the enricher wins when populated. The middleware sets `TraceId` with
  `IDiagnosticContext.Set("TraceId", httpContext.TraceIdentifier)` early in the `EnrichDiagnosticContext` callback. The
  enricher runs LAST in that callback and writes through the same `IDiagnosticContext` mechanism, so the last writer
  wins in the same diagnostic-context dictionary. When `IRequestContext.TraceId` is populated (it holds the W3C
  distributed-trace id), the enricher overrides the local `HttpContext.TraceIdentifier` value. When it is not
  populated, the middleware's value is kept. Operators reading logs should treat `TraceId` as the W3C value when
  `IRequestContext` is wired and as `HttpContext.TraceIdentifier` otherwise.
- `RequestId` - Serilog wins on the HTTP path. Serilog 9.x's `RequestLoggingMiddleware` emits `RequestId` through its
  own `ForContext` binding before the `EnrichDiagnosticContext` callback runs. Once Serilog has set the property on the
  `LogEvent`, subsequent `IDiagnosticContext.Set("RequestId", ...)` calls are silently dropped by Serilog's
  `AddPropertyIfAbsent` semantics. The enricher still emits `IRequestContext.RequestId` for completeness with the spec
  contract. On transports where Serilog does not pre-bind `RequestId` (custom diagnostic-context integrations on
  non-HTTP transports), the enricher's value appears.
- `RequestPath` - Serilog wins on the HTTP path, for the same reason as `RequestId`. Serilog's request-completion
  message template binds `RequestPath` from `HttpContext.Request.Path` through `ForContext` before the enricher runs,
  so the enricher's emission is silently dropped on HTTP. On AMQP / messaging transports without the same template
  binding, the enricher's value would appear.

The HTTP-path limitations (`RequestId` and `RequestPath`) come from the design of Serilog's request-logging middleware
and are not a bug in the enricher. The enricher keeps the same emission contract on all transports (HTTP and AMQP), as
the spec drives it. This document records the limitation so that operators know `RequestPath` in HTTP logs is always
the local path and `RequestId` is always the local `TraceIdentifier`.

## LOG-OK fields (emitted when populated)

A null check guards every field (`Truthy()` guards collections), so unpopulated fields do not reach the log line.

### Tracing

| #   | Field         | Type      | Reason                                                                                                                                                                                                                                                                      |
| --- | ------------- | --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `TraceId`     | `string?` | W3C distributed-trace id propagated across hops. Overrides the middleware's local `TraceId` when populated (see precedence note above).                                                                                                                                     |
| 2   | `RequestId`   | `string?` | Per-request unique id (`HttpContext.TraceIdentifier` for HTTP; AMQP message-id for messaging). Not PII - synthetic identifier. HTTP-path limitation: Serilog 9.x emits `RequestId` independently; the enricher's emission is silently dropped on HTTP. See precedence note. |
| 3   | `RequestPath` | `string?` | Request path / AMQP routing key. Route templates are operational metadata, not PII. HTTP-path limitation: Serilog's message template pre-binds `RequestPath`; the enricher's emission is silently dropped on HTTP. See precedence note.                                     |

### Auth/Identity

| #   | Field                     | Type      | Reason                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| --- | ------------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | `IsAuthenticated`         | `bool?`   | Trinary auth state (null=pre-auth, false=anonymous, true=authenticated). Capability metadata; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 5   | `Subject`                 | `string?` | Raw JWT `sub` claim - opaque identifier (Guid for users; client_id for service identities). Not PII per OAuth/JWT vocabulary.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 6   | `UserId`                  | `Guid?`   | Parsed `sub` as Guid for user tokens; null for service identities. Opaque audit identifier.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 7   | `Username`                | `string?` | Login handle (lowercase, unique). Human-readable account handle supplied by the host (may be server-generated at account create rather than free-text "choose username at signup"). If a host allows renaming, the value is still a deliberate public identifier within the platform. PII consideration: a handle MAY be email-shaped or name-shaped if rename/host policy allows (e.g. `alice.smith@example.com`, `alicemarsh`). The benefit of capturing username in operational logs (where it is needed for "show me Alice's requests" debugging) outweighs the small privacy cost of email-shaped identifiers. The `Subject` raw-`sub` and `UserId` Guid are the canonical identifiers; `Username` is the human-readable supplement. |
| 8   | `RequestedByClientId`     | `string?` | OAuth `client_id` of the client that requested THIS specific token (RFC 8693 section 4.3 / RFC 9068 section 2.2). Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 9   | `ImmediateCallerClientId` | `string?` | The service that immediately called this handler - outermost Service entry in the act chain. Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 10  | `OriginatingClientId`     | `string?` | The service that started the entire call chain - most-deeply-nested Service entry. Primary audit identifier for end-to-end traceability. Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 11  | `IsServiceIdentity`       | `bool?`   | True when the token represents a service identity (no user). Capability metadata; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |

### Auth/Token+Trust

| #   | Field            | Type                        | Reason                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| --- | ---------------- | --------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 12  | `Audience`       | `IReadOnlyList<string>`     | JWT `aud` claim - service identifier(s) the token was minted for. Destructured as JSON array. Empty-collection gate suppresses `[]` emission.                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 13  | `SessionIdHash`  | `string` (16 hex)           | Non-reversible correlation digest of the user's own session id. The raw id is never emitted: the id does not authenticate a request, but a log line carrying it joins that session across every log and ties it to one person, which is the SECRET regime rather than the PII one. The digest is stable across processes, so cross-line and cross-service correlation is unaffected.                                                                                                                                                                                         |
| 14  | `TokenIssuedAt`  | `DateTimeOffset?`           | JWT `iat` claim. Operational timestamp; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 15  | `TokenExpiresAt` | `DateTimeOffset?`           | JWT `exp` claim. Operational timestamp; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 16  | `ActorChain`     | `IReadOnlyList<ActorEntry>` | RFC 8693 actor chain, flattened. Emitted as a JSON array of the log-safe projection `LogSafeActorEntry`, not of `ActorEntry`. Every member is carried verbatim (Kind, Subject, ClientId, ImpersonationKind, OrgId, OrgName, OrgType, OrgRole, the nested `Act`) except the session id, which appears as `SessionIdHash` holding the same digest as rows 13 and 24; the raw session id has no member on the emitted shape. `ActTruncated` marks an entry whose nested actor the depth cap stopped. Empty-collection gate suppresses `[]` emission for end-user-direct tokens. |

### Auth/Org

| #   | Field     | Type              | Reason                                                                                                                                                                                                                                                                                                                                                                   |
| --- | --------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 17  | `OrgId`   | `Guid?`           | Operating organization id. Opaque identifier; not PII.                                                                                                                                                                                                                                                                                                                   |
| 18  | `OrgName` | `string?`         | Operating organization display name. Internal logs are read by operators who already have tenant access to query org metadata via authenticated APIs; there is no incremental disclosure from emitting org names into logs that those same operators authenticate to access. Same rationale applies to `ImpersonatorOrgName` (the agent's own org during impersonation). |
| 19  | `OrgType` | `OrgType?` (enum) | Org type (`Admin` / `Support` / `Customer` / `ThirdParty` / `Affiliate`). Capability metadata; not PII.                                                                                                                                                                                                                                                                  |
| 20  | `OrgRole` | `Role?` (enum)    | User's role in the org (`Auditor` / `Agent` / `Officer` / `Owner`). Capability metadata; not PII.                                                                                                                                                                                                                                                                        |

### Auth/Impersonation

| #   | Field                        | Type                        | Reason                                                                                                                                                            |
| --- | ---------------------------- | --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 21  | `IsImpersonating`            | `bool?`                     | True when the act chain contains any Impersonation entry. Capability metadata; not PII.                                                                           |
| 22  | `ImpersonationKind`          | `ImpersonationKind?` (enum) | `Consent` (OTP-authorized) vs `Force` (silent, admin-only). Capability metadata.                                                                                  |
| 23  | `ImpersonatedBy`             | `Guid?`                     | Agent's (impersonator's) user id. Opaque identifier; not PII.                                                                                                     |
| 24  | `ImpersonationSessionIdHash` | `string` (16 hex)           | Non-reversible correlation digest of the impersonation session id (separate from the user's own session). Raw value never emitted - same SECRET regime as row 13. |
| 25  | `ImpersonatorOrgId`          | `Guid?`                     | Agent's own org id. Opaque identifier; not PII.                                                                                                                   |
| 26  | `ImpersonatorOrgName`        | `string?`                   | Agent's own org display name. Same rationale as `OrgName` above.                                                                                                  |
| 27  | `ImpersonatorOrgType`        | `OrgType?` (enum)           | Agent's own org type. Capability metadata.                                                                                                                        |
| 28  | `ImpersonatorOrgRole`        | `Role?` (enum)              | Agent's own role in their own org. Capability metadata.                                                                                                           |

### Scopes

| #   | Field    | Type                   | Reason                                                                                                                                                                                                                          |
| --- | -------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 29  | `Scopes` | `IReadOnlySet<string>` | OAuth scope set. Destructured as JSON array (preserves field-array semantics for "show me requests with scope X" queries in Loki / Elasticsearch). Empty-collection gate suppresses `[]` emission for unauthenticated requests. |

### Trust/Risk

| #   | Field       | Type           | Reason                                                                                                                             |
| --- | ----------- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| 30  | `RiskScore` | `int?` (0-100) | Composite request-risk score computed at the ingress boundary. Derived numeric; not PII (zero reverse path to fingerprint inputs). |

### Fingerprints

| #   | Field                | Type      | Reason                                                                                                         |
| --- | -------------------- | --------- | -------------------------------------------------------------------------------------------------------------- |
| 31  | `SessionFingerprint` | `string?` | Fingerprint hash bound when the token is minted (SHA-256-derived components). Opaque hash.                     |
| 32  | `CurrentFingerprint` | `string?` | Per-request recomputed fingerprint hash. Opaque hash. Comparison with `SessionFingerprint` feeds risk scoring. |

### WhoIs/Geo

| #   | Field                 | Type                           | Reason                                                                                                                                                                               |
| --- | --------------------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 33  | `WhoIsHashId`         | `string?`                      | Content-addressable hash of the full WhoIs record. Opaque hash; not PII. Operators with auth can dereference for the full record (including suppressed sub-country geo) when needed. |
| 34  | `AdminLocationHashId` | `string?`                      | Content-addressable hash of admin-location component (city + region + country + postal). Opaque hash; not PII.                                                                       |
| 35  | `CountryCode`         | `string?` (ISO 3166-1 alpha-2) | Country level only, which is coarser than the "city + postal beyond country" PII line.                                                                                               |

### WhoIs/Network-Privacy

| #   | Field       | Type    | Reason                                                                |
| --- | ----------- | ------- | --------------------------------------------------------------------- |
| 36  | `IsVpn`     | `bool?` | Derived boolean; not PII.                                             |
| 37  | `IsProxy`   | `bool?` | Derived boolean; not PII.                                             |
| 38  | `IsTor`     | `bool?` | Derived boolean; not PII. High signal for security review.            |
| 39  | `IsHosting` | `bool?` | Derived boolean; not PII. Hosting-source requests behave differently. |

### WhoIs/ASN

| #   | Field     | Type      | Reason                                                                                                   |
| --- | --------- | --------- | -------------------------------------------------------------------------------------------------------- |
| 40  | `Asn`     | `int?`    | Public AS number (published BGP data); not PII. Risk-scoring + abuse-investigation signal.               |
| 41  | `AsnName` | `string?` | Public AS organization name; not PII.                                                                    |
| 42  | `AsnType` | `string?` | Coarse classification (`business` / `isp` / `hosting` / `mobile` / `education` / `government`); not PII. |

## NOT-LOGGED fields (suppressed at default)

The fields in the table below are PII and are suppressed outright. The geographic ones (lat/long/geohash) are
suppressed because geographic precision is an identification primitive - a geohash is lat/long, just hex-encoded.
Sub-country precision is available only through `WhoIsHashId` lookups against the WhoIs store (operators with auth can
dereference when a real investigation needs it).

| Field             | PII class                          | Reason                                                  |
| ----------------- | ---------------------------------- | ------------------------------------------------------- |
| `ClientIp`        | IP addresses (v4 + v6)             | A raw IP is PII.                                        |
| `City`            | sub-country geographic precision   | Locality grain beyond country is PII.                   |
| `Region`          | sub-country geographic precision   | Locality grain beyond country is PII.                   |
| `SubdivisionCode` | sub-country geographic precision   | ISO 3166-2 subdivision is sub-country precision; PII.   |
| `PostalCode`      | postal code                        | Locality grain beyond country is PII.                   |
| `Latitude`        | geographic (lat/long)              | GPS-exact precision is a PII primitive.                 |
| `Longitude`       | geographic (lat/long)              | GPS-exact precision is a PII primitive.                 |
| `Geohash`         | geographic (lat/long, hex-encoded) | Street-level precision; same rationale as raw lat/long. |

## Opting in to additional fields

Callers can chain into the existing `RequestLoggingOptions.EnrichDiagnosticContext` callback:

```csharp
app.UseD2RequestLogging(opts =>
{
    var existing = opts.EnrichDiagnosticContext;
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        existing?.Invoke(diag, http);
        diag.Set("MyCustomField", ResolveCustomField(http));
    };
});
```

⚠️ Caller-added structured fields BYPASS the `[RedactData]` destructuring policy (the diagnostic-context path emits
`ScalarValue`s directly). Callers must handle PII themselves for anything they add. If the data is PII, log a
`[RedactData]`-decorated wrapper object using the destructuring capture mode (`{@MyObj}`) inside a `LoggerScope`
instead.
