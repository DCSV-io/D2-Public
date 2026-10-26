<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.AspNetCore

Cross-cutting ASP.NET Core middleware and endpoint primitives that every D2 service composition root needs and that
belong on no single domain library. The public surfaces are `UseD2SecurityHeaders`, `UseD2Cors`,
`UseD2InfrastructureBypass`, `AddD2ForgeryProtection`, `UseD2ForgeryProtection`, `AddD2ProblemDetails`,
`MapD2HealthEndpoints`, `RunD2ServiceAsync` and `AddD2MutualTls`, plus the canonical `InfrastructurePathMatcher`.
`DcsvIo.D2.Logging`'s request-logging middleware and `DcsvIo.D2.Telemetry`'s AspNetCore-instrumentation `Filter`
callback use `InfrastructurePathMatcher`, so those libraries and this one share one definition of `/health`,
`/alive`, `/metrics` and `/.well-known` matching.

This foundation-tier package depends on `DcsvIo.D2.Utilities` (for `Falsey()` / `Truthy()` / `ToNullIfEmpty()`),
`DcsvIo.D2.Result` (the mTLS peer validator returns a `D2Result`), `DcsvIo.D2.Spiffe` (the SPIFFE grammar the mTLS
validator parses a presented SAN with), `DcsvIo.D2.ProblemDetails.Abstractions` and `DcsvIo.D2.Headers.Http` (the
ProblemDetails customizer), and `Serilog.AspNetCore` (for the `RunD2ServiceAsync` startup wrapper's `Log.Fatal` and
`CloseAndFlushAsync`). `DcsvIo.D2.Logging` and `DcsvIo.D2.Telemetry` depend on this lib, not the other way around.

## Install

```bash
dotnet add package DcsvIo.D2.AspNetCore
```

The library does **not** include:

- Authentication / authorization - the host-supplied JWT middleware does JWT validation, scope checks and identity
  extraction.
- OpenTelemetry SDK setup - `DcsvIo.D2.Telemetry` provides it.
- Serilog configuration / sinks - `DcsvIo.D2.Logging` provides them.

The request-pipeline middleware in this library (security-headers, infrastructure-bypass, ProblemDetails) deliberately
logs via the host's standard `ILogger<T>` rather than `[LoggerMessage]` delegates, and the startup wrapper uses Serilog
static `Log.*` calls (host startup runs outside the request pipeline). The one exception is the mutual-TLS peer
validator (`MtlsLog`): a peer-certificate rejection at the TLS handshake is a security event, so it uses a structured,
allocation-free `[LoggerMessage]` record. No delegate accepts an `Exception`; the exception type name is rendered
PII-safely via `SanitizedExceptionRender.TypeName`.

## Public API surface

### Composition

```csharp
// builder.Services
services.AddD2HealthChecks();
services.AddD2Cors(builder.Configuration);   // reads D2_CORS_ORIGINS__0/1/...
services.AddD2ForgeryProtection(builder.Configuration);
services.AddD2ProblemDetails();

// builder.Build() then app pipeline
app.UseD2SecurityHeaders();
app.UseRouting();
app.UseD2ForgeryProtection();                // needs the routed endpoint's posture
app.UseD2InfrastructureBypass();             // tags + (default) short-circuits
app.UseD2Cors();
// ... business middleware (auth, rate-limit, idempotency, etc.) ...
app.MapD2HealthEndpoints();                  // /health + /alive
// ... other Map* ...

await app.RunD2ServiceAsync("orders");
```

Each extension has its own section in the middleware reference, `middleware-reference.md`, which ships in this
package's documentation folder in the repository:

| Extension                                           | What it does                                                     |
| --------------------------------------------------- | ---------------------------------------------------------------- |
| `UseD2SecurityHeaders`                              | Response security headers, HSTS, referrer and frame policy       |
| `AddD2Cors` + `UseD2Cors`                           | Origin allow-list bound from configuration                       |
| `AddD2ForgeryProtection` + `UseD2ForgeryProtection` | Double-submit token half and fetch-metadata half                 |
| `UseD2InfrastructureBypass`                         | Tags and short-circuits infrastructure paths                     |
| `AddD2ProblemDetails`                               | RFC 9457 customizer and its extension members                    |
| `AddD2HealthChecks` + `MapD2HealthEndpoints`        | `/health` and `/alive`                                           |
| `RunD2ServiceAsync`                                 | Startup wrapper with fatal-log flush                             |
| `AddD2MutualTls`                                    | Peer-certificate validation against the SPIFFE grammar           |
| `D2AspNetCoreConstants`                             | Header names and option keys                                     |
| `D2ProblemDetailsContextItems`                      | `HttpContext.Items` slots the customizer reads                   |
| `InfrastructurePathMatcher`                         | The shared `/health`, `/alive`, `/metrics`, `/.well-known` match |

## Types (source map)

| File                                                       | Role                                                                                                                  |
| ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.AspNetCore.csproj`                              | csproj - `Microsoft.NET.Sdk.Web` and `OutputType=Library`. Package dependency on `DcsvIo.D2.Utilities`.               |
| `D2AspNetCoreConstants.cs`                                 | Public constants (endpoint paths, header names, config keys, default infrastructure-path list).                       |
| `InfrastructurePathMatcher.cs`                             | Public static helper - canonical path-matcher used by Logging, Telemetry and this library's bypass middleware.        |
| `D2SecurityHeadersOptions.cs`                              | Sealed record - per-header override Options-pattern config.                                                           |
| `D2CorsOptions.cs`                                         | Sealed record - CORS policy Options-pattern config.                                                                   |
| `D2InfrastructureBypassOptions.cs`                         | Sealed record - bypass middleware Options-pattern config.                                                             |
| `D2ProblemDetailsContextItems.cs`                          | Public constants - the `HttpContext.Items` slot key `D2_RESULT` that the ProblemDetails customizer reads.             |
| `D2ProblemDetailsOptions.cs`                               | Sealed record - ProblemDetails customizer Options-pattern config.                                                     |
| `HttpContextD2ResultExtensions.cs`                         | Public extension: `SetD2Result` and `GetD2Result` on `HttpContext`, which store and read the failing `D2Result`.      |
| `SecurityHeadersApplicationBuilderExtensions.cs`           | Public extension: `UseD2SecurityHeaders`.                                                                             |
| `CorsServiceCollectionExtensions.cs`                       | Public extension: `AddD2Cors`.                                                                                        |
| `CorsApplicationBuilderExtensions.cs`                      | Public extension: `UseD2Cors`.                                                                                        |
| `InfrastructureBypassApplicationBuilderExtensions.cs`      | Public extension: `UseD2InfrastructureBypass`.                                                                        |
| `ProblemDetailsServiceCollectionExtensions.cs`             | Public extension: `AddD2ProblemDetails`.                                                                              |
| `HealthEndpointsServiceCollectionExtensions.cs`            | Public extension: `AddD2HealthChecks`.                                                                                |
| `HealthEndpointsRouteBuilderExtensions.cs`                 | Public extension: `MapD2HealthEndpoints`.                                                                             |
| `RunD2ServiceWebApplicationExtensions.cs`                  | Public extension: `RunD2ServiceAsync`.                                                                                |
| `Forgery/D2ForgeryPostures.cs`                             | `D2ForgeryPostures` - the closed set of posture values an endpoint declares.                                          |
| `Forgery/D2ForgeryProtectionOptions.cs`                    | `D2ForgeryProtectionOptions` - `TokenCookieName` and `TokenHeaderName`.                                               |
| `Forgery/D2ForgeryHeaderNames.cs`                          | `D2ForgeryHeaderNames` - the fetch-metadata header names the check reads.                                             |
| `Forgery/D2ForgeryRefusalReasons.cs`                       | `D2ForgeryRefusalReasons` - the closed set of reasons the check refuses, as emitted on the metric and log.            |
| `Forgery/D2ForgeryTokenCookie.cs`                          | `D2ForgeryTokenCookie` - the cookie attributes the host writes the token under.                                       |
| `Forgery/D2ForgeryTokenIssue.cs`                           | `D2ForgeryTokenIssue` - a sealed token and the expiry the host writes with it.                                        |
| `Forgery/D2ForgeryTokenState.cs`                           | `D2ForgeryTokenState` - the request-scoped verdict the response path reads.                                           |
| `Forgery/D2ForgeryTokenVerdict.cs`                         | `D2ForgeryTokenVerdict` - what the host's verifier made of the presented token.                                       |
| `Forgery/ID2ForgeryPostureDeclaration.cs`                  | `ID2ForgeryPostureDeclaration` - the endpoint metadata a generated marker implements.                                 |
| `Forgery/ID2ForgeryTokenVerifier.cs`                       | `ID2ForgeryTokenVerifier` - the token half a host that serves a session cookie registers.                             |
| `Forgery/ForgeryProtectionTelemetry.cs`                    | `ForgeryProtectionTelemetry` - the meter, its counters and the outcome tag name.                                      |
| `Forgery/ForgeryProtectionServiceCollectionExtensions.cs`  | Public extension: `AddD2ForgeryProtection`.                                                                           |
| `Forgery/ForgeryProtectionApplicationBuilderExtensions.cs` | Public extension: `UseD2ForgeryProtection`.                                                                           |
| `Forgery/ForgeryProtectionLog.cs`                          | The log delegates the check writes.                                                                                   |
| `Forgery/ForgeryProtectionMiddleware.cs`                   | Internal sealed middleware impl behind `UseD2ForgeryProtection`.                                                      |
| `Internal/SecurityHeadersMiddleware.cs`                    | Internal sealed middleware impl behind `UseD2SecurityHeaders`.                                                        |
| `Internal/InfrastructureBypassMiddleware.cs`               | Internal sealed middleware impl behind `UseD2InfrastructureBypass`.                                                   |
| `Internal/D2ProblemDetailsCustomizer.cs`                   | Internal helper - the `Action<ProblemDetailsContext>` body.                                                           |
| `Mtls/D2MutualTlsOptions.cs`                               | Sealed class - mutual-TLS Options-pattern config (enabled / allowed-workloads / trust-domain / anchors).              |
| `Mtls/MutualTlsHostExtensions.cs`                          | Public extension: `AddD2MutualTls` (Kestrel require-and-validate wiring, fail-loud option validation).                |
| `Mtls/PeerWorkloadIdentityAccessor.cs`                     | Public extension: `GetD2PeerWorkloadIdentity` on `HttpContext`, which returns the validated peer workload id or null. |
| `Mtls/SpiffeSanPeerValidator.cs`                           | Internal sealed default-deny 3-conjunct peer-certificate validator.                                                   |
| `Mtls/MtlsLog.cs`                                          | Internal `[LoggerMessage]` delegates for the peer-validator rejection path (no `Exception` param).                    |

## Dependencies

| Package                 | Why                                                                                                               |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Serilog.AspNetCore`    | Static `Log.*` facade used by `RunD2ServiceAsync`. Already pinned (consumed transitively by `DcsvIo.D2.Logging`). |
| `JetBrains.Annotations` | `[MustDisposeResource]` annotations on disposable factory paths (none currently; consumed transitively).          |

| Package dependency                      | Why                                                                                                                                                                                |
| --------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Utilities`                   | `Falsey()` / `Truthy()` / `ToNullIfEmpty()` extensions consumed throughout (options validation, env-var resolution, header-override tri-state, per-entry path / origin filtering). |
| `DcsvIo.D2.Result`                      | `D2Result` returned by the mTLS peer validator and consumed by the ProblemDetails customizer.                                                                                      |
| `DcsvIo.D2.ProblemDetails.Abstractions` | Spec-emitted `D2ProblemDetailsKeys` consumed by the ProblemDetails customizer.                                                                                                     |
| `DcsvIo.D2.Headers.Http`                | `HttpHeaders.IDEMPOTENCY_KEY` / `CORRELATION_ID` consumed by the CORS allowlist + ProblemDetails customizer.                                                                       |
| `DcsvIo.D2.Spiffe`                      | The shared `SpiffeWorkloadIdentity` SPIFFE grammar the mTLS peer validator parses a presented certificate's URI SAN with.                                                          |

The `Microsoft.AspNetCore.App` framework reference (via `Microsoft.NET.Sdk.Web`) provides `IApplicationBuilder`,
`IEndpointRouteBuilder`, `WebApplication`, `HttpContext`, `PathString`, `ProblemDetails`, `ProblemDetailsContext`,
`IProblemDetailsService`, `HealthCheckResult`, `HealthCheckOptions`, the CORS `CorsPolicyBuilder` and middleware, and
the `Microsoft.Extensions.{DependencyInjection,Options,Configuration,Logging,Hosting}.Abstractions` packages.

## Edge cases / gotchas

- **HSTS only on HTTPS.** The `Strict-Transport-Security` header is conditionally written only when `request.IsHttps` is
  `true` - HSTS over cleartext HTTP is meaningless and the spec forbids preload submission for non-HTTPS-only origins.
  TestHost defaults to HTTPS; tests covering the cleartext HTTP branch construct an explicit non-TLS request URI.
- **CORS fail-closed on empty origins.** `D2_CORS_ORIGINS__*` empty / unset -> `ValidateOnStart()` raises
  `OptionsValidationException` at host build. Services that need **no** CORS do not call `AddD2Cors` at all.
- **`AllowCredentials = true` + `Origins = ["*"]` is forbidden.** The validator rejects this combination per CORS spec -
  listing explicit origins is the only safe way to accept credentials cross-origin.
- **`AllowCredentials = true` + `Authorization` or `*` in `AllowedHeaders` is forbidden too.** `Authorization` is not in
  the default allowed-headers set, and the validator rejects a policy that adds it explicitly or through the wildcard
  while allowing credentials: a page on an allowlisted origin could otherwise attach that header to a credentialed
  cross-origin mutation, and `UseD2ForgeryProtection` treats a request presenting it as exempt from the forgery check.
  A host serving bearer-only cross-origin callers adds the header and sets `AllowCredentials = false`.
- **Infrastructure-bypass requires `UseRouting()` first.** The short-circuit invokes the routing-matched endpoint
  directly via `context.GetEndpoint()?.RequestDelegate`. When no endpoint has been routed yet (caller put bypass before
  `UseRouting()`), the middleware falls through to the next delegate so the pipeline still completes correctly, but the
  bypass then does nothing.
- **`AddD2HealthChecks` is idempotent; `MapD2HealthEndpoints` is not.** Calling `MapD2HealthEndpoints` twice raises a
  duplicate-route exception per the underlying ASP.NET Core endpoint-routing convention, so a pipeline registers it
  exactly once.
- **`RunD2ServiceAsync` uses PII-safe exception rendering.** `Log.Fatal` on the catch path captures only the exception
  type FullName + first stack frame, **never** `ex.Message`, since exception messages at host startup can carry
  connection strings, configured secrets, and host-environment specifics. Operators investigate further in the host's
  process logs.
- **Forgery protection compares `Origin` against the request's own scheme, host and port.** Scheme and host compare
  case-insensitively and the scheme's default port normalizes away on both sides, so a `Host` header differing only in
  letter case or naming `:443` on HTTPS still matches; a value carrying a path, a query or a fragment is not an origin
  and never matches. A deployment that terminates TLS ahead of the host without forwarding the scheme refuses every
  request whose browser sends no `Sec-Fetch-Site`, rather than admitting one. Nothing in this lib runs
  `UseForwardedHeaders`; a host behind such a terminator must register it itself.
- **A forgery token under a key the verifier no longer holds is `Stale`, and one under a key it never held is
  `Forged`.** Both refuse and both re-issue; the difference is the reason an operator reads, and a verifier that keeps
  its remembered key ids in process memory reports `Forged` after a restart for a token it would have called `Stale`
  before one.
- **A forgery token the verifier could not check is refused, exactly as a forged one is.**
  `D2ForgeryTokenVerdict.Unverifiable` means the key was out of reach, which leaves the token unproven; admitting an
  unproven token would let the host's own outage open the check. The caller gets the same code for every reason; only
  the log line and the `outcome` tag an operator reads distinguish an outage.
- **The forgery check runs once per request, at the earliest place a pipeline installs it.** A host that composes an
  aggregate pipeline helper installing this stage and then places `UseD2ForgeryProtection` itself runs the composed one,
  wherever that helper put it. Where the composed placement sits ahead of the stage that resolves the session, the
  verifier reports no session and every required-posture mutation is admitted without the token half. Install the stage
  once, behind the stage that resolves the session.
- **The forgery check throws when its registration is missing.** `UseD2ForgeryProtection` without
  `AddD2ForgeryProtection` finds no request state, and passing the request through would leave the check open by
  default.
- **`X-Correlation-Id` length-cap.** The ProblemDetails customizer caps the inbound `X-Correlation-Id` header value at
  128 chars; over-cap values are treated as absent and a fresh GUID is generated. Prevents an arbitrary-length user
  header from inflating the response body.
- **`InfrastructurePathMatcher` is public.** `DcsvIo.D2.Logging`, `DcsvIo.D2.Telemetry` and this library's bypass
  middleware all use it, so they share one path set.
- **`RunD2ServiceAsync` uses `DcsvIo.D2.Utilities.Diagnostics.SanitizedExceptionRender`** to render the `Log.Fatal`
  exception without PII. Messaging and host auth code share this helper, which is in `DcsvIo.D2.Utilities`, an existing
  `package reference` of this library (it also supplies `Falsey()` / `Truthy()` / `ToNullIfEmpty()`).
