<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Telemetry

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.19-beta.1 - 2026-10-25
### Changed

- The `WebApplicationTelemetryExtensions` remark on the private-range filter is rewritten as two sentences that state
  what the filter admits and where to serve the endpoint. No code changed.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.2.0. That release is API-breaking; this package's own surface is
  unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.7. This package's own surface is unchanged.
## 0.2.18-beta.1 - 2026-10-21
### Changed

- The remarks of `WebApplicationTelemetryExtensions` and `InternalIpFilter` say that the filter admits any directly
  connected loopback or private-range peer, and `WebApplicationTelemetryExtensions` says that a host an untrusted
  private network can connect to serves `/metrics` on a port or address that network cannot connect to. No code
  changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.32. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.17-beta.1 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.31. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.2.16-beta.1 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.30. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.2.15-beta.1 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.29. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.2.14-beta.1 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.28. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.2.13-beta.1 - 2026-10-09
### Fixed

- `AddD2Telemetry` startup validation now rejects an `OtlpTracesEndpoint`, `OtlpMetricsEndpoint` or `OtlpLogsEndpoint`
  that is not an absolute `http` or `https` URL. It accepted any absolute URI before, so on Linux and macOS a rooted
  path such as `/v1/traces` passed as `file:///v1/traces`, and a `file:` or `ftp:` URL passed on every OS. The
  validation message now says `must be an absolute http or https URL when set`.
## 0.2.12-beta.1 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.27. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.1. This package's own surface is unchanged.
## 0.2.11-beta.1 - 2026-09-28
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.26. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.5.0. This package's own surface is unchanged.
## 0.2.10-beta.1 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.25. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.2.9-beta.1 - 2026-09-26
### Changed

- The README opening sentence no longer states a line count. No code changed.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.5. This package's own surface is unchanged.
## 0.2.8-beta.1 - 2026-09-26
### Changed

- The README describes the per-signal exporter behavior without an endpoint count, wraps the `AddD2Meter` paragraphs,
  and describes the auth wire-name literals without naming host packages. No code changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.24. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.2.7-beta.1 - 2026-09-26
### Changed

- The internal `OTEL_SDK_DISABLED` gate can be pinned to a value for one asynchronous flow, so this package's tests no
  longer read or write the process environment to control it. With no pin in place the gate reads the environment
  variable as before.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.3. This package's own surface is unchanged.
## 0.2.6-beta.1 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.2. This package's own surface is unchanged.
## 0.2.5-beta.1 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.1. This package's own surface is unchanged.
## 0.2.4-beta.1 - 2026-09-14
### Changed

- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.4.0. This package's own surface is unchanged.
## 0.2.3-beta.1 - 2026-09-13
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.3. This package's own surface is unchanged.
## 0.2.2-beta.1 - 2026-09-11
### Changed

- `D2TelemetryOptions.AdditionalMeters` is a mutable set behind an init-only property, so a `with`-copy shares the same
  set instance as its source and an add through either is seen through both. The type xmldoc, the property xmldoc and
  the README say so, and say that a copy intended to hold a different meter set must assign a new set.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.23. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.13. This package's own surface is unchanged.
### Fixed

- The README's `AddD2Meter` example used a product type that no reader of the public package or the NuGet package has.
  It now uses a portable placeholder metrics type and its own meter-name constant.
## 0.2.1-beta.1 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.22. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.2.0-beta.1 - 2026-09-10
### API-breaking

- `D2TelemetryOptions.AdditionalMeters` is an `ISet<string>` instead of an `IReadOnlyList<string>`, and starts as an
  empty `HashSet` with ordinal comparison. The property stays init-only; its contents are now mutable, so a `configure`
  callback or an `IConfigureOptions<D2TelemetryOptions>` can add a meter name after the container has constructed the
  options instance, and a name added twice is one name. A caller that assigned an array through an object initializer
  passes a set instead; a caller that read the property as a list reads a set. The per-entry non-empty validation is
  unchanged.
### Added

- `AddD2Meter<TMetrics>(string meterName)` - registers a service's metrics type as a singleton, registers the
  `IMeterFactory` it is built from, and adds its meter name to `AdditionalMeters` in one statement. A service's meter
  is both created and subscribed to only through this call: registering the type and subscribing its meter
  used to be separate calls, and a metrics type registered without the second one emits into a process where nothing
  subscribes to its meter. The write goes to this package's own options rather than to the OpenTelemetry SDK, so
  ADR-0006 and item 2 of ADR-0010 still apply.
### Changed

- `AddD2Telemetry` reads `AdditionalMeters` when the meter provider is built rather than from the snapshot it took at
  registration time. A meter name added through the options pipeline now reaches the provider regardless of call order;
  previously only a name assigned by the `configure` callback did, and only because the snapshot invoked that callback
  itself.
## 0.1.39-beta.1 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.11. This package's own surface is unchanged.
## 0.1.38-beta.1 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.4.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.1.37-beta.1 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.4.0. This package's own surface is unchanged.
## 0.1.36-beta.1 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.3.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.1.35-beta.1 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.34-beta.1 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.2.1. This package's own surface is unchanged.
## 0.1.33-beta.1 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.2.0. That release changes `AddD2MutualTls` validation: an empty
  `AllowedWorkloads` list now admits no peer instead of failing the host build, and an entry that is not lowercase with
  no whitespace is refused at build. This package's own surface is unchanged.
## 0.1.32-beta.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Handler` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.31-beta.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Caching.Local.Default` bumped to 0.4.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.6. This package's own surface is unchanged.
## 0.1.30-beta.1 - 2026-09-03
### Changed

- Depends on the caching abstractions at the advanced version; no content change in this package.
- Dependency update: `DcsvIo.D2.Caching.Distributed.Redis` bumped to 0.4.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.5. This package's own surface is unchanged.
## 0.1.29-beta.1 - 2026-09-02
### Changed

- Depends on the caching packages at their 2026-09-02 patch (the token-bucket
  refill keeps its accrual remainder); no content change in this package.

## 0.1.28-beta.1 - 2026-09-02
### Changed

- Depends on the caching packages at 0.4.0 (`ConsumeTokenAsync` added to the atomic contract); no content change in this package.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.3. This package's own surface is unchanged.
## 0.1.27-beta.1 - 2026-09-02
### Changed

- Depends on its caching dependencies at the advanced version; no content change in this package.
- Dependency update: `DcsvIo.D2.Messaging.RabbitMq` bumped to 0.3.2. This package's own surface is unchanged.
## 0.1.26-beta.1 - 2026-08-25
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.25-beta.1 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.24-beta.1 - 2026-08-20
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.23-beta.1 - 2026-08-20
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.22-beta.1 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.21-beta.1 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.20-beta.1 - 2026-08-18
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- No source in this package changed; the release fingerprint includes the declared version of each direct consumable
  dependency, so the bump changes it.
## 0.1.19-beta.1 - 2026-08-18
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- No source in this package changed; the release fingerprint includes the declared version of each direct consumable
  dependency, so the bump changes it.
## 0.1.18-beta.1 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.17-beta.1 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- No source in this package changed; the release fingerprint includes the declared version of each direct consumable
  dependency, so the bumps change it.
- Dependency update: DcsvIo.D2.Handler bumped.
## 0.1.16-beta.1 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.15-beta.1 - 2026-08-09

### Fixed

- Restored the prerelease version this package needs to pack at all. The version had changed to a stable value, and
  `dotnet pack` failed NU5104 because the OpenTelemetry `PackageReference`s pinned in `Directory.Packages.props`
  (`Exporter.Prometheus.AspNetCore`, `Instrumentation.GrpcNetClient`, `Instrumentation.Process`) publish beta builds
  only, which a stable package may not declare as dependencies. Only the version changed; no source, dependency or build
  setting changed.

## 0.1.14 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.13 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.12 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.11 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.10 - 2026-08-03
### Changed

- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.9 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.8 - 2026-07-26
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-07-25
### Changed

- Dependency / fingerprint refresh after DcsvIo.D2.Handler null-input ValidationFailed pipeline (no public API change in
  this package).
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.6 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.5 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Caching.Local.Default bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
## 0.1.3-beta.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Remains **prerelease** (NU5104) while OpenTelemetry Prometheus / GrpcNetClient / Process instrumentations stay non-stable.
## 0.1.2-beta.1 - 2026-07-17
### Changed

- Package version is **prerelease** so NuGet pack is legal while three OpenTelemetry
  dependencies remain non-stable (`OpenTelemetry.Exporter.Prometheus.AspNetCore`,
  `OpenTelemetry.Instrumentation.GrpcNetClient`, `OpenTelemetry.Instrumentation.Process`
  - beta/rc only on nuget.org; NU5104 blocks a stable `DcsvIo.D2.Telemetry` version). Move to a stable version when
  those packages ship stable builds. Core is **0.1.2** (not 0.1.1-beta) so the repository's release version check, which
  requires a PATCH increase over the prior `0.1.1`, is met by a prerelease of the required core.
- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Caching.Distributed.Redis bumped.
- Dependency update: DcsvIo.D2.Handler bumped.
- Dependency update: DcsvIo.D2.Messaging.RabbitMq bumped.
