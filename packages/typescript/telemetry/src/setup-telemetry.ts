// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { propagation } from "@opentelemetry/api";
import { OTLPLogExporter } from "@opentelemetry/exporter-logs-otlp-http";
import { OTLPMetricExporter } from "@opentelemetry/exporter-metrics-otlp-http";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { BatchLogRecordProcessor } from "@opentelemetry/sdk-logs";
import { PeriodicExportingMetricReader } from "@opentelemetry/sdk-metrics";
import { NodeSDK } from "@opentelemetry/sdk-node";
import {
  ATTR_SERVICE_NAME,
  ATTR_DEPLOYMENT_ENVIRONMENT_NAME,
} from "@opentelemetry/semantic-conventions/incubating";

import { buildPropagators } from "./propagators.js";
import type { TelemetryOptions } from "./telemetry-options.js";

/**
 * Result of {@link setupTelemetry}: a `shutdown()` that shuts the SDK down and
 * flushes pending telemetry, plus a `disabled` flag. Disabled mode returns a
 * no-op shutdown.
 */
export interface TelemetryHandle {
  readonly shutdown: () => Promise<void>;
  readonly disabled: boolean;
}

/**
 * One-call OTel SDK bootstrap. Mirrors the .NET
 * `services.AddD2Telemetry(options)` shape. It wires the traces, metrics and
 * logs exporters, installs the W3C propagator stack, and returns early when the
 * `disabled` flag is set.
 */
export function setupTelemetry(opts: TelemetryOptions): TelemetryHandle {
  if (opts.disabled === true) {
    return { shutdown: async () => undefined, disabled: true };
  }

  propagation.setGlobalPropagator(buildPropagators());

  const resource = resourceFromAttributes({
    [ATTR_SERVICE_NAME]: opts.serviceName,
    [ATTR_DEPLOYMENT_ENVIRONMENT_NAME]: opts.environment ?? "unknown",
  });

  const traceExporter = new OTLPTraceExporter(
    opts.otlpTracesEndpoint ? { url: opts.otlpTracesEndpoint } : undefined,
  );

  const metricReader = new PeriodicExportingMetricReader({
    exporter: new OTLPMetricExporter(
      opts.otlpMetricsEndpoint ? { url: opts.otlpMetricsEndpoint } : undefined,
    ),
  });

  const logRecordProcessor = new BatchLogRecordProcessor(
    new OTLPLogExporter(
      opts.otlpLogsEndpoint ? { url: opts.otlpLogsEndpoint } : undefined,
    ),
  );

  const sdk = new NodeSDK({
    resource,
    traceExporter,
    metricReader,
    logRecordProcessors: [logRecordProcessor],
  });

  sdk.start();

  return {
    disabled: false,
    shutdown: async () => {
      await sdk.shutdown();
    },
  };
}
