<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-messaging-rabbitmq

All notable changes to this package are documented here. Versions follow the
per-package semver + build-free-diff convention.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Changed

### Fixed

## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption bumped.
- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
- Dependency update: @dcsv-io/d2-headers-amqp bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-messaging-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-telemetry bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.0

Initial release: the service-agnostic RabbitMQ **consumer** runtime — the
TypeScript twin of the .NET `DcsvIo.D2.Messaging.RabbitMq` consumer path.

- `subscribe` / `createConnection` public surface (consumer-only; no publisher).
- Topology declaration matching the .NET contract exactly (primary + `{q}.dlx`
  + `{q}.dlq` + optional retry tiers), byte-identical `DlqNaming`.
- Manual-ack consume with the DLQ republish-with-`DlqFailureMetadata`-then-ack
  failure path (+ NACK-no-requeue fallback).
- The precise 5-point idempotency contract over an `IMessageIdempotencyStore`
  seam (+ in-memory 24h-TTL impl).
- Consume-side context establishment: traceparent-parented `Consumer`-kind span
  + `x-d2-context` (base64url-of-JSON) decoded and applied onto a fresh
  per-message context (identity + `RequestOrigin` never taken from the wire).
- Injectable body-decompose seam (plaintext now; encrypted body with no
  registered opener → fail-loud DLQ `DECRYPT_FAILURE`).
- Reconnect (rabbitmq-client auto-recovery + aux-topology re-declaration).
