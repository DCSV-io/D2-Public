// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  CompositePropagator,
  W3CBaggagePropagator,
  W3CTraceContextPropagator,
} from "@opentelemetry/core";

/**
 * Builds the W3C-compliant context propagator stack — `traceparent` +
 * `tracestate` (W3C trace context) + W3C baggage. Mirrors .NET
 * `TextMapPropagator` defaults.
 */
export function buildPropagators(): CompositePropagator {
  return new CompositePropagator({
    propagators: [new W3CTraceContextPropagator(), new W3CBaggagePropagator()],
  });
}
