<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-headers-amqp

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.9 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.1.8 - 2026-10-20
### Changed

- The TSDoc example for the `x-proto-type` header in `AmqpHeaders` is now `d2.events.orders.v1.OrderPlacedEvent` in
  place of a type name from the files service. No value changed.
## 0.1.7 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.6 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.5 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
## 0.1.4 - 2026-08-03
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
