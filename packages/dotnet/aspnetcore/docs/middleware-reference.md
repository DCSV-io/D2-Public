<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.AspNetCore - middleware reference

One section per public extension in [`DcsvIo.D2.AspNetCore`](../README.md): what it registers, the options it
binds, the order it needs in the pipeline, and the states it refuses. The package README contains the composition
sketch and links here.

## Contents

- [`UseD2SecurityHeaders`](#used2securityheadersactiond2securityheadersoptions)
- [`AddD2Cors` + `UseD2Cors`](#addd2corsiconfiguration-actiond2corsoptions--used2cors)
- [`AddD2ForgeryProtection` +
  `UseD2ForgeryProtection`](#addd2forgeryprotectioniconfiguration-actiond2forgeryprotectionoptions--used2forgeryprotection)
- [`UseD2InfrastructureBypass`](#used2infrastructurebypassactiond2infrastructurebypassoptions)
- [`AddD2ProblemDetails`](#addd2problemdetailsactiond2problemdetailsoptions)
- [`AddD2HealthChecks` + `MapD2HealthEndpoints`](#addd2healthchecks--mapd2healthendpoints)
- [`RunD2ServiceAsync`](#rund2serviceasyncstring-servicename--null)
- [`AddD2MutualTls`](#addd2mutualtlsactiond2mutualtlsoptions)
- [Constants - `D2AspNetCoreConstants`](#constants---d2aspnetcoreconstants)
- [Constants - `D2ProblemDetailsContextItems`](#constants---d2problemdetailscontextitems)
- [`InfrastructurePathMatcher`](#infrastructurepathmatcher-public-static)

## `UseD2SecurityHeaders(Action<D2SecurityHeadersOptions>?)`

Installs a middleware that writes an OWASP-aligned default header set on every response via `HttpResponse.OnStarting`:

| Header                              | Default value                                      | Reason                                                                            |
| ----------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------- |
| `X-Content-Type-Options`            | `nosniff`                                          | Prevents MIME-sniffing attacks.                                                   |
| `X-Frame-Options`                   | `DENY`                                             | Prevents clickjacking via framing.                                                |
| `Referrer-Policy`                   | `strict-origin-when-cross-origin`                  | MDN-recommended default; preserves Referer same-origin, only origin cross-origin. |
| `X-Permitted-Cross-Domain-Policies` | `none`                                             | Blocks legacy Adobe Flash / PDF cross-domain.                                     |
| `Cross-Origin-Resource-Policy`      | `same-origin`                                      | Prevents cross-origin embedding (CORB / CORP).                                    |
| `Cross-Origin-Opener-Policy`        | `same-origin`                                      | Isolates browsing context (Spectre mitigation).                                   |
| `Strict-Transport-Security`         | `max-age=31536000; includeSubDomains` (HTTPS only) | 1-year HSTS with subdomain coverage.                                              |

Per-header override semantic on `D2SecurityHeadersOptions`: `null` override -> default written; empty / whitespace
override -> header suppressed; non-empty override -> override literal written.

HSTS preload submission is intentionally **not** included by default - preload is hard to reverse: once the apex domain
is in the browsers' built-in preload list, removal is slow and incomplete. Each service that wants preload submission
opts in by setting `StrictTransportSecurity` to a value that includes `preload`.

The following are **not** set by default (each is service-specific):

- `Content-Security-Policy` - service-specific (each service knows its own script / style / connect sources).
- `Permissions-Policy` - service-specific (each service knows which browser features it uses).
- `X-XSS-Protection` - deprecated by all major browsers (replaced by CSP).

## `AddD2Cors(IConfiguration, Action<D2CorsOptions>?)` + `UseD2Cors()`

Registers the `D2_DEFAULT` CORS policy reading the canonical indexed env-var convention `D2_CORS_ORIGINS__0`,
`D2_CORS_ORIGINS__1`, ... per .NET `IConfiguration` array binding. Validates fail-closed at host build via
`ValidateOnStart()` - empty origins list is a deliberate decision, not an oversight.

Default `D2CorsOptions`:

| Property                 | Default                                                                                                                                                                                                    |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Origins`                | `[]` (validated non-empty at startup; fail-closed)                                                                                                                                                         |
| `AllowedHeaders`         | `Content-Type`, `X-Correlation-Id` (from spec-driven `HttpHeaders.CORRELATION_ID`), `Idempotency-Key` (from spec-driven `HttpHeaders.IDEMPOTENCY_KEY`), `X-Forwarded-For`, `X-Real-IP`, `CF-Connecting-IP` |
| `AllowedMethods`         | `GET`, `HEAD`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS`                                                                                                                                                 |
| `AllowCredentials`       | `true` (a browser client authenticated by a cookie needs it)                                                                                                                                               |
| `PreflightMaxAgeSeconds` | `600` (10 minutes)                                                                                                                                                                                         |

The `AllowCredentials = true` + `Origins = ["*"]` combination is forbidden per CORS spec; the validator rejects it at
host build. `AllowCredentials = true` together with `Authorization` in `AllowedHeaders` is rejected at host build as
well: a page on an allowlisted origin could otherwise attach that header to a credentialed cross-origin mutation, and
`UseD2ForgeryProtection` treats a request presenting it as exempt from the forgery check. A host that serves
bearer-only cross-origin callers adds the header and sets `AllowCredentials = false`.

Services that need **no** CORS do not call `AddD2Cors` at all - calling it **is** the explicit decision to use CORS. New
canonical `X-D2-*` headers added to any cross-cutting middleware are added to `AllowedHeaders` in the same change.

## `AddD2ForgeryProtection(IConfiguration, Action<D2ForgeryProtectionOptions>?)` + `UseD2ForgeryProtection()`

Cross-site request forgery protection for cookie-authenticated mutations: the browser's fetch-metadata headers, and a
signed token the page echoes from a script-readable cookie into a request header. Both must pass. Install
`UseD2ForgeryProtection()` after `app.UseRouting()`, so the routed endpoint's `ID2ForgeryPostureDeclaration` resolves.
It checks each request once, at the earliest registration in the pipeline; where a second registration exists, the
earliest placement decides and the later one passes through, with the consequences that the Edge cases / gotchas
section of the package README describes.
It throws when `AddD2ForgeryProtection` did not run, rather than passing an unchecked request on.

An endpoint declares its posture as the `ID2ForgeryPostureDeclaration.Posture` string, one of the values on
`D2ForgeryPostures`; a value outside that set is treated as `D2ForgeryPostures.REQUIRED`. Classification, first match
wins. A safe method (`GET`, `HEAD`, `OPTIONS`), a request presenting a non-empty `Authorization` header, and an endpoint
declaring `D2ForgeryPostures.EXEMPT` run neither check. The `Authorization` exemption holds only while the host's CORS
policy never allows that header from a foreign origin: a browser cannot attach it cross-site on its own, and a policy
that permits it can. `AddD2Cors` keeps that true for a host built through it - the header is not in the default
`AllowedHeaders`, and allowing it alongside `AllowCredentials` is refused at host build - and a host that builds its
CORS policy some other way must satisfy that precondition itself. `D2ForgeryPostures.METADATA` runs the fetch-metadata
check. `D2ForgeryPostures.REQUIRED`, and an endpoint declaring nothing at all, run the fetch-metadata check on every
non-safe request and the token check on top of it once a session resolves - so an undeclared non-safe route is closed
rather than open, and a session-less mutation on one is refused on the same header states a metadata route is. The
fetch-metadata check admits `Sec-Fetch-Site: same-origin` and `none` and refuses every other value; when that header is
absent it admits only an `Origin` naming the request's own scheme, host and port, comparing scheme and host
case-insensitively and treating the scheme's default port as no port on both sides. `D2ForgeryHeaderNames` defines the
header names that check reads - `SEC_FETCH_SITE` and `ORIGIN` - and the `Sec-Fetch-Site` values it compares against
(`SEC_FETCH_SITE_SAME_ORIGIN`, `SEC_FETCH_SITE_NONE`, `SEC_FETCH_SITE_SAME_SITE`, `SEC_FETCH_SITE_CROSS_SITE`). The
browser sets both headers, so neither name is configurable.

The token check runs where the host registers an `ID2ForgeryTokenVerifier`. It reads the token from `TokenHeaderName` on
a required or undeclared route and from `TokenCookieName` elsewhere, passes it to `ID2ForgeryTokenVerifier.Verify`, and
admits only `D2ForgeryTokenVerdict.Bound`; `Absent`, `Stale`, `Forged` and `Unverifiable` refuse, and `NoSession` means
nothing resolved, so no token can be bound to one and the token check is skipped while the fetch-metadata check still
decides. A host that registers no verifier refuses every non-safe cookie-borne request to a required or undeclared
route, because the verifier determines whether a session resolved, and without one those cases cannot be told apart.
`AddD2ForgeryProtection` registers a request-scoped `D2ForgeryTokenState`, which the middleware writes and the host's
token issuer reads on the response path: `Checked` records that the check has already run on this request, so a second
registration in one pipeline sees it set, and `Verdict` holds what the verifier returned, staying
`D2ForgeryTokenVerdict.NoSession` where the host registered no verifier. The issuer reads `Verdict` to decide whether
the response needs a fresh token cookie.

A refusal returns 403 with an RFC 7807 body that contains `GEN_FORGERY_REFUSED` whatever the reason, asks the verifier
for a fresh token cookie when a session resolved, and never reaches the endpoint.
`ID2ForgeryTokenVerifier.IssueForResolvedSession` returns a `D2ForgeryTokenIssue` for that cookie - `Value`, the sealed
token as it goes on the wire, and `ExpiresAt`, the absolute expiry of the session the token is bound to or `null` for a
session cookie - or `null` when no session resolved or the sealing key was out of reach. `Value` contains live token
material, so it is marked `[RedactData]` and `ToString` renders `D2ForgeryTokenIssue.REDACTION_PLACEHOLDER` in its
place: a structured-log destructure or an interpolation of the record prints no token, and `ExpiresAt` still renders.
`D2ForgeryTokenCookie` defines the attributes both writers use: `PATH` `/`, `SECURE` true, `SAME_SITE` `Lax`, and
`HTTP_ONLY` false, because page script reads the token. `D2ForgeryTokenCookie.Options(expires)` builds the
`CookieOptions` with those attributes and that expiry. `D2ForgeryProtectionOptions` binds the `LIB_ASPNETCORE_FORGERY`
section, whose name is the constant `SECTION`; the defaults below are `DEFAULT_TOKEN_COOKIE_NAME` and
`DEFAULT_TOKEN_HEADER_NAME`, and the validated cookie-name prefix is `REQUIRED_COOKIE_NAME_PREFIX`:

| Property          | Default                                                                    |
| ----------------- | -------------------------------------------------------------------------- |
| `TokenCookieName` | `__Host-d2_csrf`; validated non-empty and `__Host-`-prefixed at host build |
| `TokenHeaderName` | `X-CSRF-Token`; validated non-empty at host build                          |

Telemetry: `ForgeryProtectionTelemetry` defines the meter `SR_Meter` (named by `METER_NAME`), the refusal counter
`SR_Refusals`, the skipped-token-half counter `SR_TokenHalfSkipped`, and the tag key `TAG_OUTCOME`:

| Instrument                                           | Unit        | Tags                                      |
| ---------------------------------------------------- | ----------- | ----------------------------------------- |
| `d2.aspnetcore.forgery.refusals` (counter)           | `{refusal}` | `outcome`, from `D2ForgeryRefusalReasons` |
| `d2.aspnetcore.forgery.token_half_skipped` (counter) | `{request}` | none                                      |

The `outcome` values are the `D2ForgeryRefusalReasons` constants `headers-absent`, `metadata-cross-site`,
`metadata-unknown-value`, `origin-mismatch`, `token-absent`, `token-stale`, `token-forged`, `token-unverifiable` and
`verifier-absent`. Log EventIds 9570 (refused, with the route pattern and the reason), 9571 (a refusal that could
not carry a fresh cookie) and 9572 (Debug - the token half skipped for want of a resolved session, with the route
pattern). None includes a header value, a cookie value or a token.

An operator uses `SR_TokenHalfSkipped` and EventId 9572 to tell a healthy host from one whose pipeline
places this stage ahead of session resolution. On a healthy host the counter tracks sign-in and other session-less
mutations. On a misconfigured one it tracks the whole mutation traffic, because the check reads `NoSession` for every
request and the token half never runs.

## `UseD2InfrastructureBypass(Action<D2InfrastructureBypassOptions>?)`

Installs a middleware that, for each request, sets `HttpContext.Items["D2.IsInfrastructure"]` to a boolean indicating
whether the request path matches the configured infrastructure-path list (default: `/health`, `/alive`, `/metrics`,
`/.well-known`).

Default short-circuit mode (`TagOnly = false`): when the path matches AND a routing-resolved endpoint is present on the
context, the middleware invokes the matched endpoint's `RequestDelegate` directly and returns - bypassing every
middleware registered **after** this one. Heavy business middleware (rate limiting, idempotency, auth) does **not**
execute on cheap probe / metrics / well-known requests.

Tag-only mode (`TagOnly = true`): the middleware sets the `Items` flag and **only** that, then continues the pipeline.
Business middleware downstream still runs and can opt-out by reading the flag - for services that want custom middleware
to execute on infrastructure paths.

Pipeline placement: install AFTER `app.UseRouting()` (which resolves the matched endpoint without invoking it) and
BEFORE the business middleware. When no endpoint has been routed (caller put bypass before `UseRouting()`), the
middleware falls through to the next delegate so the pipeline still completes correctly.

## `AddD2ProblemDetails(Action<D2ProblemDetailsOptions>?)`

Registers ASP.NET Core's `IProblemDetailsService` with the D2 customizer applied as the `CustomizeProblemDetails`
callback. The customizer reads `D2Result`: it is path B of the RFC 7807 emit stack, the sibling of the path-A
conversion a host applies in its own auth middleware.

When the request pipeline has stashed a `D2Result` on `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]` (via
the `SetD2Result` typed extension), the customizer populates the RFC 7807 Shape A body from spec-driven constants in
`D2ProblemDetailsKeys` (`DcsvIo.D2.ProblemDetails.Abstractions`):

1. `Type` <- `TYPE_URI_PREFIX + KebabCase(D2Result.ErrorCode)` (fallback `"unhandled-exception"` on empty error code).
2. `Title` <- `TitleFor(D2Result.StatusCode)`.
3. `Status` <- `(int)D2Result.StatusCode`.
4. `Extensions[d2_error_code]` <- `D2Result.ErrorCode`.
5. `Extensions[d2_messages]` <- `D2Result.Messages`.
6. `Extensions[d2_input_errors]` <- `D2Result.InputErrors` (only when non-empty).
7. `Extensions[d2_category]` <- `D2Result.Category?.ToWire()` (only when non-null). The value is the closed-enum
   semantic `ErrorCategory` wire string and mirrors the gRPC envelope's `category` field for cross-transport parity.

Whether or not a `D2Result` is stashed, the customizer always populates:

8. `Extensions[traceId]` <- `Activity.Current?.TraceId.ToString()` falling back to `HttpContext.TraceIdentifier`.
9. `Extensions[correlationId]` <- inbound `X-Correlation-Id` request header (capped at 128 chars). When absent /
   over-cap, generates `Guid.NewGuid().ToString("N")` and (when `EchoCorrelationIdInResponse` is true) writes it to the
   response header.
10. `Instance` <- `"{Method} {Path}"` when `IncludeRequestPath` is true (matches the path-A emit shape exactly -
    cross-path wire-shape consistency by construction).

Cross-language parity: the .NET path-A + path-B body shapes are byte-identical, and they match the TypeScript-side
`toProblemDetails` output for the same `D2Result` inputs. Every emit site consumes the same spec-derived constants.

PII discipline: the customizer **never** reads `HttpContext.Request.QueryString`, `Request.Body`, or any user-input
source. The 128-char cap on the inbound correlation id prevents an arbitrary-length user header from inflating the
response body;
values exceeding the cap are treated as absent.

**Stashing a D2Result for the customizer**: handlers / middleware call `httpContext.SetD2Result(result)` ahead of
letting the response flow through the `AddProblemDetails` pipeline. Consumers that do not stash a D2Result (raw
exception path) still get `traceId`, `correlationId` and `instance` for diagnostic correlation; the framework defaults
for `Type` / `Title` apply.

## `AddD2HealthChecks()` + `MapD2HealthEndpoints()`

`AddD2HealthChecks()` registers a baseline `"self"` check tagged `"live"` that always returns `Healthy`. Idempotent: a
second call does nothing. Per-service infrastructure layers add their own checks (DB, Redis, message bus,
host-specific dependencies) by chaining `services.AddHealthChecks().AddDbContextCheck<...>()` etc. - those auto-flow
into `/health`. Checks tagged `"live"` additionally participate in `/alive`.

`MapD2HealthEndpoints()` maps:

- `/health` - full health-check status (every registered check, regardless of tag).
- `/alive` - only checks tagged `"live"` (the Kubernetes-conventional liveness split).

## `RunD2ServiceAsync(string? serviceName = null)`

Async wrapper around `WebApplication.RunAsync()` that:

- Logs `Log.Information("Starting {ServiceName} ({EnvironmentName})", ...)` at entry.
- On exception: logs `Log.Fatal` with PII-safe exception rendering - type FullName + first stack frame only, **never**
  `ex.Message`. Re-throws so the host exit code reflects failure.
- In `finally`: awaits `Log.CloseAndFlushAsync()` to drain Serilog's buffered batch sink before process exit.

PII discipline: exception messages at host startup can carry connection strings, configured secrets, and
host-environment specifics; the wrapper renders only the type FullName + first stack frame so log dashboards reflect the
failure class without leaking secrets via `ex.Message`. Operators investigate further in the host's process logs.

The async form catches both synchronously-faulted (host build / hosted-service `StartAsync`) and asynchronously-faulted
(post-startup, mid-request) exceptions.

## `AddD2MutualTls(Action<D2MutualTlsOptions>)`

Wires mutual-TLS client-certificate require-and-validate into the host's Kestrel HTTPS endpoint. When
`D2MutualTlsOptions.Enabled`, Kestrel is configured with `ClientCertificateMode.RequireCertificate` and a
`ClientCertificateValidation` callback that delegates to the default-deny `SpiffeSanPeerValidator`. When disabled (the
default), no Kestrel client-certificate configuration is added - an un-wired host never starts requiring client
certificates and locking itself out. Off by default; a development host and a real cross-process host opt in.

This is the server (callee) half of the internal-mTLS workload-identity layer. The client (caller) half - per-channel
leaf presentation and refresh-ahead - is host-supplied (opt-in). The Kestrel-config LOGIC is in this library; hosts
compose it with a gated `AddD2MutualTls` call and a `MutualTlsConfigure` pass-through.

`SpiffeSanPeerValidator` is a default-deny check whose conjuncts must **all** hold for a certificate to be
accepted:

1. **Chains to the internal CA.** The presented certificate is re-chained against the configured trust anchors with
   `X509ChainTrustMode.CustomRootTrust` rather than the OS machine store. A certificate valid against the machine store
   but not against this deployment's own internal root is rejected; `SslPolicyErrors.None` alone is insufficient.
2. **SPIFFE SAN trust domain matches.** Exactly one URI subject-alternative-name is extracted (via
   `System.Formats.Asn1`) and parsed through the shared `SpiffeWorkloadIdentity` grammar - a foreign trust domain, a
   non-SPIFFE SAN, no URI SAN, or more than one URI SAN is rejected.
3. **Workload in the allowed set.** The parsed workload id must be a member of `D2MutualTlsOptions.AllowedWorkloads`
   (the receiver's "who may call ME" list).

The validator **never** throws - a crypto exception, a malformed SAN, or a chain build failure all map to a rejection
(the same discipline the CA provider uses). The Kestrel callback adapts the `D2Result` to a `bool` (`Ok` => accept).

`D2MutualTlsOptions`:

| Property               | Default | Role                                                                                                                                                                                                                                                       |
| ---------------------- | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Enabled`              | `false` | mTLS is opt-in. When `true`, `TrustAnchorsProvider` MUST be set and every `AllowedWorkloads` entry MUST be canonical, or the host throws at build.                                                                                                         |
| `AllowedWorkloads`     | `[]`    | The receiver's allowed-workload set (lowercase service ids). Empty admits no peer and the port still requires a client certificate. A non-canonical entry - upper case or whitespace - matches nothing, so the host refuses it at build and identifies it. |
| `TrustAnchorsProvider` | `null`  | Host-supplied provider of the PUBLIC internal CA trust anchor(s) the peer chains to. NEVER private keys. Required (non-null) when `Enabled`.                                                                                                               |

The SPIFFE trust domain is fixed at `d2.internal` - enforced by the `SpiffeWorkloadIdentity` grammar, not a configurable
option. Any SAN whose host differs from `d2.internal` is rejected by the grammar before reaching the workload-membership
check.

The options are validated at host build via `ValidateOnStart()`: a missing `TrustAnchorsProvider`, an empty entry, and a
non-canonical entry each fail the build, and the failure lists every offending entry at once. An empty list is valid and
admits no peer. The host supplies trust anchors through `TrustAnchorsProvider` - a development host loads the public
root from a local file; a production host supplies trust anchors from its certificate / CA issuance path. This lib reads
no files, no `secrets/`, and never references a service domain.

### Real-socket proof runs on Linux/OpenSSL

The end-to-end real-socket harness for this path
binds a real Kestrel HTTPS endpoint on a loopback ephemeral port and drives the require-and-validate path over a genuine
TLS handshake - the valid leaf round-trips, every bad-cert variant (wrong CA, expired, foreign trust domain, unknown
workload) is rejected at the handshake, and a no-client-certificate connection is refused by `RequireCertificate`. The
cases that **present** a client certificate run on **Linux/OpenSSL** (the deployment target) and **skip** on Windows:
Windows-Schannel cannot build an `SslStreamCertificateContext` for a leaf chaining to a private CA without first
installing the root into the OS trust store (Microsoft-documented; even a bare leaf, even with the intermediate
supplied), and the harness deliberately performs zero OS-store mutation. The Linux proof
runs in a `.NET 10 SDK` Linux container and needs no Postgres/Redis/RabbitMQ - the harness
is self-contained loopback. The validator's full conjunct matrix is proven cross-platform by the
`SpiffeSanPeerValidator` unit suite, which drives the same validator with in-memory chains and needs no socket.

## Constants - `D2AspNetCoreConstants`

| Constant                               | Value                                                                                          |
| -------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `HEALTH_ENDPOINT_PATH`                 | `/health`                                                                                      |
| `ALIVE_ENDPOINT_PATH`                  | `/alive`                                                                                       |
| `METRICS_ENDPOINT_PATH`                | `/metrics`                                                                                     |
| `WELL_KNOWN_ENDPOINT_PATH`             | `/.well-known`                                                                                 |
| `LIVE_HEALTH_TAG`                      | `live`                                                                                         |
| `SELF_HEALTH_CHECK_NAME`               | `self`                                                                                         |
| `DEFAULT_INFRASTRUCTURE_PATHS`         | `[HEALTH_ENDPOINT_PATH, ALIVE_ENDPOINT_PATH, METRICS_ENDPOINT_PATH, WELL_KNOWN_ENDPOINT_PATH]` |
| `CORS_ORIGINS_CONFIG_KEY`              | `D2_CORS_ORIGINS`                                                                              |
| `DEFAULT_CORS_POLICY_NAME`             | `D2_DEFAULT`                                                                                   |
| `MAX_CORRELATION_ID_LENGTH`            | `128`                                                                                          |
| `INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY` | `D2.IsInfrastructure`                                                                          |

Note: wire-header values consumed by this lib live in the spec-driven `HttpHeaders` catalog -
`HttpHeaders.CORRELATION_ID` (`"X-Correlation-Id"`) and `HttpHeaders.IDEMPOTENCY_KEY` (`"Idempotency-Key"`) are
referenced directly. One wire value for one concept across the platform; no intra-.NET drift between the CORS allowlist
/ ProblemDetails customizer and the headers/http catalog.

## Constants - `D2ProblemDetailsContextItems`

HttpContext.Items slot keys consumed by the path-B Customizer to source the originating `D2Result`:

| Constant    | Value         | Use                                                                                                                                                                                                                                                                               |
| ----------- | ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2_RESULT` | `__d2_result` | Slot under which middleware / handlers stash the originating `D2Result` so the customizer can populate the full RFC 7807 Shape A body from spec constants. Set via the typed `httpContext.SetD2Result(result)` extension; read by the customizer via `httpContext.GetD2Result()`. |

## `InfrastructurePathMatcher` (public static)

The single definition of infrastructure-path matching across the D2 shared libraries.
`DcsvIo.D2.Logging.WebApplicationLoggingExtensions.UseD2RequestLogging` uses it to down-rank request-completion log
lines for infrastructure endpoints to `Verbose`,
`DcsvIo.D2.Telemetry.TelemetryServiceCollectionExtensions.AddD2Telemetry` uses it in the AspNetCore-instrumentation
`Filter` callback to suppress auto-spans, and `UseD2InfrastructureBypass` (this library) uses it.

```csharp
bool IsInfrastructurePath(PathString path, IReadOnlyList<string>? infrastructurePaths);
```

Uses the AspNetCore-canonical `PathString.StartsWithSegments(PathString)` overload - case-insensitive,
segment-boundary-matched (`/healthz` does **not** match prefix `/health`; `/health/db` does). Empty `PathString`, null
configured list, and per-entry null / empty / whitespace prefixes are all defensive no-ops (returns `false` rather than
throwing).
