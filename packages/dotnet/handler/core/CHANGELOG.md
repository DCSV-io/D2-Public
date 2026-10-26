<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Handler

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.0 - 2026-10-25
### API-breaking

- `BaseHandler<TSelf, TInput, TOutput>` now requires `TInput : notnull`, the constraint the 0.1.6 entry records. The
  highest version on nuget.org is 0.1.2, which puts no constraint on `TInput`, so 0.2.0 is the first published version
  that has it. Code that uses a nullable reference type such as `string?` as `TInput` reports
  warning CS8714 when nullable annotations are enabled, and the build fails when warnings are treated as errors.
## 0.1.32 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.20. This package's own surface is unchanged.
## 0.1.31 - 2026-10-20
### Changed

- The csproj comment on the `InternalsVisibleTo` grants is rewritten, and one grant moved below it. The set of grants is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.1.30 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.1.29 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.1.28 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.1.27 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.15. This package's own surface is unchanged.
## 0.1.26 - 2026-09-28
### Changed

- The README's Require/Elevate section and the 0.1.25 release note describe the `InternalsVisibleTo` grant as one to a
  private host assembly and its test assembly, and no longer name either assembly. No code changed.
## 0.1.25 - 2026-09-27
### Changed

- The README's Require/Elevate section and the project file comment record the `InternalsVisibleTo` grant to a private
  host assembly as an exception: the grant serves scope elevation in one middleware call that runs before a handler
  exists to declare `ElevateScopes`, and it is not a security boundary because the assemblies are not strong-named. No
  code changed.
- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.1.24 - 2026-09-26
### Changed

- The README describes `HandlerOptions.ElevateScopes` as the whole elevation contract, lists the handler instruments
  without counting them, and no longer mentions a host assembly. The 0.1.23 and 0.1.6 release notes drop the host
  assembly name. No code changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.23 - 2026-09-11
### Added

- `BaseHandlerLog` delegates log the pre-execution refusals that previously emitted no log line: a null input
  (event id 8, `Warning`), `ElevateScopes` declared with no `ScopeRequirement` (event id 9, `Error`), and
  `ElevateScopes` on a request context that is not a `MutableRequestContext` (event id 10, `Error`). The latter two
  are handler-declaration and composition faults, and an operator could not tell either from an ordinary scope denial,
  which shares their `Forbidden` result and their counters. The returned `D2Result` is unchanged for each of these
  refusals.
- `HandlerTelemetry.Tags` contains the span-tag, metric-tag and log-scope keys `BaseHandler` writes, each previously a
  bare literal at both its span-tag and its log-scope site. The nested class is internal; the emitted key strings are
  unchanged.
### Changed

- The README no longer presents `ScopeElevation.Push` as a host-callable mechanism. `HandlerOptions.ElevateScopes` is
  the whole elevation contract a consumer of this package has; `ScopeElevation` stays internal and appears in no
  PublicAPI baseline.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.12. This package's own surface is unchanged.
## 0.1.22 - 2026-09-10
### Changed

- The README states the host requirement that follows from the dropped `ConfigureAwait(false)` calls: the package is
  supported only in a host with no `SynchronizationContext`.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.1.21 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
### Fixed

- `ScopeElevation` keeps a lease count per elevated scope rather than recording only the scopes a lease was the one to
  add. Two leases over one context asking for the same scope both take a claim, so the scope stays in force until the
  last of them is disposed. Previously the second lease recorded nothing, and the first lease's dispose removed the
  scope while the second was still open, leaving that lease unelevated for the rest of its window.
## 0.1.20 - 2026-09-08
### Changed

- `BaseHandler` no longer calls `ConfigureAwait(false)` on either of its awaits. The call changes nothing in a host with
  no `SynchronizationContext`, which is what an ASP.NET Core host runs under. A host that installs one marshals every
  continuation back to it, and a caller that blocks on `HandleAsync` there deadlocks; the package is supported only in
  hosts with no `SynchronizationContext`.
- `ScopeElevation`'s type summary states what the type does and the constraint on it rather than listing the
  assemblies allowed to reach it. The `InternalsVisibleTo` entries in the project file already record that.
- The comment blocks added above the null-input guard and above the elevate lease are cut to the fact a reader needs. No
  behavior changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
### Fixed

- A non-empty `HandlerOptions.ElevateScopes` paired with a null or empty `ScopeRequirement` is refused with `Forbidden`
  before `ExecuteAsync` runs. The pipeline previously applied the elevation whenever `ElevateScopes` was non-empty, so a
  handler declaring elevation with no requirement granted those scopes to whatever caller reached it, for the whole
  execute window and every nested handler's check.
- The `ScopeElevation` lease removes exactly the scopes it added rather than restoring a snapshot of the ambient set.
  Two leases live over one request scope previously interleaved so that the ambient context stayed elevated after both
  were released; each lease now removes only its own contribution, and a scope the ambient set already contained is
  never removed. Both the union and the removal run under a per-context gate, so the read-then-write of `Scopes` cannot
  interleave.
- The null-input refusal records `d2.handler.invoked` and `d2.handler.failed`, as the scope-check and
  non-mutable-elevate refusals do. A null-input refusal was invisible in handler telemetry and both counts silently
  omitted it.
## 0.1.19 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.18 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
## 0.1.17 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
## 0.1.16 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.15 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.14 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.13 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.12 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.11 - 2026-08-06
### Changed

- Dependency update cascade: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.10 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.9 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.8 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.7 - 2026-07-26
### Changed

- `BaseHandler`'s null-input guard now tests `(object?)input is null` instead of
  `input is null`. `notnull` is a compile-time contract only, so a caller can still
  pass `null!` and the runtime guard rejects the null; comparing directly made static
  analysis read the branch as unreachable from the annotation alone. The cast erases
  that annotation without suppressing a diagnostic and without relaxing the
  constraint. Behavior is unchanged - for a value-type input the comparison is
  constant-false exactly as before, and for a reference type it still catches the
  `null!` a caller can pass (pinned by `HandleAsync_NullInput_ReturnsValidationFailed`).
  The guard itself shipped in 0.1.6.
- Removed a conversation-scoped identifier from a `BaseHandler` comment.
- Gallery README revised. No behavioral effect.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.6 - 2026-07-25
### API-breaking

- `BaseHandler<TSelf, TInput, TOutput>` constrains `TInput : notnull` (matches `IHandler`).

### Added

- Internal `ScopeElevation.Push(MutableRequestContext, params string[])` lease
  (restore on dispose; dispose-twice safe).
- `BaseHandler` wires `HandlerOptions.ElevateScopes`: after Require passes, push
  composition scopes for the Execute window; non-mutable ambient fails closed
  Forbidden; lease restores on all exit paths.

### Changed

- Null handler `input` returns `D2Result.ValidationFailed` from `RunCorePipelineAsync`
  before scope checks, LogInput, and `ExecuteAsync` (not `ArgumentNullException`).

## 0.1.5 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.1 - 2026-07-17

### Changed

- Package release 0.1.1 (dependency cascade / baseline reseed; no public API surface change in this release).
