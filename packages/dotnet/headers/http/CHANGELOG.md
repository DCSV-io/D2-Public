<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Headers.Http

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.7 - 2026-10-21
### Changed

- The `X-D2-Internal-Token` description in `contracts/headers/headers.spec.json` now says that no service issues or
  validates this token yet. The generated doc changed; no header name or value changed.
## 0.1.6 - 2026-10-20
### Changed

- The description of the `x-proto-type` header in the shared `headers.spec.json` now gives
  `d2.events.orders.v1.OrderPlacedEvent` as its example. The source files of this package did not change.
## 0.1.5 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.4 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.3 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
## 0.1.2 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
## 0.1.1 - 2026-07-17

### Changed

- Package release 0.1.1 (dependency cascade / baseline reseed; no public API surface change in this release).
