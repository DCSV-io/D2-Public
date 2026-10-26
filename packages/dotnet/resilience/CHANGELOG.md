<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Resilience

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.17 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.1.16 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.1.15 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.1.14 - 2026-10-17
### Changed

- Punctuation in the `ArgumentOutOfRangeException` message of `RateLimiterOptions` for a `MaxConcurrency` below 1:
  a dash is now a hyphen.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.1.13 - 2026-09-27
### Changed

- The README's and the pipeline guide's prose lines over 120 characters are re-wrapped at that width; no wording
  changed. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.1.12 - 2026-09-26
### Changed

- The README decision table is re-aligned and its "see below" reference is an anchor link. No code changed.
## 0.1.11 - 2026-09-26
### Changed

- The README opener lists the mechanism's layers without a marketing adjective, and all-caps emphasis in the README
  and the pipeline guide is sentence case. The pipeline guide's examples use generic service names, and its unlabelled
  fenced blocks have a `text` tag. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.10 - 2026-09-26
### Changed

- README: the circuit-breaker section describes `CircuitBreaker<T>.FailureCount`, and the source map lists `Build()`
  on `IResilientPipelineBuilder<TKey, TValue>` and `ResilientPipelineBuilder<TKey, TValue>`. Two British spellings of
  "cancelable" now use the American form. No code changed.
## 0.1.9 - 2026-09-10
### Changed

- The packed README contains no outbound link and refers to the pipeline document by name instead: the registry
  renderer cannot resolve a relative repository path.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.1.8 - 2026-09-08
### Changed

- The README coverage recipe uses `dotnet test --collect` and a report renderer a consumer has, rather than a tool that ships in no package.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.1.7 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.4 - 2026-08-03
### Changed

- Gallery README: the coverage instructions now point at `coverage.mjs` instead of a `coverlet` command line. Coverlet hooks the VSTest MSBuild target, and these suites run on Microsoft.Testing.Platform, which bypasses it - so the documented command silently produced nothing. Punctuation in the same file normalized to keyboard ASCII. Shipped README text only: no source, dependency, target framework or build setting changed.
## 0.1.3 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
