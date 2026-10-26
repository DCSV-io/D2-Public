<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Handler.Analyzers

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.9 - 2026-10-25
### Fixed

- `dotnet pack` no longer fails with NU5128: the project sets `SuppressDependenciesWhenPacking`, so the package declares
  no dependency group, because the analyzer has no run-time dependencies.
## 0.1.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.6 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
## 0.1.5 - 2026-09-11

### Changed

- Public XML documentation and analyzer release tracking are enforced without suppressing CS1591 or RS2008.
- AnalyzerReleases files record the existing diagnostic IDs, categories, and severities. Diagnostic behavior is unchanged.

## 0.1.4 - 2026-09-11
### Changed

- `HandlerScopeAnalyzer.Initialize` has the blank line after its multi-line statement that the code convention calls
  for. No behavior changed.
## 0.1.3 - 2026-09-10
### Changed

- The project silences only CS1591 and RS2008, matching its sibling generator projects. RS1032 and RS1038 were silenced with no reason recorded and neither fires.
### Fixed

- `HandlerScopeAnalyzer` matches the scope-binding marker by its full metadata name rather than by simple name, so an attribute called `D2GeneratedHandlerScopeAttribute` declared in any other namespace no longer satisfies D2HND003. A handler could otherwise look bound while binding no `ScopeRequirement` and taking no generated Scope partial.
## 0.1.2 - 2026-09-08
### Changed

- The README names `HandlerScopeAnalyzer`, the diagnostics it raises and when each fires, and contains the
  configuration, dependency, usage, telemetry and edge-case sections. The internal unit-coverage table listing product
  and fixture type names is removed; it gave a consumer of the analyzer no information.
### Fixed

- `HandlerScopeAnalyzer` requires the candidate `I{Op}Handler` interface to derive from `DcsvIo.D2.Handler.Abstractions.IHandler<TInput, TOutput>`, resolved from the compilation under analysis. A class implementing an unrelated interface of that shape, such as a consumer's own `IRequestHandler` or `ICommandHandler`, was reported D2HND001, D2HND002 and D2HND003 at error severity in any consumer of this package.
- The carve-out is the assembly name matched by exact suffix - `.Tests`, `.UnitTests` or `.IntegrationTests` - and nothing else. The previous carve-out matched `.Tests`, `.Unit` and `.Integration` anywhere in the assembly name and `Fixture`, `Fake`, `Stub`, `Test` or `Sample` anywhere in the type name, so a production handler named `TestConnectionHandler`, or any handler in an assembly whose name contains `Integrations`, shipped with no scope binding and the analyzer reported nothing.
## 0.1.1 - 2026-07-26
### Changed

- Gallery README revised for the package-registry audience (self-contained, no monorepo or product references). Analyzer rules, diagnostics and behavior are unchanged.
## 0.1.0 - 2026-07-26
### Added

- Initial Roslyn analyzer package for handler scope binding as defense-in-depth.
- Rules D2HND001 (implementer name), D2HND002 (must be partial),
  D2HND003 (sealed DefaultOptions or `[D2GeneratedHandlerScope]`).
- Test / Fixture / Fake / Stub / Sample leaf-name carve-out and
  `*.Tests` / `*.Unit` / `*.Integration` assembly carve-out.
