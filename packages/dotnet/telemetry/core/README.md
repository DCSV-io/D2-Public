<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Telemetry

OpenTelemetry SDK setup (traces / metrics / logs) + OTLP exporters (per-signal opt-in via canonical env vars) +
IP-restricted Prometheus scraping endpoint + aggregation of every shared lib's `ActivitySource` and `Meter` into a
single `AddD2Telemetry()` call. A base library that composition-root hosts call to wire the OTel SDK, so each service
does not duplicate the `OpenTelemetryBuilder` / `ConfigureResource` / per-instrumentation registration boilerplate.

The lib is independent of `DcsvIo.D2.Logging` by design. Telemetry and Logging work together at runtime through the MEL
bridge - Serilog's `writeToProviders: true` (set by `AddD2Logging`) routes through the OTLP log exporter that
`AddD2Telemetry` registers as an `ILoggerProvider` - and neither lib references the other at compile time. Hosts may
wire one without the other.

The lib does NOT include:

- Serilog configuration / sinks - `DcsvIo.D2.Logging` owns them.
- The `[RedactData]` enforcement pipeline - `DcsvIo.D2.Logging` owns it.
- `[LoggerMessage]` source-generated delegates - bootstrapping the OTel logs MEL provider during MEL bootstrap is
  circular.
- Self-monitoring counters for export failures - the OTel SDK's own internal diagnostics are enough for the base
  tier; consumers can wire `EventListener` against the SDK if lib-internal export-failure visibility is needed.

## Install

```bash
dotnet add package DcsvIo.D2.Telemetry
```

## Public API surface

### Composition

```csharp
services.AddD2Telemetry(configuration, opts =>
{
    opts.ServiceName = "orders";                     // optional - defaults from OTEL_SERVICE_NAME -> IHostEnvironment.ApplicationName
    opts.OtlpTracesEndpoint = "https://otlp.example.com/v1/traces";  // optional - defaults from OTEL_EXPORTER_OTLP_TRACES_ENDPOINT
    opts.AdditionalActivitySources = ["YourHost.Service"];    // optional - service-specific activity sources
});

// later, inside the endpoint-routing pipeline:
app.MapD2PrometheusEndpoint();
```

`AddD2Telemetry(IConfiguration, Action<D2TelemetryOptions>?)` registers the OpenTelemetry SDK builder, the OTLP
exporters whose corresponding endpoint env vars are truthy, the in-process Prometheus exporter (when
`EnablePrometheusExporter` is true), and the standard auto-instrumentations (AspNetCore, HttpClient, GrpcNetClient,
Process, Runtime). Validates options at the first `IOptions<D2TelemetryOptions>.Value` resolution via
`ValidateOnStart()` - fail-fast on invalid config.

`MapD2PrometheusEndpoint()` maps `/metrics` via OpenTelemetry's `MapPrometheusScrapingEndpoint`, attaches an endpoint
filter that returns `403 Forbidden` for requests whose connection-remote IP is neither loopback nor RFC 1918 private.

### `OTEL_SDK_DISABLED` short-circuit

When the canonical OpenTelemetry env var `OTEL_SDK_DISABLED` is set to `"true"` (case-insensitive), BOTH
`AddD2Telemetry` and `MapD2PrometheusEndpoint` short-circuit:

- `AddD2Telemetry` returns the unmutated `services` collection - no providers / exporters / instrumentations registered.
- `MapD2PrometheusEndpoint` returns the unmutated `endpoints` builder - no `/metrics` route mapped.

Consumers MUST NOT rely on `MeterProvider` / `TracerProvider` resolution from DI after `AddD2Telemetry` returns when the
env var is set. E2E tests set it where the OTel SDK is undesired (the SDK's per-process singleton state otherwise
leaks into tests that build multiple hosts in sequence).

### A service's own meter - `AddD2Meter`

```csharp
services.AddD2Meter<OrderMetrics>(OrderMetrics.METER_NAME);
```

`AddD2Meter<TMetrics>(meterName)` registers `TMetrics` as a singleton, registers the `IMeterFactory` it builds its
instruments from, and adds the meter name to `AdditionalMeters` - in one statement. A service's meter is both created
and subscribed to only through this call: a metrics type registered any other way emits into a process where nothing
subscribes to its meter, and no exporter ever sees its counters.

The write goes to this package's own options and never to the OpenTelemetry SDK, so a library still publishes its meter
name as a constant instead of taking an SDK registration (ADR-0006, and item 2 of ADR-0010). `AddD2Telemetry` reads
`AdditionalMeters` when the meter provider is built, so a host may make those calls in either order; under
`OTEL_SDK_DISABLED` no provider is registered and nothing uses the name. The call is idempotent - the singleton goes in
through `TryAdd` and the names are a set.

Pass the type's own meter-name constant, so a rename of that constant is a compile error at the registration. Call it
from the composition layer that owns the module and not from its domain, application or client layer: this package's
reference closure includes the OTel SDK, and those layers must not reference it.

### Configuration - `D2TelemetryOptions`

| Property                             | Type                    | Default                                                   | Notes                                                                                                                                                                                                                                                                                                                                                               |
| ------------------------------------ | ----------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ServiceName`                        | `string?`               | `OTEL_SERVICE_NAME` -> `IHostEnvironment.ApplicationName` | `service.name` resource attribute on every span / metric / log record. Validated non-empty / non-whitespace at startup.                                                                                                                                                                                                                                             |
| `OtlpTracesEndpoint`                 | `string?`               | `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` config               | Falsey value suppresses traces OTLP exporter; spans still emit to in-process listeners. Validated as an absolute http or https URL when truthy.                                                                                                                                                                                                                     |
| `OtlpMetricsEndpoint`                | `string?`               | `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` config              | Falsey value suppresses metrics OTLP exporter; Prometheus scraping remains active when enabled. Validated as an absolute http or https URL when truthy.                                                                                                                                                                                                             |
| `OtlpLogsEndpoint`                   | `string?`               | `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` config                 | Falsey value suppresses logs OTLP exporter; logs still flow through other MEL providers. Validated as an absolute http or https URL when truthy.                                                                                                                                                                                                                    |
| `InstrumentationExcludedPaths`       | `IReadOnlyList<string>` | `["/health", "/alive", "/metrics", "/.well-known"]`       | Path prefixes excluded from AspNetCore-instrumentation auto-spans (matches `DcsvIo.D2.Logging`'s default infrastructure-path set so spans and logs stay aligned). Validated non-empty (collection) and per-entry non-empty / non-whitespace at startup.                                                                                                             |
| `AdditionalActivitySources`          | `IReadOnlyList<string>` | `[]`                                                      | Names of additional `ActivitySource`s registered with the tracer provider on top of the standard aggregation set. Validated per-entry non-empty when populated.                                                                                                                                                                                                     |
| `AdditionalMeters`                   | `ISet<string>`          | `[]`                                                      | Names of additional `Meter`s registered with the meter provider on top of the standard aggregation set. Mutable, so a configure callback adds to it; a name added twice is one name. A `with`-copy shares the same set instance, so a copy that is to hold a different meter set must assign a new set. Read when the meter provider is built. Validated per-entry. |
| `EnableAspNetCoreInstrumentation`    | `bool`                  | `true`                                                    | Inbound HTTP request spans + metrics.                                                                                                                                                                                                                                                                                                                               |
| `EnableHttpClientInstrumentation`    | `bool`                  | `true`                                                    | Outbound HttpClient spans + metrics. Self-referential filter suppresses spans for outbound calls to the configured OTLP exporter endpoints (prevents infinite-loop instrumentation).                                                                                                                                                                                |
| `EnableGrpcNetClientInstrumentation` | `bool`                  | `true`                                                    | Outbound gRPC client spans.                                                                                                                                                                                                                                                                                                                                         |
| `EnableProcessInstrumentation`       | `bool`                  | `true`                                                    | Process metrics (CPU, memory, fd count).                                                                                                                                                                                                                                                                                                                            |
| `EnableRuntimeInstrumentation`       | `bool`                  | `true`                                                    | .NET runtime metrics (GC, threadpool, JIT).                                                                                                                                                                                                                                                                                                                         |
| `EnablePrometheusExporter`           | `bool`                  | `true`                                                    | In-process Prometheus exporter; off when host wants OTLP-only metrics push.                                                                                                                                                                                                                                                                                         |

### Constants

| Constant                                                | Value                                   |
| ------------------------------------------------------- | --------------------------------------- |
| `D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY`     | `"OTEL_SERVICE_NAME"`                   |
| `D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY`  | `"OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"`  |
| `D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY` | `"OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"` |
| `D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY`    | `"OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"`    |
| `D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR`        | `"OTEL_SDK_DISABLED"`                   |
| `D2TelemetryConstants.PROMETHEUS_ENDPOINT_PATH`         | `"/metrics"`                            |
| `D2TelemetryConstants.HEALTH_ENDPOINT_PATH`             | `"/health"`                             |
| `D2TelemetryConstants.ALIVE_ENDPOINT_PATH`              | `"/alive"`                              |
| `D2TelemetryConstants.WELL_KNOWN_ENDPOINT_PATH`         | `"/.well-known"`                        |

The `OTEL_SERVICE_NAME_CONFIG_KEY` value matches `DcsvIo.D2.Logging`'s constant of the same name - both reference the
OpenTelemetry-canonical env var so log + trace + metric service names stay aligned. Re-declared here rather than
depending on `DcsvIo.D2.Logging`, which this lib is independent of.

## Telemetry surface coverage

`AddD2Telemetry` aggregates shared-lib `ActivitySource` / `Meter` wire names into the configured exporters. Package
owners bind via published `const string` symbols (compile-time rename safety). Optional host auth modules may also emit
under reserved OTel wire names registered as **literals only** (no ProjectReference into host packages from this lib).

> **Optional host auth meters:** wire names `"DcsvIo.D2.Auth"` / `"DcsvIo.D2.Auth.Outbound"` are AssemblyName / OTel
> series labels for hosts that ship auth runtime modules. Public Telemetry registers those names as string literals so
> dashboards stay continuous; it does not ProjectReference host auth packages.

### `ActivitySource` aggregation (tracer provider)

| Wire / owning surface          | Source name                    | Binding in public Telemetry             |
| ------------------------------ | ------------------------------ | --------------------------------------- |
| `DcsvIo.D2.Handler` (public)   | `DcsvIo.D2.Handler`            | symbol `HandlerTelemetry.SourceName`    |
| Host auth runtime (optional)   | `DcsvIo.D2.Auth`               | wire literal `AUTH_WIRE_NAME`           |
| Host auth outbound (optional)  | `DcsvIo.D2.Auth.Outbound`      | wire literal `AUTH_OUTBOUND_WIRE_NAME`  |
| `DcsvIo.D2.Messaging.RabbitMq` | `DcsvIo.D2.Messaging.RabbitMq` | symbol `MessagingTelemetry.SOURCE_NAME` |

### `Meter` aggregation (meter provider)

| Wire / owning surface                   | Meter name                            | Binding in public Telemetry             |
| --------------------------------------- | ------------------------------------- | --------------------------------------- |
| `DcsvIo.D2.Handler` (public)            | `DcsvIo.D2.Handler`                   | symbol `HandlerTelemetry.SourceName`    |
| Host auth runtime (optional)            | `DcsvIo.D2.Auth`                      | wire literal `AUTH_WIRE_NAME`           |
| Host auth outbound (optional)           | `DcsvIo.D2.Auth.Outbound`             | wire literal `AUTH_OUTBOUND_WIRE_NAME`  |
| `DcsvIo.D2.Messaging.RabbitMq` (public) | `DcsvIo.D2.Messaging.RabbitMq`        | symbol `MessagingTelemetry.SOURCE_NAME` |
| `DcsvIo.D2.Caching.Distributed.Redis`   | `DcsvIo.D2.Caching.Distributed.Redis` | symbol `RedisCacheTelemetry.METER_NAME` |
| `DcsvIo.D2.Caching.Local`               | `DcsvIo.D2.Caching.Local`             | symbol `LocalCacheTelemetry.METER_NAME` |

`Caching.Distributed.Redis` and `Caching.Local.Default` publish counters only - no spans - so they appear in the meter
list but not the activity-source list. ProjectReferenced libs use source-of-truth `const string` symbols so a rename
surfaces as a build break. Optional host auth names are **literals**. Spec-pinning unit tests in
`AggregatedTelemetrySourcesTests` and `D2TelemetryConstantsTests` pin the literal wire values, so changing a value in
place fails a test. Operators who query Tempo / Prometheus by a literal source name (`DcsvIo.D2.Auth`) are therefore
protected from silent wire-format drift.

### Auto-instrumentations

| Instrumentation        | Default | Notes                                                                                                                                                                                                                                                                                                                                                 |
| ---------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AspNetCore inbound     | enabled | Filter callback excludes `/health`, `/alive`, `/metrics`, `/.well-known` (configurable via `InstrumentationExcludedPaths`). `RecordException = true` captures exception type + stack trace as span events (no PII - diagnostic-class data).                                                                                                           |
| HttpClient outbound    | enabled | `FilterHttpRequestMessage` callback suppresses spans for outbound calls whose URI starts with any configured OTLP endpoint (prevents infinite-loop instrumentation). `EnrichWithHttpRequestMessage` re-sets `url.full` to scheme + host + port + path (no query string) as defense-in-depth against SDK regressions that would leak query-string PII. |
| GrpcNetClient outbound | enabled | Standard span tags.                                                                                                                                                                                                                                                                                                                                   |
| Process metrics        | enabled | CPU, memory, fd count.                                                                                                                                                                                                                                                                                                                                |
| Runtime metrics        | enabled | GC, threadpool, JIT.                                                                                                                                                                                                                                                                                                                                  |
| Prometheus exporter    | enabled | In-process scraping endpoint mapped via `MapD2PrometheusEndpoint`.                                                                                                                                                                                                                                                                                    |

## Dependencies

| Package                                        | Why                                                                                                                                                                             |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OpenTelemetry.Api`                            | Pinned at `1.15.3` - targeted override per GHSA-g94r-2vxg-569j on the 1.15.0 line. Direct ref required to override the transitive 1.15.0 pulled by Instrumentation.\* packages. |
| `OpenTelemetry`                                | Core SDK (`OpenTelemetryBuilder`, `BatchExportProcessor`, etc.).                                                                                                                |
| `OpenTelemetry.Extensions.Hosting`             | `services.AddOpenTelemetry()` extension + `WithTracing` / `WithMetrics` overloads.                                                                                              |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | Pinned at `1.15.3` - targeted override per GHSA-mr8r-92fq-pj8p + GHSA-q834-8qmm-v933. OTLP HTTP/Protobuf exporter for traces / metrics / logs.                                  |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | Pinned at `1.15.0-beta.1` - long-running beta in OTel .NET; >18mo of v1 production stability supports the pin. Required for `MapPrometheusScrapingEndpoint`.                    |
| `OpenTelemetry.Instrumentation.AspNetCore`     | Inbound HTTP request spans + metrics.                                                                                                                                           |
| `OpenTelemetry.Instrumentation.GrpcNetClient`  | Pinned at `1.15.0-beta.1`. Outbound gRPC client spans.                                                                                                                          |
| `OpenTelemetry.Instrumentation.Http`           | Outbound HttpClient spans + metrics.                                                                                                                                            |
| `OpenTelemetry.Instrumentation.Process`        | Pinned at `1.15.0-beta.1`. Process metrics.                                                                                                                                     |
| `OpenTelemetry.Instrumentation.Runtime`        | .NET runtime metrics.                                                                                                                                                           |
| `JetBrains.Annotations`                        | `[MustDisposeResource]` annotations on disposable factory paths (none currently; consumed transitively).                                                                        |

| PackageId                             | Why                                                                                                                                                                                                                                                                                                                               |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Utilities`                 | `Falsey()` / `Truthy()` / `ToNullIfEmpty()` extensions for options validation + env-var resolution.                                                                                                                                                                                                                               |
| `DcsvIo.D2.AspNetCore`                | Canonical `InfrastructurePathMatcher` (consumed by the AspNetCore-instrumentation `Filter` callback) + `D2AspNetCoreConstants` (re-exported HEALTH / ALIVE / METRICS / WELL_KNOWN endpoint path constants + `DEFAULT_INFRASTRUCTURE_PATHS`). Single source of truth shared with `DcsvIo.D2.Logging`'s request-logging middleware. |
| `DcsvIo.D2.Handler`                   | `HandlerTelemetry.SourceName` const referenced by the aggregation table.                                                                                                                                                                                                                                                          |
| `DcsvIo.D2.Messaging.RabbitMq`        | `MessagingTelemetry.SOURCE_NAME` const.                                                                                                                                                                                                                                                                                           |
| `DcsvIo.D2.Caching.Distributed.Redis` | `RedisCacheTelemetry.METER_NAME` const.                                                                                                                                                                                                                                                                                           |
| `DcsvIo.D2.Caching.Local.Default`     | `LocalCacheTelemetry.METER_NAME` const.                                                                                                                                                                                                                                                                                           |

**Not referenced (wire literals only):** the auth runtime and outbound sources. Telemetry registers the OTel wire names
`"DcsvIo.D2.Auth"` / `"DcsvIo.D2.Auth.Outbound"` as string literals - see `AggregatedTelemetrySources.AUTH_WIRE_NAME` /
`AUTH_OUTBOUND_WIRE_NAME`.

## Edge cases / gotchas

- **OTel SDK builds singleton `MeterProvider` / `TracerProvider` per process.** Tests that build multiple hosts in
  sequence see static SDK state carried over between host lifetimes. The lib's integration tests use
  `[Collection("OtelStaticState")]` to run serially against any other test that touches the SDK.
- **Infrastructure-path matching lives in `DcsvIo.D2.AspNetCore`.** The AspNetCore-instrumentation `Filter` callback
  consumes the public `InfrastructurePathMatcher`; `DcsvIo.D2.Logging`'s request-logging middleware consumes the same
  one. The path set (`/health`, `/alive`, `/metrics`, `/.well-known`) stays aligned across both without per-lib literal
  duplication.
- **Cache libs (`Redis`, `Local`) expose only their `Meter` - no spans.** The libs publish aggregate counters (hits,
  misses, sets, evictions) without per-call spans because cache work is sub-microsecond and per-call instrumentation
  would dominate it. The aggregation table reflects this - meter list includes both cache libs; activity-source list
  does not.
- **`MessagingTelemetry`, `RedisCacheTelemetry`, and `LocalCacheTelemetry` are `public static` so their `SOURCE_NAME`
  consts are reachable cross-assembly.** The consts themselves are `public`; the surrounding class visibility matches so
  cross-assembly references compile.
- **Per-signal OTLP exporter is registered ONLY when its endpoint env var is truthy.** With no endpoint set, the SDK
  still builds - spans emit to in-process listeners; metrics flow to the Prometheus endpoint when enabled; logs flow
  through other MEL providers. A deployment sets the endpoint of each signal it exports, and a signal whose endpoint is
  absent is skipped without error.
- **HttpClient instrumentation's self-referential filter prevents infinite-loop instrumentation.** Outbound calls whose
  URI starts with any configured OTLP endpoint are suppressed from span emission (else
  `HttpClient -> OTLP -> HttpClient -> OTLP` loops forever).
- **`url.full` query-string stripping is defense-in-depth.** The OTel 1.15.x HttpClient instrumentation default already
  strips query strings from the standard span tags, but the `EnrichWithHttpRequestMessage` callback explicitly re-sets
  `url.full` to scheme + host + port + path so an SDK regression cannot silently leak query-string PII.
- **OTLP collector unavailable -> SDK retries internally + drops on full buffer.** The OTel SDK's `BatchExportProcessor`
  implements the retry / drop behavior; the lib does not add another layer. Operators see unavailability as missing data
  in dashboards (the SDK's own internal diagnostics + `EventListener` against `OpenTelemetry.EventSource` provide
  drop-rate visibility for operators who wire it).
