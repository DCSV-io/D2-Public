// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { inspect } from "node:util";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("rabbitmq-client", async () => {
  const { ConnectionFixture } =
    await import("./fixtures/managed-connection-fixture.js");
  return { Connection: ConnectionFixture };
});

import { setupLogger } from "@dcsv-io/d2-logging";
import { createChangingOptionsFixture } from "./fixtures/changing-options-fixture.js";
import { vendor } from "./fixtures/managed-connection-fixture.js";
import { RecordingLogger } from "./helpers.js";

import {
  createConnection,
  RabbitMqConnectionOptions,
} from "../src/connection/connection-options.js";
import { DlqRepublisher } from "../src/subscribing/dlq-republisher.js";

// Every case here fails without the private field and its reveal method. As a
// plain interface, the options type would give `new RabbitMqConnectionOptions`
// no value to construct, so each case would fail at import, and createConnection
// would read a plain object literal instead of refusing it.

const PASSWORD = "broker-secret-5d81";
const HOST = "rabbitmq.internal";
const URI = `amqps://svc:${PASSWORD}@${HOST}:5671/d2`;
const CLIENT_NAME = "reveal-fixture-svc";
const DLQ_OPTIONS = { republishMs: 100, cleanupMs: 10 };

function options(): RabbitMqConnectionOptions {
  return new RabbitMqConnectionOptions({
    connectionUri: URI,
    clientProvidedName: CLIENT_NAME,
    retryLowMs: 100,
  });
}

function expectNoAddress(rendered: string): void {
  expect(rendered).not.toContain(PASSWORD);
  expect(rendered).not.toContain(HOST);
}

beforeEach(() => {
  vendor.connections.length = 0;
  vendor.options.length = 0;
  vendor.construct.mockReset();
  vendor.close.mockReset().mockResolvedValue(undefined);
});

describe("RabbitMqConnectionOptions - the address is read only through the reveal method", () => {
  it("RevealConnectionUri_ReturnsTheAddressItWasCreatedWith", () => {
    expect(options().revealConnectionUri()).toBe(URI);
  });

  it("ConnectionUri_IsNoPropertyOfTheInstance", () => {
    const created = options();
    expect("connectionUri" in created).toBe(false);
    expect(
      (created as unknown as Record<string, unknown>)["connectionUri"],
    ).toBeUndefined();
    expect(Object.getOwnPropertyNames(created)).not.toContain("connectionUri");
  });

  it("Create_InitChangedAfterwards_KeepsTheSettingsItCopied", () => {
    const init = {
      connectionUri: URI,
      clientProvidedName: CLIENT_NAME,
    };
    const created = new RabbitMqConnectionOptions(init);
    init.connectionUri = "amqp://other:changed@elsewhere.example:5672/";
    init.clientProvidedName = "changed";
    expect(created.revealConnectionUri()).toBe(URI);
    expect(created.clientProvidedName).toBe(CLIENT_NAME);
  });

  it("Create_Always_FreezesTheInstance", () => {
    const created = options();
    expect(Object.isFrozen(created)).toBe(true);
    expect(() => {
      (created as { allowUnencryptedTransport?: boolean })[
        "allowUnencryptedTransport"
      ] = true;
    }).toThrow(TypeError);
    expect(created.allowUnencryptedTransport).toBeUndefined();
  });
});

describe("RabbitMqConnectionOptions - no serialized form carries the address", () => {
  it("JsonStringify_RendersThePublicSettingsWithoutTheAddress", () => {
    const rendered = JSON.stringify(options());
    expect(rendered).toContain(CLIENT_NAME);
    expectNoAddress(rendered);
  });

  it("StructuredClone_CopiesThePublicSettingsWithoutTheAddress", () => {
    const rendered = JSON.stringify(structuredClone(options()));
    expect(rendered).toContain(CLIENT_NAME);
    expectNoAddress(rendered);
  });

  it("ObjectSpread_CopiesThePublicSettingsWithoutTheAddress", () => {
    const rendered = JSON.stringify({ ...options() });
    expect(rendered).toContain(CLIENT_NAME);
    expectNoAddress(rendered);
  });

  it("UtilInspect_RendersThePublicSettingsWithoutTheAddress", () => {
    const rendered = inspect(options(), { showHidden: true, depth: 8 });
    expect(rendered).toContain(CLIENT_NAME);
    expectNoAddress(rendered);
  });

  it("StringConversion_RendersNoSetting", () => {
    expect(String(options())).toBe("[object Object]");
  });
});

describe("RabbitMqConnectionOptions - no log record carries the address", () => {
  it("StructuredLog_OptionsRevealedAndForwarded_RendersNoAddress", async () => {
    const created = options();
    // The connection reveals the address and forwards it to the vendor client
    // before the options are logged.
    const connection = createConnection(created, new RecordingLogger());
    const chunks: string[] = [];
    const stream = process.stdout as unknown as { write: unknown };
    const original = stream.write;
    stream.write = (chunk: unknown): boolean => {
      chunks.push(String(chunk));
      return true;
    };
    try {
      // Pino resolves its destination when it is built, so the logger is
      // built inside the capture.
      const logger = setupLogger({ serviceName: "reveal-fixture" });
      logger.info("broker settings", { options: created });
    } finally {
      stream.write = original;
    }
    await connection.close();
    expect(vendor.options).toStrictEqual([
      { url: URI, connectionName: CLIENT_NAME, retryLow: 100 },
    ]);
    const rendered = chunks.join("");
    expect(rendered).toContain("broker settings");
    expect(rendered).toContain(CLIENT_NAME);
    expectNoAddress(rendered);
  });
});

describe("RabbitMqConnectionOptions - a plain object is refused without its address in the error", () => {
  const literal = {
    connectionUri: URI,
    clientProvidedName: CLIENT_NAME,
  } as unknown as RabbitMqConnectionOptions;

  it("CreateConnection_PlainObjectLiteral_ThrowsTypeErrorBeforeAnyConnectionAttempt", () => {
    let caught: unknown;
    try {
      createConnection(literal, new RecordingLogger());
    } catch (error) {
      caught = error;
    }
    expect(caught).toBeInstanceOf(TypeError);
    expect((caught as Error).message).toContain(
      "new RabbitMqConnectionOptions",
    );
    expectNoAddress((caught as Error).message);
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  it("CreateConnection_MissingOptions_ThrowsTypeErrorBeforeAnyConnectionAttempt", () => {
    expect(() =>
      createConnection(undefined as never, new RecordingLogger()),
    ).toThrow("new RabbitMqConnectionOptions");
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  it("CreateConnection_ObjectWithoutThePrivateField_ThrowsTypeErrorBeforeAnyConnectionAttempt", () => {
    const forged = Object.create(
      RabbitMqConnectionOptions.prototype,
    ) as RabbitMqConnectionOptions;
    expect(() => createConnection(forged, new RecordingLogger())).toThrow(
      TypeError,
    );
    expect(vendor.construct).not.toHaveBeenCalled();
  });

  // Objects that have their own reveal method but were not created by the
  // constructor; one of them also has the class prototype.
  const forgeries: [string, () => RabbitMqConnectionOptions][] = [
    [
      "PlainObject",
      () =>
        ({
          clientProvidedName: CLIENT_NAME,
          revealConnectionUri: () => URI,
        }) as unknown as RabbitMqConnectionOptions,
    ],
    [
      "ClassPrototype",
      () =>
        Object.assign(Object.create(RabbitMqConnectionOptions.prototype), {
          clientProvidedName: CLIENT_NAME,
          revealConnectionUri: () => URI,
        }) as RabbitMqConnectionOptions,
    ],
  ];

  it.each(forgeries)(
    "CreateConnection_%sWithItsOwnRevealMethod_ThrowsTypeErrorBeforeAnyConnectionAttempt",
    (_, forge) => {
      let caught: unknown;
      try {
        createConnection(forge(), new RecordingLogger());
      } catch (error) {
        caught = error;
      }
      expect(caught).toBeInstanceOf(TypeError);
      expect((caught as Error).message).toContain(
        "new RabbitMqConnectionOptions",
      );
      expectNoAddress((caught as Error).message);
      expect(vendor.construct).not.toHaveBeenCalled();
    },
  );

  it.each(forgeries)(
    "CreateDlqRepublisher_%sWithItsOwnRevealMethod_ThrowsTypeError",
    (_, forge) => {
      expect(
        () =>
          new DlqRepublisher(forge(), DLQ_OPTIONS, 1, new RecordingLogger(), {
            connectionFactory: vi.fn<() => never>(),
          }),
      ).toThrow("new RabbitMqConnectionOptions");
    },
  );

  it("CreateConnection_OptionsInstance_Connects", async () => {
    const connection = createConnection(options(), new RecordingLogger());
    expect(vendor.options).toStrictEqual([
      { url: URI, connectionName: CLIENT_NAME, retryLow: 100 },
    ]);
    await connection.close();
  });

  it("CreateDlqRepublisher_PlainObjectLiteral_ThrowsTypeError", () => {
    let caught: unknown;
    try {
      new DlqRepublisher(literal, DLQ_OPTIONS, 1, new RecordingLogger(), {
        connectionFactory: vi.fn<() => never>(),
      });
    } catch (error) {
      caught = error;
    }
    expect(caught).toBeInstanceOf(TypeError);
    expectNoAddress((caught as Error).message);
  });
});

describe("RabbitMqConnectionOptions - the address checked is the address connected to", () => {
  // Each reveal after the first returns a plain address to a remote host, which
  // the transport check refuses; a second read would connect to it unchecked.
  const REFUSED = `amqp://svc:${PASSWORD}@${HOST}:5672/d2`;

  it("CreateConnection_RevealsTheAddressOnce_ConnectsToTheCheckedAddress", async () => {
    const fixture = createChangingOptionsFixture(URI, REFUSED);
    const connection = createConnection(fixture.options, new RecordingLogger());
    expect(fixture.reads()).toBe(1);
    expect(vendor.options).toStrictEqual([{ url: URI }]);
    await connection.close();
  });

  it("CreateDlqRepublisher_RevealsTheAddressOnce", async () => {
    const fixture = createChangingOptionsFixture(URI, REFUSED);
    const republisher = new DlqRepublisher(
      fixture.options,
      DLQ_OPTIONS,
      1,
      new RecordingLogger(),
    );
    expect(fixture.reads()).toBe(1);
    await republisher.close();
    await republisher.resourceCompletion;
  });
});

describe("RabbitMqConnectionOptions.revealConnectionUri - production callers", () => {
  const REVEAL = /\brevealConnectionUri\b/g;
  const REVEAL_CALL = /\.revealConnectionUri\s*\(/g;

  // Every production file that mentions the reveal method, with the number of
  // times its code mentions it: the declaration, and the call in the transport
  // check that reads the address once per options instance for both connection
  // builders. A new entry widens where the credential is read and must be a
  // deliberate, reviewed change.
  const EXPECTED_NAMES = {
    "connection/connection-options.ts": 1,
    "connection/transport-admission.ts": 1,
  };
  const EXPECTED_CALLS = { "connection/transport-admission.ts": 1 };

  const ADMIT = /\badmitConnectionUri\b/g;
  const ADMIT_CALL = /(?<!function\s+)\badmitConnectionUri\s*\(/g;

  // admitConnectionUri returns the address with the credential in it, so every
  // production file that mentions it is pinned too: the declaration, and an import
  // and one call in each connection builder.
  const EXPECTED_ADMIT_NAMES = {
    "connection/managed-connection.ts": 2,
    "connection/transport-admission.ts": 1,
    "subscribing/dlq-republisher.ts": 2,
  };
  const EXPECTED_ADMIT_CALLS = {
    "connection/managed-connection.ts": 1,
    "subscribing/dlq-republisher.ts": 1,
  };

  function packageSourceRoot(): string {
    let directory = dirname(fileURLToPath(import.meta.url));
    for (;;) {
      const manifest = join(directory, "package.json");
      if (
        existsSync(manifest) &&
        (JSON.parse(readFileSync(manifest, "utf8")) as { name?: string })
          .name === "@dcsv-io/d2-messaging-rabbitmq"
      )
        return join(directory, "src");
      const parent = dirname(directory);
      if (parent === directory)
        throw new Error("The package root was not found above the test.");
      directory = parent;
    }
  }

  function sourceFiles(directory: string): string[] {
    return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
      const path = join(directory, entry.name);
      if (entry.isDirectory()) return sourceFiles(path);
      return entry.name.endsWith(".ts") ? [path] : [];
    });
  }

  function withoutComments(text: string): string {
    return text.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
  }

  it("RevealConnectionUri_HasNoUnexpectedProductionCaller", () => {
    const root = packageSourceRoot();
    const names: Record<string, number> = {};
    const calls: Record<string, number> = {};
    for (const file of sourceFiles(root)) {
      const code = withoutComments(readFileSync(file, "utf8"));
      const key = relative(root, file).split("\\").join("/");
      const nameCount = code.match(REVEAL)?.length ?? 0;
      const callCount = code.match(REVEAL_CALL)?.length ?? 0;
      if (nameCount > 0) names[key] = nameCount;
      if (callCount > 0) calls[key] = callCount;
    }
    expect(names).toStrictEqual(EXPECTED_NAMES);
    expect(calls).toStrictEqual(EXPECTED_CALLS);
  });

  it("AdmitConnectionUri_HasNoUnexpectedProductionCaller", () => {
    const root = packageSourceRoot();
    const names: Record<string, number> = {};
    const calls: Record<string, number> = {};
    for (const file of sourceFiles(root)) {
      const code = withoutComments(readFileSync(file, "utf8"));
      const key = relative(root, file).split("\\").join("/");
      const nameCount = code.match(ADMIT)?.length ?? 0;
      const callCount = code.match(ADMIT_CALL)?.length ?? 0;
      if (nameCount > 0) names[key] = nameCount;
      if (callCount > 0) calls[key] = callCount;
    }
    expect(names).toStrictEqual(EXPECTED_ADMIT_NAMES);
    expect(calls).toStrictEqual(EXPECTED_ADMIT_CALLS);
  });
});
