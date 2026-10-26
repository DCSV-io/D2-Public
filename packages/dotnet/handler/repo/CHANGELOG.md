<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Handler.Repo

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.0 - 2026-10-25
### API-breaking

- `BaseRepoHandler<TSelf, TInput, TOutput>` now requires `TInput : notnull`, the constraint the 0.1.6 entry records.
  The highest version on nuget.org is 0.1.2, which puts no constraint on `TInput`, so 0.2.0 is the first published
  version that has it. Code that uses a nullable reference type such as `string?` as `TInput` reports
  warning CS8714 when nullable annotations are enabled, and the build fails when warnings are treated as errors.

### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.2.0. That release is API-breaking, with the same `TInput : notnull`
  constraint as the entry above.
## 0.1.32 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.32. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.19. This package's own surface is unchanged.
## 0.1.31 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.31. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.1.30 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.30. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.1.29 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.29. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.1.28 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.28. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.1.27 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.27. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.15. This package's own surface is unchanged.
## 0.1.26 - 2026-09-28
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.26. This package's own surface is unchanged.
## 0.1.25 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.25. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.1.24 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.24. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.23 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.23. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.12. This package's own surface is unchanged.
## 0.1.22 - 2026-09-10
### Changed

- The README states the host requirement that follows from the dropped `ConfigureAwait(false)` call: the package is
  supported only in a host with no `SynchronizationContext`.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.22. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.1.21 - 2026-09-09
### Changed

- `BaseRepoHandler.HandleAsync` no longer calls `ConfigureAwait(false)`. The call changes nothing in a host with no
  `SynchronizationContext`, which is what an ASP.NET Core host runs under, and the base type in `DcsvIo.D2.Handler`
  dropped its own calls in 0.1.20; the two now agree. A host that installs a `SynchronizationContext` marshals every
  continuation back to it, and a caller that blocks on `HandleAsync` there deadlocks; the package is supported only in
  hosts with no `SynchronizationContext`.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
## 0.1.20 - 2026-09-08
### Changed

- The README prose is re-wrapped at 120 characters. No code changed.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.1.19 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.18 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
## 0.1.17 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler.Repo.Abstractions` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.16 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.15 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
## 0.1.14 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.13 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.12 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
## 0.1.11 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.10 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.9 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.8 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.7 - 2026-07-26
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.6 - 2026-07-25
### API-breaking

- `BaseRepoHandler<TSelf, TInput, TOutput>` restates `TInput : notnull`, which it inherits from `BaseHandler` /
  `IHandler`.

### Changed

- Dependency / fingerprint refresh after the DcsvIo.D2.Handler pipeline began returning ValidationFailed for a null
  input.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.

## 0.1.5 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Handler.Abstractions bumped.
- Dependency update: DcsvIo.D2.Handler.Repo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
