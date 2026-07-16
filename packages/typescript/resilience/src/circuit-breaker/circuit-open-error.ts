// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Thrown by `CircuitBreaker.execute()` when the circuit is currently open
 * (cooldown active). Mirrors .NET `CircuitOpenException`.
 */
export class CircuitOpenError extends Error {
  constructor(message = "circuit is open") {
    super(message);
    this.name = "CircuitOpenError";
  }
}
