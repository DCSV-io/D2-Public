// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Per-call structured bindings. Plain object, never an `Error` instance.
 * The `ILogger` API forbids passing `Error` directly because `error.message`
 * leaks broker URI passwords / user input. Use {@link sanitizedErrorRender}
 * to extract a safe shape (`{ name, firstFrame }`) and pass that instead.
 */
export type LogBindings = Readonly<Record<string, unknown>>;

/**
 * Structured logger interface. Mirrors the .NET `DcsvIo.D2.Logging.ILogger`
 * shape: per-level methods accepting `(message, bindings?)` with no
 * `Error`-typed parameter (PII safety; matches .NET's
 * `LoggerMessageParameterContractTests.cs` rule that `[LoggerMessage]` partial
 * methods must not accept `Exception`).
 */
export interface ILogger {
  trace(message: string, bindings?: LogBindings): void;
  debug(message: string, bindings?: LogBindings): void;
  info(message: string, bindings?: LogBindings): void;
  warn(message: string, bindings?: LogBindings): void;
  error(message: string, bindings?: LogBindings): void;
  fatal(message: string, bindings?: LogBindings): void;

  /** Returns a child logger that adds the given bindings to every call. */
  child(bindings: LogBindings): ILogger;
}
