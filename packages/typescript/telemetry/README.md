<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-telemetry

`setupTelemetry` starts the OpenTelemetry SDK for a Node service in one call. It wires the OTLP exporters for
traces, metrics and logs and the W3C propagator stack. It mirrors `DcsvIo.D2.Telemetry` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-telemetry
```

## Public API

| Export                 | Purpose                                                                               |
| ---------------------- | ------------------------------------------------------------------------------------- |
| `TelemetryOptions`     | `serviceName`, plus optional `environment`, per-signal OTLP endpoints and `disabled`. |
| `TelemetryHandle`      | Result handle: `shutdown(): Promise<void>` and `disabled: boolean`.                   |
| `setupTelemetry(opts)` | Starts the OTel SDK. Honors `disabled` for tests, as .NET honors `OTEL_SDK_DISABLED`. |
| `buildPropagators()`   | Returns the W3C trace-context and baggage composite propagator.                       |

## Dependencies

- `@dcsv-io/d2-utilities` (declared for env-var configuration helpers)
- `@dcsv-io/d2-logging` (declared for telemetry-side log enrichment; this package does not implement that
  integration)
- `@opentelemetry/api`, `@opentelemetry/sdk-node`, OTLP exporters
 (traces / metrics / logs over HTTP), `@opentelemetry/resources`,
 `@opentelemetry/semantic-conventions`

## Usage example

```ts
import { setupTelemetry } from "@dcsv-io/d2-telemetry";

const handle = setupTelemetry({
  serviceName: "my-svc",
  environment: process.env.NODE_ENV,
  otlpTracesEndpoint: process.env.OTEL_EXPORTER_OTLP_TRACES_ENDPOINT,
});

// On shutdown:
process.on("SIGTERM", async () => {
  await handle.shutdown();
});
```

## Parity with .NET

Mirrors `DcsvIo.D2.Telemetry`:

- `setupTelemetry` <-> `services.AddD2Telemetry(options)`.
- `buildPropagators` <-> `Propagators.SetDefault(...)`.
- `TelemetryHandle.shutdown` <-> `IServiceProvider.GetRequiredService<TracerProvider>().ShutdownAsync()`.
- `TelemetryOptions.disabled` <-> `OTEL_SDK_DISABLED` env var short-circuit.

## Edge cases

- `disabled=true` returns a no-op handle, and its `shutdown()` resolves immediately.
- When a per-signal endpoint URL is missing, the exporter falls back to the OTLP environment variable defaults
  (`OTEL_EXPORTER_OTLP_*_ENDPOINT`).
- The underlying SDK does not support multiple `setupTelemetry` calls in one process. Call it once at process boot.
- `shutdown()` is idempotent at the SDK level: after the first call flushes, later calls return resolved promises.
