<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.AspNetCore

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.10 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Headers.Http` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.9 - 2026-10-20
### Changed

- The XML docs of `D2MutualTlsOptions` now use `["orders", "inventory"]` as the example service identifiers, and the XML
  docs of `ProblemDetailsServiceCollectionExtensions` give the full name
  `Microsoft.AspNetCore.Http.ProblemDetailsOptions.CustomizeProblemDetails`. No code changed.
- Dependency update: `DcsvIo.D2.Headers.Http` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ProblemDetails.Abstractions` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Headers.Http` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ProblemDetails.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Headers.Http` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ProblemDetails.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.6 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Headers.Http` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.ProblemDetails.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.5 - 2026-09-27
### Changed

- The README's and the middleware reference's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.5.4 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.3 - 2026-09-13
### Fixed

- `AddD2Cors` rejects the allowed-header wildcard when credentials are enabled, including when the wildcard appears
  beside explicit headers. The wildcard permits `Authorization` and therefore needs the same startup validation as an
  explicit `Authorization` header. Policies without credentials and credentialed policies without either header remain
  supported.
## 0.5.2 - 2026-09-11
### Changed

- `ForgeryProtectionMiddleware` and the middleware reference state what each posture runs rather than what it
  requires, and multi-line statements in the middleware have the blank line the convention asks for. No behavior
  changed.
### Fixed

- The README source-map row for `D2ForgeryProtectionOptions` listed cookie attributes and a TTL the type does not
  declare. It now lists `TokenCookieName` and `TokenHeaderName`, the properties the type declares; the cookie
  attributes stay attributed to `D2ForgeryTokenCookie` in its own row.
## 0.5.1 - 2026-09-10
### Changed

- `D2CorsOptions` no longer includes `Authorization` in its default allowed-headers list, and `AddD2Cors` rejects at
  host build a policy that allows that header together with `AllowCredentials`. `UseD2ForgeryProtection` exempts a
  request presenting a non-empty `Authorization` header from both halves of the forgery check, and its stated
  precondition is that the host's CORS policy never allows that header from a foreign origin. A host built from these
  defaults broke that precondition, letting a page on an allowlisted origin attach an arbitrary `Authorization` value
  to a credentialed cross-origin mutation. A host serving bearer-only cross-origin callers adds the header and sets
  `AllowCredentials` to `false`.
- The README lists each type in the forgery surface rather than three filename globs, and contains no markdown link out
  of the packed README: the registry renderer cannot resolve a relative repository path.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.0 - 2026-09-09
### Added

- `ForgeryProtectionTelemetry.SR_TokenHalfSkipped` counts cookie-borne mutations admitted with the token half skipped
  for want of a resolved session, and `ForgeryProtectionLog` writes a Debug line that includes the route on the same
  arm. That arm previously recorded nothing while every other arm recorded a refusal, so a host whose pipeline places
  the stage ahead of session resolution - which reads `NoSession` for every request and admits every required-posture
  mutation without the token half - looked identical in metrics and logs to a healthy one.
## 0.4.1 - 2026-09-08
### Changed

- The `UseD2ForgeryProtection` README paragraph on the surface no longer describes a second registration in one pipeline
  as passing through harmlessly; it states that the earliest placement decides and points to the edge-cases section,
  which describes the consequence. The surface section and the edge-cases section disagreed.
- The `Authorization` exemption states its condition in the class remarks and the README: it holds only while the host's CORS policy never allows that header from a foreign origin. A browser cannot attach it cross-site on its own; a policy that permits it can.
- `D2ForgeryTokenIssue` records in its remarks why the token stays on a property rather than moving behind a reveal method: a forgery token authenticates nobody on its own, is bound to one already-resolved session, and is delivered on purpose to page script in a cookie that is not `HttpOnly`. The redaction attribute and the `ToString` override stay as defense in depth for the log pipeline. No behavior changed.
- The problem-details parity paragraph refers to the TypeScript-side `toProblemDetails` output rather than a product
  topology term. No behavior changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
### Fixed

- The forgery check runs the fetch-metadata half before it passes a session-less request through. A non-safe request to a `D2ForgeryPostures.REQUIRED` or undeclared endpoint on which no session resolved previously skipped both halves, so an unauthenticated cross-site POST to a sign-in, sign-up or password-reset route reached the endpoint unexamined while the class remarks and the README both described such a route as closed. Only the token half is now skipped for want of a session to bind against; the fetch-metadata half decides as it does on a `D2ForgeryPostures.METADATA` route.
- A present-but-empty `Authorization` header no longer exempts a request from the check. The exemption tested the header's presence rather than its value, so a cookie-borne mutation carrying a blank `Authorization` skipped both halves.
- The `Origin` fallback compares scheme and host case-insensitively and treats the scheme's default port as no port on both sides. A `Host` header differing only in letter case, or naming `:443` on HTTPS, previously refused a legitimate same-origin request from a browser that sends no `Sec-Fetch-Site`, and the refusal was indistinguishable from a real cross-origin one. A value carrying a path, a query or a fragment is still not an origin and still never matches.
## 0.4.0 - 2026-09-06
### Wire-breaking

- The `d2.aspnetcore.forgery.refusals` counter tags its failure class on `outcome` rather than on `reason`. The values are unchanged - they are still the `D2ForgeryRefusalReasons` strings. A dashboard or alert grouping forgery refusals by `reason` sees no series after this bump and groups by `outcome` instead.
### API-breaking

- `ForgeryProtectionTelemetry.TAG_REASON` is removed and replaced by `ForgeryProtectionTelemetry.TAG_OUTCOME`. A consumer reading the tag key through the constant renames the reference; one reading it as a literal changes the literal too.
### Added

- `D2ForgeryTokenIssue.Value` has `[RedactData(Reason = RedactReason.SecretInformation)]`, and the record overrides
  `ToString` to render the new `D2ForgeryTokenIssue.REDACTION_PLACEHOLDER` in place of the token. A structured-log
  destructure or an interpolation of the record no longer prints live token material. `ExpiresAt` still renders.
### Changed

- The `UseD2ForgeryProtection` remarks and the README state that a pipeline runs the check at the earliest place it installs it. A host that composes an aggregate pipeline helper installing this stage and then places the stage itself gets the composed placement, and where that placement sits ahead of the stage resolving a session, every required-posture mutation is admitted without the token half. The previous wording described a double registration as harmless. No behavior changed.
- The `Unverifiable` verdict's disposition is stated at the branch and in the README: a key the verifier could not reach
  leaves the token unproven, so the request is refused with the same code as a forged one, and only the log line and the
  `outcome` tag distinguish an outage.
## 0.3.0 - 2026-09-06
### Added

- `AddD2ForgeryProtection` and `UseD2ForgeryProtection` install cross-site request forgery protection for
  cookie-authenticated mutations. The check reads the routed endpoint's `ID2ForgeryPostureDeclaration`, applies the
  browser's fetch-metadata headers and, where the host registers an `ID2ForgeryTokenVerifier`, a signed token the page
  echoes from a script-readable cookie into `X-CSRF-Token`. A refusal returns 403 with `GEN_FORGERY_REFUSED` and
  re-issues the token cookie when a session resolved. A host that installs the middleware without the registration gets
  an `InvalidOperationException` on the first request rather than an unchecked one.
- `D2ForgeryProtectionOptions` binds the `LIB_ASPNETCORE_FORGERY` configuration section and has `TokenCookieName`
  (`__Host-d2_csrf`) and `TokenHeaderName` (`X-CSRF-Token`), both validated at host build.
- `D2ForgeryPostures`, `D2ForgeryHeaderNames`, `D2ForgeryRefusalReasons`, `D2ForgeryTokenVerdict`,
  `D2ForgeryTokenIssue`, `D2ForgeryTokenState`, `D2ForgeryTokenCookie` and `ForgeryProtectionTelemetry` are the closed
  sets, seams and cookie attributes of that surface.
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.2.1 - 2026-09-04
### Changed

- The `AddD2MutualTls` remarks in `MutualTlsHostExtensions.cs` and the wording of the 0.2.0 entry now state the validator change subject-first. No behavior and no public surface changed.
## 0.2.0 - 2026-09-04
### Changed

- `AddD2MutualTls` no longer stops an enabled host from starting when its `AllowedWorkloads` list is empty. An empty
  list now means admit no peer: the host boots, the Kestrel endpoint keeps `ClientCertificateMode.RequireCertificate`,
  and `SpiffeSanPeerValidator` rejects every presented certificate. A host that previously relied on the throw to catch
  an unset list gets a booting host that admits nobody instead.
- `AddD2MutualTls` rejects an `AllowedWorkloads` entry that is not in canonical form - lowercase with no whitespace -
  and reports the entry and its index. `SpiffeSanPeerValidator` compares each entry ordinally against a SPIFFE service
  id the grammar has already lower-cased, so `Jobs` or ` jobs` admitted nobody and said nothing; that configuration now
  fails at build.
- One `IValidateOptions<D2MutualTlsOptions>` implementation replaces the chained `Validate` predicates on
  `D2MutualTlsOptions`. It collects every offending entry, so one failure lists all of them rather than the first. The
  public surface is unchanged.

## 0.1.9 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Spiffe` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.8 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Headers.Http bumped.
- Dependency update: DcsvIo.D2.ProblemDetails.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Spiffe bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Spiffe bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.ProblemDetails.Abstractions bumped.
