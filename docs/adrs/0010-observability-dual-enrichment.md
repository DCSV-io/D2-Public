<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0010: Observability - OTel aggregation + dual span-tag and log-scope enrichment

- **Status**: Accepted
- **Date**: 2026-05-30

## Context

Every service ships two independent observability signal paths that must contain the same request-context fields
simultaneously: distributed traces to Tempo (via OTLP) and structured log lines to Loki (via Serilog + OTLP). These are
separate backends with separate query languages; a field set only on a span does not appear on a log line, and vice
versa.

The codebase spans multiple shared libraries (`Handler`, host-supplied auth modules when present, `Messaging.RabbitMq`,
`Caching.Distributed.Redis`, `Caching.Local.Default`), each owning its own `ActivitySource` and/or `Meter`. Without
aggregation, each service's composition root has to know the exact string names of every library's source/meter to
subscribe - leaking internal naming across the boundary and silently breaking on any rename. Infrastructure paths
(`/health`, `/alive`, `/metrics`, `/.well-known`) required filtering in two places (logging middleware + tracer
instrumentation), risking divergence.

## Decision

**1. Dual enrichment: span tags AND log scope are set independently per handler invocation.**
`BaseHandler.RunCorePipelineAsync` (ADR-0005) sets OTel span tags on the current `Activity` and opens a Serilog log
scope in two separate, explicit blocks within the same pipeline pass. Tags (`d2.handler.name`, `d2.user_id`,
`d2.org_id`, `d2.org_type`, `d2.org_role`, impersonation context) propagate to Tempo; an overlapping field set passed to
`Logger.BeginScope(...)` (also containing `d2.trace_id`) propagates to every Serilog event inside `ExecuteAsync` -
including through the MEL bridge to the OTLP log exporter. Neither mechanism is treated as a proxy for the other; both
are always exercised. The same dual-write discipline applies at the HTTP boundary: `UseD2RequestLogging` opens a Serilog
diagnostic context enriched by `D2RequestContextEnricher` (projecting the LOG-OK fields from `IRequestContext`), while
OTel AspNetCore instrumentation records spans on the same requests - independent codepaths, and neither includes the
other.

**2. Cross-library `ActivitySource`/`Meter` aggregation via compile-time symbol references with spec-pin drift tests.**
`AggregatedTelemetrySources` is the single place collecting all cross-library telemetry registrations, enumerating the
`ActivitySource` names and the `Meter` names by referencing each owning library's `public const string` directly
(`HandlerTelemetry.SourceName`, `MessagingTelemetry.SOURCE_NAME`, `RedisCacheTelemetry.METER_NAME`, ...).
`AddD2Telemetry` iterates these to register all sources in one call. A rename of any owning `const` causes a compile
error here immediately; separate spec-pin tests assert the literal wire values (catching the distinct failure where a
value drifts without a rename).

**3. Per-signal OTLP exporters are env-var-gated; `OTEL_SDK_DISABLED` symmetrically suppresses all registration.** OTLP
exporters for traces/metrics/logs register independently, each only when its canonical endpoint env var resolves to a
non-falsey URI. Log export runs through `AddSerilog(writeToProviders: true)`, routing Serilog into the OTel
`OpenTelemetryLoggerProvider`. The Prometheus endpoint at `/metrics` (`MapD2PrometheusEndpoint`) is protected by
`InternalIpFilter` (403 for any remote IP not loopback or RFC 1918). `OTEL_SDK_DISABLED=true` short-circuits both
`AddD2Telemetry` and `MapD2PrometheusEndpoint` symmetrically; the HttpClient instrumentation filter suppresses spans for
outbound calls to configured OTLP endpoints, which prevents `HttpClient -> OTLP -> HttpClient` loops.

**4. Telemetry tag constants are codegen'd from the spec** (an instance of ADR-0002). The generator in `telemetry/tags-source-gen/` reads `contracts/telemetry/telemetry.spec.json` and emits per-meter `*TelemetryTags.g.cs` typed constants; cross-spec references resolve via `CrossSpecResolver` (e.g. the `d2.auth.problem.emitted` tag values are sourced from `auth-error-codes.spec.json`, not duplicated). Hand-written tag-name literals are forbidden for any tag with a spec entry.

**5. One canonical `InfrastructurePathMatcher`** (`DcsvIo.D2.AspNetCore`) is the single source of truth for the infra path set. The telemetry instrumentation `Filter` and the logging `UseD2RequestLogging` level callback both call the same static matcher with the same default path list, so they cannot diverge.

**6. The `Meter` object is an instance; the name is a `const`.** A service's metrics type is a sealed class built from
`IMeterFactory` and registered as a singleton, not a static holder of static instruments. Decision item 2 (cross-library
aggregation by symbol reference) holds: `METER_NAME` is a `public const string` that `AggregatedTelemetrySources` and a
host registration reference by symbol, and the tag catalogs are static consts. The instrument objects are per container,
so a test builds its own metrics instance and reads it through a `MetricCollector` bound to that container's factory
instead of reading a process-wide instrument every other test also writes to.

**7. One registration call for a service meter.** `AddD2Meter<TMetrics>(meterName)` registers the metrics type and adds
its meter name to `D2TelemetryOptions.AdditionalMeters` in one statement, and is the only registration of a service's
metrics type. A type registered without subscribing its meter emits to a meter no provider subscribes, and the single
call prevents that. `AdditionalMeters` is a mutable set for that reason, and `AddD2Telemetry` reads it when the meter
provider is built rather than from a registration-time snapshot, so a host may make those calls in either order. The
seam writes to this package's own options and never to the SDK, so item 2 and ADR-0006 hold: libraries publish names as
constants, and aggregation keeps SDK knowledge in one place. The call belongs in the host's composition root, because
this package's dependency closure includes the OTel SDK and a library that references it takes that closure too.

## Consequences

**Positive.**

- Operators correlate handler-level context (user, org, impersonation) in Tempo traces and Loki logs independently, without a cross-backend join; both signals are always populated.
- A library rename of a telemetry `const` is a build break at `AggregatedTelemetrySources`, not a silent dashboard gap found post-deploy.
- Infra path filtering - the noisiest probe traffic - is suppressed identically in logging and tracing from one definition.
- The Prometheus endpoint is inaccessible from public addresses by construction; no per-deployment firewall rule needed for that surface.
- `OTEL_SDK_DISABLED=true` gives test harnesses a complete no-op with no dangling providers or routes.

**Negative / risks.**

- Dual-write constructs two overlapping data structures (Activity tags + log-scope dictionary) per invocation - a modest allocation that auto-instrumentation-only approaches avoid.
- `AggregatedTelemetrySources` is a manual registry: adding a new library's telemetry needs a PR there, and forgetting
  produces no compile error - only missing dashboards (partially mitigated by the spec-pin presence tests, which must be
  updated in the same PR). OpenTelemetry registers public library sources/meters by symbol; hosts may also register
  additional ActivitySource / Meter wire names for host-supplied auth modules.
- `writeToProviders: true` means each Serilog line is processed twice (Serilog pipeline + MEL->OTel bridge); operators must size the OTLP log collector accordingly.
- `InternalIpFilter` uses byte-prefix matching with no `IPNetwork` abstraction; IPv6 private ranges other than `::1` are out of scope for this filter (deliberate boundary; IPv6-primary pod networks would need a separate design).

## Alternatives considered

**Rely on auto-instrumentation only.** AspNetCore auto-instrumentation emits HTTP-level span attributes but not application context (user/org/impersonation), which the instrumentation library cannot see. Filtering traces by authenticated subject would require a custom processor, re-introducing most of the dual-enrichment code.

**Per-library telemetry registration (each lib calls `AddOpenTelemetry()` itself).** Removes the aggregation layer but
takes an OTel SDK dependency into every library, lets whichever library registers first initialize the SDK, and requires
library authors to configure export, which they should not do. Aggregation keeps SDK/export knowledge in one place and
keeps libraries free of export concerns (consistent with ADR-0006).

**Set span tags only; derive logs from span export.** Some stacks generate logs from spans, reducing dual-write. D2's
Loki lines contain the LOG-OK `IRequestContext` fields (the list is in `D2RequestContextEnricher.cs`), which a span
contains only partly; a log-from-span transform for every field relies on infrastructure outside the application
boundary. Explicit log scope is more predictable and self-contained.

**Hand-written tag-name constants per library.** A plain `Tags` class is straightforward but creates two independent maintenance surfaces (the constant + the dashboard query referencing the wire value). Spec-driven codegen (ADR-0002) gives compile-time symbol safety *and* a literal-drift test; hand-written constants give only the former.

## References

- `packages/dotnet/telemetry/core/` - `TelemetryServiceCollectionExtensions.cs` (`AddD2Telemetry`, OTLP wiring, self-referential HttpClient filter), `Internal/AggregatedTelemetrySources.cs` (the `ActivitySource` and `Meter` symbol refs), `Internal/OtelSdkDisabledGate.cs`, `Internal/InternalIpFilter.cs`, `WebApplicationTelemetryExtensions.cs` (`MapD2PrometheusEndpoint`), `D2TelemetryOptions.cs`.
- `packages/dotnet/telemetry/tags-source-gen/` - `TelemetryTagsGenerator.cs`, `TelemetryTagsEmitter.cs`, `CrossSpecResolver.cs`.
- `packages/dotnet/handler/core/BaseHandler.cs` + `HandlerTelemetry.cs` - dual enrichment (`SetTag` + `BeginScope`) and the handler instruments.
- `packages/dotnet/logging/` - `LoggingServiceCollectionExtensions.cs` (`AddSerilog(writeToProviders: true)`), `WebApplicationLoggingExtensions.cs` (`UseD2RequestLogging`), `Internal/D2RequestContextEnricher.cs` (the LOG-OK fields).
- [ADR-0011](0011-pii-redaction-logging-safety.md) - PII-redaction safety (`[RedactData]` destructuring policy, `SanitizedExceptionRender`, the LOG-OK / NOT-LOGGED split). [ADR-0006](0006-abstractions-implementation-split.md) - libraries publish telemetry names as `const`, not SDK registrations. [ADR-0005](0005-handler-pipeline.md) - the pipeline that performs dual enrichment. [ADR-0002](0002-spec-driven-codegen.md) - the codegen pattern for tag constants. Host composition roots own the aggregator registry (`AggregatedTelemetrySources`) that subscribes every library source/meter.
