// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { EventEmitter } from "node:events";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { afterAll, describe, expect, it } from "vitest";
import { createCompletion } from "../src/connection/managed-connection.js";
import {
  PackedPackageFixture,
  findMessagingPackageFixture,
  loadPackHelperFixture,
  runWithGnuTarSemanticsFixture,
} from "./fixtures/packed-package-fixture.js";
import { existsSync } from "node:fs";
import {
  copyFile,
  mkdir,
  readFile,
  readdir,
  writeFile,
} from "node:fs/promises";

const require = createRequire(import.meta.url);
const clientDirectory = dirname(require.resolve("rabbitmq-client"));
const { Channel } = require(join(clientDirectory, "Channel.js")) as {
  Channel: { prototype: object };
};
const { Cmd, FrameType } = require(join(clientDirectory, "codec.js")) as {
  Cmd: Record<string, number>;
  FrameType: Record<string, number>;
};

async function receive(
  method: string,
  input: Buffer,
  contentType: string,
  chunkSize: number,
) {
  const channel = Object.create(Channel.prototype) as EventEmitter & {
    id: number;
    _state: Record<string, unknown>;
    _onMethod(frame: object): void;
    _onHeader(frame: object): void;
    _onBody(frame: object): void;
  };
  EventEmitter.call(channel);
  channel.id = 1;
  const received = createCompletion<Record<string, unknown>>();
  const consumers = new Map();
  channel._state = {
    maxFrameSize: chunkSize + 8,
    consumers,
    stream: { writable: false },
  };
  if (method === "BasicDeliver") consumers.set("fixture", received.resolve);
  else if (method === "BasicReturn")
    channel.once("basic.return", received.resolve);
  else
    channel._state["rpc"] = [
      { resolve: received.resolve, reject: received.reject },
      Cmd["BasicGet"],
      Cmd["BasicGetOK"],
    ];
  channel._onMethod({
    type: FrameType["METHOD"],
    channelId: 1,
    methodId: Cmd[method],
    params: {
      consumerTag: "fixture",
      exchange: "fixture.exchange",
      routingKey: "fixture.route",
      deliveryTag: 1,
      redelivered: false,
      messageCount: 0,
      replyCode: 312,
      replyText: "NO_ROUTE",
    },
  });
  channel._onHeader({
    type: FrameType["HEADER"],
    channelId: 1,
    bodySize: input.length,
    fields: { contentType, deliveryMode: 2 },
  });
  for (let offset = 0; offset < input.length; offset += chunkSize) {
    channel._onBody({
      type: FrameType["BODY"],
      channelId: 1,
      payload: input.subarray(offset, offset + chunkSize),
    });
  }
  return received.promise;
}

describe.each(["BasicDeliver", "BasicGetOK", "BasicReturn"])(
  "Patched client %s",
  (method) => {
    it.each([3, 4096])(
      "Receive_JsonAtChunkSize_%s_PreservesRawBytesAndLegacyDecode",
      async (chunkSize) => {
        const input = Buffer.from(' { "key": 1, "key": 2 } \n');
        const message = await receive(
          method,
          input,
          "application/json",
          chunkSize,
        );
        expect(message["rawBody"]).toEqual(input);
        expect(message["body"]).toEqual({ key: 2 });
      },
    );

    it.each([3, 4096])(
      "Receive_InvalidUtf8AtChunkSize_%s_PreservesRawBytesAndLegacyDecode",
      async (chunkSize) => {
        const input = Buffer.from([0x61, 0xff, 0xc3, 0x28, 0x62]);
        const message = await receive(method, input, "text/plain", chunkSize);
        expect(message["rawBody"]).toEqual(input);
        expect(message["body"]).toBe("a\ufffd\ufffd(b");
      },
    );
  },
);

describe("Pack archive validation", () => {
  // Without the pack helper, these checks fail because no archive admission check exists.
  async function validator(): Promise<
    (names: string[], details: string[], allowed: string[]) => void
  > {
    const moduleUrl = new URL("../scripts/pack-patched.mjs", import.meta.url)
      .href;
    const module = (await import(moduleUrl)) as {
      validateArchiveMembers: (
        names: string[],
        details: string[],
        allowed: string[],
      ) => void;
    };
    return module.validateArchiveMembers;
  }

  it("ValidateArchive_AllowedRegularMember_Accepts", async () => {
    const validate = await validator();
    expect(() =>
      validate(
        ["package/dist/index.js"],
        ["-rw-r--r-- file"],
        ["package/dist/index.js"],
      ),
    ).not.toThrow();
  });

  it.each([
    "/package/dist/index.js",
    "package/../outside",
    "package/dist/../../outside",
    "package\\dist\\index.js",
    "package/extra.js",
  ])("ValidateArchive_UnsafeMember_%s_Rejects", async (name) => {
    const validate = await validator();
    expect(() =>
      validate([name], ["-rw-r--r-- file"], ["package/dist/index.js"]),
    ).toThrow(/archive member/i);
  });

  it.each(["lrwxrwxrwx symlink", "hrw-r--r-- hardlink"])(
    "ValidateArchive_Link_%s_Rejects",
    async (details) => {
      const validate = await validator();
      expect(() =>
        validate(
          ["package/dist/index.js"],
          [details],
          ["package/dist/index.js"],
        ),
      ).toThrow(/archive member/i);
    },
  );
});

// The flat export test runs a workspace install and build, a pack, then a
// consumer install and type check: 20 to 40 s on a developer machine, and past
// 120 s on the GitHub-hosted Windows runner.
const FLAT_EXPORT_BUDGET_MS = process.platform === "win32" ? 300_000 : 120_000;

describe("Patched D2 package", () => {
  // This package's artifact and its workspace dependencies' archives are packed
  // once, by the first test that asks, and only read after that. A test that
  // changes the pack's input or its runner still packs for itself.
  let shared: Promise<PackedPackageFixture> | undefined;
  let sharedDependencies: Promise<ReadonlyMap<string, string>> | undefined;
  let sharedArtifact: Promise<{ artifact: string; sha256: string }> | undefined;
  const sharedFixture = () => (shared ??= PackedPackageFixture.create());
  const sharedDestination = async () =>
    join((await sharedFixture()).directory, "shared");
  const dependencyArchives = () =>
    (sharedDependencies ??= sharedFixture().then((fixture) =>
      fixture.packDependencies(),
    ));
  const packedOnce = () =>
    (sharedArtifact ??= (async () =>
      (await loadPackHelperFixture()).packPatched({
        packageRoot: await findMessagingPackageFixture(),
        destination: await sharedDestination(),
      }))());

  afterAll(async () => {
    const fixture = await shared?.catch(() => undefined);
    await fixture?.dispose();
  });

  it(
    "Pack_FlatExport_BuildsAndRunsWithoutPrivateTools",
    async () => {
      const fixture = await PackedPackageFixture.create();
      try {
        const flat = await fixture.createFlatWorkspace();
        expect(existsSync(join(flat, "private"))).toBe(false);
        const destination = join(fixture.directory, "flat-artifacts");
        await fixture.pnpm(
          [
            "--filter",
            "@dcsv-io/d2-messaging-rabbitmq",
            "run",
            "pack:patched",
            "--",
            destination,
          ],
          flat,
        );
        const artifacts = await readdir(destination);
        expect(artifacts).toHaveLength(1);
        const consumer = await fixture.installConsumer(
          join(destination, artifacts[0]!),
          await dependencyArchives(),
        );
        expect(await fixture.probeConsumer(consumer)).toContain(
          "Packed D2 runtime and raw-byte mapping passed.",
        );
        await fixture.typeCheckConsumer(consumer);
      } finally {
        await fixture.dispose();
      }
    },
    FLAT_EXPORT_BUDGET_MS,
  );

  it("Pack_DirectSourceCommand_RejectsWithoutArtifact", async () => {
    const fixture = await PackedPackageFixture.create();
    try {
      const root = await findMessagingPackageFixture();
      const destination = join(fixture.directory, "direct");
      await expect(
        fixture.pnpm(["pack", "--pack-destination", destination], root),
      ).rejects.toThrow("direct source packing is not supported");
      expect(existsSync(destination)).toBe(false);
    } finally {
      await fixture.dispose();
    }
  });

  it("Pack_ExistingArtifact_RejectsWithoutOverwriting", async () => {
    const helper = await loadPackHelperFixture();
    const packageRoot = await findMessagingPackageFixture();
    const destination = await sharedDestination();
    const first = await packedOnce();
    const original = await readFile(first.artifact);
    await expect(
      helper.packPatched({ packageRoot, destination }),
    ).rejects.toThrow(/Patched pack failed during publish:.*EEXIST/);
    expect(await readFile(first.artifact)).toEqual(original);
  }, 120_000);

  it.each(["package.json", "packaging/producer.package.json"])(
    "Pack_DriftedClientPin_%s_RejectsBeforeChild",
    async (drifted) => {
      const fixture = await PackedPackageFixture.create();
      try {
        const original = await findMessagingPackageFixture();
        const root = join(fixture.directory, "source");
        await mkdir(join(root, "patches"), { recursive: true });
        await mkdir(join(root, "packaging"));
        for (const path of [
          "package.json",
          "packaging/producer.package.json",
          "patches/rabbitmq-client.json",
          "patches/rabbitmq-client@5.0.8.patch",
        ])
          await copyFile(join(original, path), join(root, path));
        const manifest = JSON.parse(
          await readFile(join(root, drifted), "utf8"),
        );
        manifest.dependencies["rabbitmq-client"] = "5.0.7";
        await writeFile(join(root, drifted), JSON.stringify(manifest));
        let invoked = false;
        const helper = await loadPackHelperFixture();
        await expect(
          helper.packPatched(
            {
              packageRoot: root,
              destination: join(fixture.directory, "output"),
            },
            async () => {
              invoked = true;
              throw new Error("fixture child must not run");
            },
          ),
        ).rejects.toThrow(
          "Client dependency pin differs from the producer contract",
        );
        expect(invoked).toBe(false);
      } finally {
        await fixture.dispose();
      }
    },
  );

  it("Pack_CleanConsumer_ResolvesBundledRuntimeAndTypes", async () => {
    const fixture = await PackedPackageFixture.create();
    try {
      const helper = await loadPackHelperFixture();
      const packageRoot = await findMessagingPackageFixture();
      const first = await packedOnce();
      const second = await helper.packPatched({
        packageRoot,
        destination: join(fixture.directory, "second"),
      });
      expect(second.sha256).toBe(first.sha256);
      const consumer = await fixture.installConsumer(
        first.artifact,
        await dependencyArchives(),
      );
      expect(await fixture.probeConsumer(consumer)).toContain(
        "Packed D2 runtime and raw-byte mapping passed.",
      );
      await fixture.typeCheckConsumer(consumer);
      const manifest = JSON.parse(
        await readFile(
          join(
            consumer,
            "node_modules",
            "@dcsv-io",
            "d2-messaging-rabbitmq",
            "package.json",
          ),
          "utf8",
        ),
      ) as Record<string, unknown>;
      expect(manifest["bundleDependencies"]).toEqual(["rabbitmq-client"]);
      expect(manifest).not.toHaveProperty("scripts");
      expect(manifest).not.toHaveProperty("d2PackInputs");
      expect(manifest).not.toHaveProperty("pnpm");
      const unpatched = await fixture.installUnpatchedClient();
      await fixture.replaceBundledClient(consumer, unpatched);
      await expect(fixture.probeConsumer(consumer)).rejects.toThrow(
        "AssertionError",
      );
    } finally {
      await fixture.dispose();
    }
  }, 120_000);

  it("Pack_TarArguments_StayRelativeToWorkingDirectory", async () => {
    const fixture = await PackedPackageFixture.create();
    try {
      const helper = await loadPackHelperFixture();
      const result = await helper.packPatched(
        {
          packageRoot: await findMessagingPackageFixture(),
          destination: join(fixture.directory, "portable"),
        },
        runWithGnuTarSemanticsFixture,
      );
      expect(existsSync(result.artifact)).toBe(true);
    } finally {
      await fixture.dispose();
    }
  }, 120_000);

  it("Pack_ChildFailure_ProducesNoFinalArtifact", async () => {
    const fixture = await PackedPackageFixture.create();
    try {
      const helper = await loadPackHelperFixture();
      const destination = join(fixture.directory, "failed");
      await expect(
        helper.packPatched(
          { packageRoot: await findMessagingPackageFixture(), destination },
          async () => {
            throw new Error("fixture child failed");
          },
        ),
      ).rejects.toThrow(
        "Patched pack failed during sourcePack: fixture child failed",
      );
      expect(existsSync(destination)).toBe(false);
    } finally {
      await fixture.dispose();
    }
  });

  it("VerifyClient_UnpatchedDistribution_RejectsBeforePacking", async () => {
    const fixture = await PackedPackageFixture.create();
    try {
      const helper = await loadPackHelperFixture();
      const unpatched = await fixture.installUnpatchedClient();
      const provenance = JSON.parse(
        await readFile(
          join(
            await findMessagingPackageFixture(),
            "patches",
            "rabbitmq-client.json",
          ),
          "utf8",
        ),
      );
      await expect(
        helper.verifyClientTree(unpatched, provenance),
      ).rejects.toThrow("file set or SHA-256 differs from provenance");
    } finally {
      await fixture.dispose();
    }
  });
});
