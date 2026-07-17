# Changelog — DcsvIo.D2.Telemetry

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

### Changed

### Fixed

## 0.1.3-beta.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Remains **prerelease** (NU5104) while OpenTelemetry Prometheus / GrpcNetClient / Process instrumentations stay non-stable.
## 0.1.2-beta.1 - 2026-07-17
### Changed

- Package version is **prerelease** so NuGet pack is legal while three OpenTelemetry
  dependencies remain non-stable (`OpenTelemetry.Exporter.Prometheus.AspNetCore`,
  `OpenTelemetry.Instrumentation.GrpcNetClient`, `OpenTelemetry.Instrumentation.Process`
  — beta/rc only on nuget.org; NU5104 blocks a stable `DcsvIo.D2.Telemetry` version).
  Graduate when those packages ship stable builds. Core is **0.1.2** (not 0.1.1-beta)
  so version-lockstep’s PATCH floor from prior `0.1.1` is met by a prerelease of the
  required core.
- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.