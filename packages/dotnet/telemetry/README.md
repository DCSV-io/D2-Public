<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# telemetry/

> Parent: [`packages/dotnet/`](../README.md)

The OpenTelemetry composition root for D2 services. It contains the runtime SDK setup (traces, metrics and logs, OTLP
exporters, a Prometheus endpoint and auto-instrumentations) and the source generator that emits the per-meter
telemetry-tag constants from `contracts/telemetry/telemetry.spec.json`. The runtime collects each owning library's
`ActivitySource` and `Meter` through compile-time symbol references, so a rename in an owning library breaks the build
here.

The code generator for OTel messaging tags is in
[`messaging/otel-messaging-tags-source-gen/`](../messaging/otel-messaging-tags-source-gen/README.md).

## Packages

| Package                                         | Description                                                                                                                                                |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                       | OpenTelemetry SDK setup - per-signal OTLP exporters, `MapD2PrometheusEndpoint`, auto-instrumentations, and the single `AddD2Telemetry()` aggregation call. |
| [`tags-source-gen/`](tags-source-gen/README.md) | Roslyn generator that emits the per-meter `*TelemetryTags.g.cs` typed-constant classes from `contracts/telemetry/telemetry.spec.json`.                     |
