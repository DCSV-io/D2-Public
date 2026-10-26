// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import {
  copyFile,
  cp,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  realpath,
  rm,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, dirname, isAbsolute, join, relative, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export type PackCommandRunnerFixture = (
  program: string,
  args: string[],
  cwd: string,
  environment?: Record<string, string>,
) => Promise<string>;

export function findPackWorkspaceFixture(): string {
  let directory = dirname(fileURLToPath(import.meta.url));
  while (!existsSync(join(directory, "pnpm-workspace.yaml"))) {
    const parent = dirname(directory);
    if (parent === directory)
      throw new Error("Fixture workspace boundary was not found.");
    directory = parent;
  }
  return directory;
}

export async function runPackCommandFixture(
  program: string,
  args: string[],
  cwd: string,
  environment: Record<string, string> = {},
): Promise<string> {
  return new Promise((resolve, reject) => {
    const child = spawn(program, args, {
      cwd,
      env: { ...process.env, ...environment },
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let output = "";
    child.stdout.on("data", (chunk) => {
      output += String(chunk);
    });
    child.stderr.on("data", (chunk) => {
      output += String(chunk);
    });
    child.once("error", reject);
    child.once("close", (code) =>
      code === 0
        ? resolve(output)
        : reject(new Error(`Command exited ${code}: ${output}`)),
    );
  });
}

// GNU tar reads an archive name with a colon before its first slash as
// host:file, so a Windows drive letter fails with the message below.
export const runWithGnuTarSemanticsFixture: PackCommandRunnerFixture = async (
  program,
  args,
  cwd,
  environment,
) => {
  if (program === "tar") {
    for (const arg of args) {
      if (/^[A-Za-z]:/.test(arg))
        throw new Error(
          `tar (child): Cannot connect to ${arg[0]}: resolve failed`,
        );
      if (isAbsolute(arg))
        throw new Error(`Fixture tar accepts only relative arguments: ${arg}`);
    }
  }
  return runPackCommandFixture(program, args, cwd, environment);
};

export class PackedPackageFixture {
  private constructor(
    readonly directory: string,
    readonly parent: string,
  ) {}

  static async create(): Promise<PackedPackageFixture> {
    const parent = await realpath(tmpdir());
    return new PackedPackageFixture(
      await mkdtemp(join(parent, "d2-packed-consumer-fixture-")),
      parent,
    );
  }

  async dispose(): Promise<void> {
    const actual = await realpath(this.directory);
    if (
      dirname(actual) !== this.parent ||
      !basename(actual).startsWith("d2-packed-consumer-fixture-")
    )
      throw new Error("Fixture cleanup escaped its owned directory.");
    await rm(actual, { recursive: true });
  }

  async pnpm(args: string[], cwd = this.directory): Promise<string> {
    const entry = process.env["npm_execpath"];
    if (!entry || !/pnpm\.(?:c?js)$/i.test(entry))
      throw new Error("Run packed-consumer tests through pnpm.");
    return runPackCommandFixture(process.execPath, [entry, ...args], cwd);
  }

  async createFlatWorkspace(): Promise<string> {
    const workspace = findPackWorkspaceFixture();
    const publicRoot = existsSync(join(workspace, "public", "packages"))
      ? join(workspace, "public")
      : workspace;
    const flat = join(this.directory, "flat");
    await mkdir(join(flat, "packages", "typescript"), { recursive: true });
    const projects = JSON.parse(
      await this.pnpm(
        [
          "--filter",
          "@dcsv-io/d2-messaging-rabbitmq...",
          "list",
          "--depth",
          "-1",
          "--json",
        ],
        workspace,
      ),
    ) as Array<{ path: string }>;
    for (const project of projects) {
      const name = relative(publicRoot, project.path);
      if (
        name.startsWith("..") ||
        !name.startsWith(`packages${sep}typescript${sep}`)
      )
        throw new Error(
          "Flat fixture dependency is outside public TypeScript packages.",
        );
      await cp(project.path, join(flat, name), {
        recursive: true,
        filter: (path) => {
          const parts = relative(project.path, path).split(sep);
          return (
            !parts.some((part) =>
              ["node_modules", "dist", ".coverage", "artifacts"].includes(part),
            ) && !path.endsWith(".tsbuildinfo")
          );
        },
      });
    }
    await copyFile(join(publicRoot, "LICENSE"), join(flat, "LICENSE"));
    await cp(
      join(publicRoot, "packages", "typescript", "scripts"),
      join(flat, "packages", "typescript", "scripts"),
      { recursive: true },
    );
    await copyFile(
      join(publicRoot, "packages", "typescript", "tsconfig.base.json"),
      join(flat, "packages", "typescript", "tsconfig.base.json"),
    );
    for (const name of ["package.json", "pnpm-workspace.yaml"]) {
      const template = join(
        workspace,
        "private",
        "tools",
        "scripts",
        `export-public-oss.${name}`,
      );
      await copyFile(
        existsSync(template) ? template : join(workspace, name),
        join(flat, name),
      );
    }
    await this.pnpm(["install", "--ignore-scripts"], flat);
    await this.pnpm(
      [
        "--filter",
        "@dcsv-io/d2-messaging-rabbitmq...",
        "-r",
        "--no-bail",
        "build",
      ],
      flat,
    );
    return flat;
  }

  /**
   * Packs every workspace package the messaging package depends on into this
   * fixture's directory, and maps each package name to its archive's path.
   */
  async packDependencies(): Promise<ReadonlyMap<string, string>> {
    const workspace = findPackWorkspaceFixture();
    const projects = JSON.parse(
      await this.pnpm(
        [
          "--filter",
          "@dcsv-io/d2-messaging-rabbitmq...",
          "list",
          "--depth",
          "-1",
          "--json",
        ],
        workspace,
      ),
    ) as { name: string; path: string }[];
    const dependencies = join(this.directory, "dependencies");
    await mkdir(dependencies);
    const archives = new Map<string, string>();
    for (const project of projects) {
      if (project.name === "@dcsv-io/d2-messaging-rabbitmq") continue;
      const before = new Set(await readdir(dependencies));
      await this.pnpm(
        ["--filter", project.name, "pack", "--pack-destination", dependencies],
        workspace,
      );
      const added = (await readdir(dependencies)).filter(
        (name) => !before.has(name),
      );
      if (added.length !== 1)
        throw new Error(`Expected one dependency archive for ${project.name}.`);
      archives.set(project.name, join(dependencies, added[0]!));
    }
    return archives;
  }

  async installConsumer(
    archive: string,
    dependencies?: ReadonlyMap<string, string>,
  ): Promise<string> {
    const archives = dependencies ?? (await this.packDependencies());
    const consumer = join(this.directory, "consumer");
    const overrides: Record<string, string> = {};
    for (const [name, path] of archives)
      overrides[name] =
        `file:${relative(consumer, path).replaceAll("\\", "/")}`;
    await mkdir(consumer);
    await writeFile(join(consumer, "pnpm-workspace.yaml"), "packages: []\n");
    await writeFile(
      join(consumer, ".npmrc"),
      "ignore-scripts=true\nlogs-max=0\nnode-linker=hoisted\n",
    );
    await writeFile(
      join(consumer, "package.json"),
      JSON.stringify(
        {
          name: "packed-consumer-fixture",
          private: true,
          type: "module",
          dependencies: {
            "@dcsv-io/d2-messaging-rabbitmq": `file:${relative(consumer, archive).replaceAll("\\", "/")}`,
          },
          devDependencies: { "@types/node": "24.10.0", typescript: "5.9.3" },
          pnpm: { overrides },
        },
        null,
        2,
      ),
    );
    await this.pnpm(["install", "--ignore-scripts"], consumer);
    return consumer;
  }

  async probeConsumer(consumer: string): Promise<string> {
    const script = `
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { EventEmitter } from "node:events";
import * as d2 from "@dcsv-io/d2-messaging-rabbitmq";
const require = createRequire(import.meta.url);
const entry = fileURLToPath(import.meta.resolve("@dcsv-io/d2-messaging-rabbitmq"));
const clientRequire = createRequire(entry);
const client = clientRequire.resolve("rabbitmq-client");
assert.ok(client.split(String.fromCharCode(92)).join("/")
  .includes("d2-messaging-rabbitmq/node_modules/rabbitmq-client"));
assert.equal(typeof d2.subscribe, "function");
const { Channel } = clientRequire(join(dirname(client), "Channel.js"));
const { Cmd, FrameType } = clientRequire(join(dirname(client), "codec.js"));
const { toConsumedMessage } = await import(pathToFileURL(join(dirname(entry), "subscribing/subscriber.js")));
for (const method of ["BasicDeliver", "BasicGetOK", "BasicReturn"]) {
  for (const size of [3, 4096]) {
    for (const [body, contentType] of [
      [Buffer.from(' { "x": 1, "x": 2 } '), "application/json"],
      [Buffer.from([97,255,195,40,98]), "text/plain"],
    ]) {
      const channel = Object.create(Channel.prototype);
      EventEmitter.call(channel);
      const received = Promise.withResolvers();
      const consumers = new Map();
      channel.id = 1;
      channel._state = { consumers, maxFrameSize: size + 8, stream: { writable: false } };
      if (method === "BasicDeliver") consumers.set("fixture", received.resolve);
      else if (method === "BasicReturn") channel.once("basic.return", received.resolve);
      else channel._state.rpc = [received, Cmd.BasicGet, Cmd.BasicGetOK];
      channel._onMethod({
        type: FrameType.METHOD, channelId: 1, methodId: Cmd[method],
        params: {
          consumerTag: "fixture", exchange: "fixture", routingKey: "", deliveryTag: 1,
          redelivered: false, messageCount: 0, replyCode: 312, replyText: "NO_ROUTE",
        },
      });
      channel._onHeader({
        type: FrameType.HEADER, channelId: 1, bodySize: body.length,
        fields: { contentType, messageId: "original:id" },
      });
      for (let offset = 0; offset < body.length; offset += size) {
        channel._onBody({
          type: FrameType.BODY, channelId: 1,
          payload: body.subarray(offset, offset + size),
        });
      }
      const message = await received.promise;
      assert.deepEqual(message.rawBody, body);
      const mapped = toConsumedMessage(message);
      assert.deepEqual(mapped.body, body);
      assert.equal(mapped.originalMessageId, "original:id");
      message.rawBody.fill(0);
      assert.deepEqual(mapped.body, body);
    }
  }

}
console.log("Packed D2 runtime and raw-byte mapping passed.");
`;
    await writeFile(join(consumer, "probe.mjs"), script);
    return runPackCommandFixture(
      process.execPath,
      [join(consumer, "probe.mjs")],
      consumer,
    );
  }

  async installUnpatchedClient(): Promise<string> {
    const producer = join(this.directory, "unpatched");
    await mkdir(producer);
    await writeFile(
      join(producer, "package.json"),
      JSON.stringify({
        name: "unpatched-client-fixture",
        private: true,
        dependencies: { "rabbitmq-client": "5.0.8" },
      }),
    );
    await writeFile(join(producer, "pnpm-workspace.yaml"), "packages: []\n");
    await writeFile(
      join(producer, ".npmrc"),
      "ignore-scripts=true\nlogs-max=0\nnode-linker=hoisted\n",
    );
    await this.pnpm(["install", "--ignore-scripts"], producer);
    return join(producer, "node_modules", "rabbitmq-client");
  }

  async typeCheckConsumer(consumer: string): Promise<void> {
    await writeFile(
      join(consumer, "probe.ts"),
      `import { type D2Connection, subscribe, type RabbitMqSubscriberOptions } from "@dcsv-io/d2-messaging-rabbitmq";
declare const connection: D2Connection;
declare const options: RabbitMqSubscriberOptions;
void connection.close(); void options; void subscribe;
`,
    );
    await this.pnpm(
      [
        "exec",
        "tsc",
        "--noEmit",
        "--strict",
        "--module",
        "NodeNext",
        "--target",
        "ES2022",
        "probe.ts",
      ],
      consumer,
    );
  }

  async replaceBundledClient(consumer: string, source: string): Promise<void> {
    const target = await realpath(
      join(
        consumer,
        "node_modules",
        "@dcsv-io",
        "d2-messaging-rabbitmq",
        "node_modules",
        "rabbitmq-client",
      ),
    );
    if (!target.startsWith((await realpath(this.directory)) + sep))
      throw new Error("Client replacement escaped the fixture directory.");
    await rm(target, { recursive: true });
    await cp(source, target, { recursive: true });
  }

  async probeLinuxConsumer(consumer: string): Promise<string> {
    const { GenericContainer, Wait } = await import("testcontainers");
    const container = await new GenericContainer("node:24-bookworm")
      .withCommand(["sh", "-c", "echo fixture-ready; tail -f /dev/null"])
      .withWaitStrategy(Wait.forLogMessage("fixture-ready"))
      .withCopyDirectoriesToContainer([
        {
          source: join(this.directory, "dependencies"),
          target: "/fixture/dependencies",
        },
        { source: join(this.directory, "first"), target: "/fixture/first" },
      ])
      .withCopyFilesToContainer(
        [
          "package.json",
          "pnpm-workspace.yaml",
          ".npmrc",
          "probe.mjs",
          "probe.ts",
        ].map((name) => ({
          source: join(consumer, name),
          target: `/fixture/consumer/${name}`,
        })),
      )
      .start();
    try {
      const result = await container.exec([
        "timeout",
        "120s",
        "sh",
        "-c",
        "corepack enable && corepack prepare pnpm@10.15.0 --activate && pnpm --dir /fixture/consumer install --ignore-scripts && node /fixture/consumer/probe.mjs && pnpm --dir /fixture/consumer exec tsc --noEmit --strict --module NodeNext --target ES2022 probe.ts",
      ]);
      if (result.exitCode !== 0)
        throw new Error(
          `Linux packed-consumer command exited ${result.exitCode}: ${result.output}`,
        );
      return result.output;
    } finally {
      await container.stop();
    }
  }
}

export async function loadPackHelperFixture(): Promise<{
  packPatched(
    request: { packageRoot: string; destination: string },
    run?: PackCommandRunnerFixture,
  ): Promise<{ artifact: string; sha256: string }>;
  verifyClientTree(root: string, provenance: unknown): Promise<void>;
}> {
  const workspace = findPackWorkspaceFixture();
  const monorepo = join(
    workspace,
    "public",
    "packages",
    "typescript",
    "messaging",
    "rabbitmq",
  );
  const exported = join(
    workspace,
    "packages",
    "typescript",
    "messaging",
    "rabbitmq",
  );
  const root = existsSync(monorepo) ? monorepo : exported;
  return import(pathToFileURL(join(root, "scripts", "pack-patched.mjs")).href);
}

export async function findMessagingPackageFixture(): Promise<string> {
  const workspace = findPackWorkspaceFixture();
  const candidates = [
    join(
      workspace,
      "public",
      "packages",
      "typescript",
      "messaging",
      "rabbitmq",
    ),
    join(workspace, "packages", "typescript", "messaging", "rabbitmq"),
  ];
  for (const candidate of candidates) {
    if (
      existsSync(join(candidate, "package.json")) &&
      JSON.parse(await readFile(join(candidate, "package.json"), "utf8"))
        .name === "@dcsv-io/d2-messaging-rabbitmq"
    )
      return candidate;
  }
  throw new Error("Messaging package was not found in the fixture workspace.");
}
