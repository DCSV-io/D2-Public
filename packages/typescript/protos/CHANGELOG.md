<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-protos

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.8 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.1.7 - 2026-10-22
### Wire-breaking

- The generated modules come from package `d2.common.v2` (`contracts/protos/d2/common/v2/`) in place of `d2.common.v1`:
  `protobufPackage` is `d2.common.v2`, the full message names are `d2.common.v2.*`, and the ping method path is
  `/d2.common.v2.PingService/Ping`. Field names, numbers, types and labels are unchanged.
### Changed

- The generated files move from `src/generated/common/v1/` to `src/generated/d2/common/v2/`; the `D2ResultProto`,
  `TKMessageProto` and `InputErrorProto` exports of the package root are unchanged.
- The `generate` script formats `src/generated/` with `prettier`, added as a devDependency at 3.8.1.
## 0.1.6 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.5 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.4 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
## 0.1.3 - 2026-07-26
### Changed

- Release-baseline reseed only. This package's sources, public API surface and behavior are unchanged; the committed `.release-fingerprint` was stale against a recompute and has been regenerated.
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
