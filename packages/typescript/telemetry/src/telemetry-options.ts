// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Options for {@link setupTelemetry}. All endpoint URLs default to env-var
 * resolution (`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` etc.) when unset.
 *
 * Mirrors `DcsvIo.D2.Telemetry.D2TelemetryOptions` field-for-field where
 * the .NET options surface applies in Node.
 */
export interface TelemetryOptions {
  /** Service name; emitted as `service.name` resource attribute. */
  readonly serviceName: string;
  /** Environment label (e.g. `"prod"`, `"local"`). */
  readonly environment?: string;
  /**
   * Per-signal OTLP endpoints. An endpoint set here overrides the
   * `OTEL_EXPORTER_OTLP_<SIGNAL>_ENDPOINT` environment variable.
   */
  readonly otlpTracesEndpoint?: string;
  readonly otlpMetricsEndpoint?: string;
  readonly otlpLogsEndpoint?: string;
  /**
   * Additional ActivitySources / meters / instrumentation paths.
   * `setupTelemetry` does not read `additionalActivitySources`,
   * `additionalMeters` or `instrumentationExcludedPaths`.
   */
  readonly additionalActivitySources?: readonly string[];
  readonly additionalMeters?: readonly string[];
  readonly instrumentationExcludedPaths?: readonly (string | RegExp)[];
  /**
   * Set to `true` to short-circuit telemetry init - `setupTelemetry`
   * creates no provider and returns a handle with a no-op `shutdown` and
   * `disabled` set to `true`. Mirrors `OTEL_SDK_DISABLED`
   * env var behavior.
   */
  readonly disabled?: boolean;
}
