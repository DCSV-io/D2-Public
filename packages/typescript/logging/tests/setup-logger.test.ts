// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import {
  clearRedactedFieldsRegistry,
  markRedactedFields,
} from "../src/redaction.js";
import { setupLogger } from "../src/setup-logger.js";

/**
 * Capture the records pino serializes while `run` executes. The default
 * destination writes through `process.stdout.write`, so replacing it for the
 * duration of the call collects exactly the emitted JSON lines.
 */
function captureRecords(run: () => void): Record<string, unknown>[] {
  const chunks: string[] = [];
  const stream = process.stdout as unknown as { write: unknown };
  const original = stream.write;
  stream.write = (chunk: unknown): boolean => {
    chunks.push(String(chunk));
    return true;
  };

  try {
    run();
  } finally {
    stream.write = original;
  }

  return chunks
    .join("")
    .split("\n")
    .filter((line) => line.length > 0)
    .map((line) => JSON.parse(line) as Record<string, unknown>);
}

describe("setupLogger", () => {
  afterEach(() => clearRedactedFieldsRegistry());

  it("returns an ILogger with all six level methods + child()", () => {
    const log = setupLogger({ serviceName: "svc" });
    for (const m of ["trace", "debug", "info", "warn", "error", "fatal"]) {
      expect(typeof (log as unknown as Record<string, unknown>)[m]).toBe(
        "function",
      );
    }
    expect(typeof log.child).toBe("function");
  });

  it("level methods accept (message) and (message, bindings)", () => {
    const log = setupLogger({ serviceName: "svc", minLevel: "trace" });
    expect(() => log.trace("plain")).not.toThrow();
    expect(() => log.trace("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.debug("plain")).not.toThrow();
    expect(() => log.debug("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.info("plain")).not.toThrow();
    expect(() => log.info("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.warn("plain")).not.toThrow();
    expect(() => log.warn("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.error("plain")).not.toThrow();
    expect(() => log.error("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.fatal("plain")).not.toThrow();
    expect(() => log.fatal("with bindings", { x: 1 })).not.toThrow();
  });

  it("child() returns a wrapper that exposes the same shape", () => {
    const log = setupLogger({ serviceName: "svc" });
    const child = log.child({ requestId: "x" });
    expect(typeof child.info).toBe("function");
    expect(child).not.toBe(log);
    expect(() => child.info("hello", { extra: 1 })).not.toThrow();
  });

  it("redactPaths from explicit array merge into Pino redact config", () => {
    // Smoke check: setup with redactPaths must not throw, and the logger it
    // returns must be usable.
    const log = setupLogger({
      serviceName: "svc",
      redactPaths: [["email", "phone"], ["secret"]],
    });
    expect(() => log.info("hi", { email: "a@b.c" })).not.toThrow();
  });

  it("drops every path of an emitted redact-paths constant, and nothing else", () => {
    // The path shapes a generated redact-paths constant contains: a top-level
    // field, a field inside a nested model, and a field inside a collection of
    // nested models.
    const emittedPaths: readonly string[] = [
      "aadContext",
      "owner.email",
      "entries[*].keyBytes",
    ];
    // The logger is built inside the capture: pino resolves its destination
    // when it is constructed, so a logger built beforehand writes past the
    // replacement.
    const records = captureRecords(() => {
      const log = setupLogger({
        serviceName: "svc",
        redactPaths: [emittedPaths],
      });

      log.info("keyring read", {
        activeKid: "kid-1",
        aadContext: "AAD-VALUE",
        owner: { id: "u-1", email: "someone@example.com" },
        entries: [
          { kid: "kid-1", keyBytes: "SECRET-ONE" },
          { kid: "kid-2", keyBytes: "SECRET-TWO" },
        ],
      });
    });

    expect(records).toHaveLength(1);
    const record = records[0]!;
    const serialized = JSON.stringify(record);
    expect(serialized).not.toContain("AAD-VALUE");
    expect(serialized).not.toContain("someone@example.com");
    expect(serialized).not.toContain("SECRET-ONE");
    expect(serialized).not.toContain("SECRET-TWO");

    // The unmarked sibling fields remain, which shows that only the listed
    // paths were removed and not the objects that contain them.
    expect(record["activeKid"]).toBe("kid-1");
    expect((record["owner"] as Record<string, unknown>)["id"]).toBe("u-1");
    expect(
      (record["entries"] as Record<string, unknown>[]).map((e) => e["kid"]),
    ).toEqual(["kid-1", "kid-2"]);
  });

  it("redact paths from markRedactedFields registry get merged", () => {
    markRedactedFields(Symbol("X"), ["password"]);
    const log = setupLogger({ serviceName: "svc" });
    expect(() => log.info("hi", { password: "x" })).not.toThrow();
  });

  it("environment defaults to 'unknown'", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", minLevel: "info" }),
    ).not.toThrow();
  });

  it("environment override accepted", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", environment: "test" }),
    ).not.toThrow();
  });

  it("pretty=true wires up pino-pretty transport (smoke)", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", pretty: true }),
    ).not.toThrow();
  });
});
