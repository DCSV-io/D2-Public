// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { createHash } from "node:crypto";
import { spawn } from "node:child_process";
import { constants } from "node:fs";
import {
  copyFile,
  cp,
  lstat,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  realpath,
  rm,
  writeFile,
} from "node:fs/promises";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import {
  basename,
  dirname,
  isAbsolute,
  join,
  relative,
  resolve,
  sep,
} from "node:path";
import { fileURLToPath } from "node:url";

const packageDirectory = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const patchName = "rabbitmq-client@5.0.8.patch";
const clientVersion = "5.0.8";
const upstreamIntegrity =
  "sha512-nNto/okuVq+d/OamRYX4HIiCkHdOE0RICIDRjS4YQUo2DI/l3R9RlsFaEEYSjIc+LT6DV+1wUub3nD1HwrM4SQ==";
const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");
const json = async (path) => JSON.parse(await readFile(path, "utf8"));

async function command(program, args, cwd, environment = {}) {
  return new Promise((resolveCommand, reject) => {
    const child = spawn(program, args, {
      cwd,
      env: { ...process.env, ...environment },
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let output = "";
    let error = "";
    child.stdout.on("data", (chunk) => {
      output += chunk;
    });
    child.stderr.on("data", (chunk) => {
      error += chunk;
    });
    child.once("error", reject);
    child.once("close", (code) => {
      if (code === 0) resolveCommand(output);
      else
        reject(
          new Error(
            `${program} ${args.join(" ")} exited ${code}: ${error}${output}`,
          ),
        );
    });
  });
}

function pnpmCommand() {
  const entry = process.env.npm_execpath;
  if (!entry || !/pnpm\.(?:c?js)$/i.test(entry))
    throw new Error(
      "Run this helper through pnpm run pack:patched so npm_execpath identifies pnpm.",
    );
  return [process.execPath, entry];
}

async function pnpm(args, cwd, environment, run) {
  const [program, entry] = pnpmCommand();
  return run(program, [entry, ...args], cwd, environment);
}

export function validateArchiveMembers(names, details, allowed) {
  const expected = new Set(allowed);
  const seen = new Set();
  if (names.length !== details.length)
    throw new Error("Archive member listing is inconsistent.");
  names.forEach((name, index) => {
    if (
      !name.startsWith("package/") ||
      name.includes("\\") ||
      /[\x00-\x1f:]/.test(name) ||
      name.split("/").some((part) => part === ".." || part === ".") ||
      !expected.has(name) ||
      seen.has(name) ||
      details[index][0] !== "-"
    )
      throw new Error(`Unsafe or unexpected archive member: ${name}`);
    seen.add(name);
  });
  if (seen.size !== expected.size)
    throw new Error(
      `Archive member set is incomplete: ${[...expected].filter((name) => !seen.has(name)).join(", ")}`,
    );
}

async function regularFiles(root) {
  const files = [];
  const walk = async (directory) => {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const fullPath = join(directory, entry.name);
      const stat = await lstat(fullPath);
      if (stat.isSymbolicLink())
        throw new Error(`Symbolic link is not a pack input: ${fullPath}`);
      if (stat.isDirectory()) await walk(fullPath);
      else if (stat.isFile())
        files.push(relative(root, fullPath).split(sep).join("/"));
      else throw new Error(`Non-regular pack input: ${fullPath}`);
    }
  };
  const rootStat = await lstat(root);
  if (!rootStat.isDirectory() || rootStat.isSymbolicLink())
    throw new Error(`Invalid pack input directory: ${root}`);
  await walk(root);
  return files.sort();
}

async function hashes(root) {
  return Object.fromEntries(
    await Promise.all(
      (await regularFiles(root)).map(async (name) => [
        name,
        sha256(await readFile(join(root, name))),
      ]),
    ),
  );
}

export async function verifyClientTree(root, provenance) {
  const actual = await hashes(root);
  if (JSON.stringify(actual) !== JSON.stringify(provenance.files))
    throw new Error(
      "Bundled rabbitmq-client file set or SHA-256 differs from provenance.",
    );
  const manifest = await json(join(root, "package.json"));
  if (
    manifest.version !== clientVersion ||
    manifest.name !== "rabbitmq-client" ||
    actual.LICENSE !== provenance.licenseSha256
  )
    throw new Error(
      "Bundled rabbitmq-client version or license differs from provenance.",
    );
}

async function producer(root, scratch, refresh, run) {
  await validateClientPins(root);
  const target = join(scratch, "producer");
  await mkdir(join(target, "patches"), { recursive: true });
  const templates = [
    ["producer.package.json", "package.json"],
    ["producer.pnpm-workspace.yaml", "pnpm-workspace.yaml"],
    ["producer.npmrc", ".npmrc"],
  ];
  if (!refresh) templates.push(["producer.pnpm-lock.yaml", "pnpm-lock.yaml"]);
  for (const [source, destination] of templates)
    await copyFile(join(root, "packaging", source), join(target, destination));
  await copyFile(
    join(root, "patches", patchName),
    join(target, "patches", patchName),
  );
  await pnpm(
    ["install", "--ignore-scripts", ...(refresh ? [] : ["--frozen-lockfile"])],
    target,
    {},
    run,
  );
  const lock = await readFile(join(target, "pnpm-lock.yaml"), "utf8");
  if (!lock.includes(upstreamIntegrity))
    throw new Error("Producer lock is missing the pinned registry integrity.");
  return target;
}

async function validateClientPins(root) {
  for (const name of ["package.json", "packaging/producer.package.json"]) {
    const manifest = await json(join(root, name));
    if (manifest.dependencies?.["rabbitmq-client"] !== clientVersion)
      throw new Error(
        `Client dependency pin differs from the producer contract: ${name}`,
      );
  }
}

async function withScratch(action) {
  const parent = await realpath(tmpdir());
  const scratch = await mkdtemp(join(parent, "d2-rabbitmq-pack-"));
  try {
    return await action(scratch);
  } finally {
    const actual = await realpath(scratch);
    if (
      dirname(actual) !== parent ||
      !basename(actual).startsWith("d2-rabbitmq-pack-")
    )
      throw new Error(
        "Refusing cleanup outside the owned pack scratch directory.",
      );
    await rm(actual, { recursive: true });
  }
}

export async function refreshProducer(
  packageRoot = packageDirectory,
  run = command,
) {
  return withScratch(async (scratch) => {
    const root = resolve(packageRoot);
    const target = await producer(root, scratch, true, run);
    const files = await hashes(join(target, "node_modules", "rabbitmq-client"));
    const provenance = {
      generatedBy: "scripts/pack-patched.mjs --refresh-producer",
      version: clientVersion,
      upstreamUrl: `https://registry.npmjs.org/rabbitmq-client/-/rabbitmq-client-${clientVersion}.tgz`,
      integrity: upstreamIntegrity,
      patchSha256: sha256(await readFile(join(root, "patches", patchName))),
      licenseSha256: files.LICENSE,
      files,
    };
    await copyFile(
      join(target, "pnpm-lock.yaml"),
      join(root, "packaging", "producer.pnpm-lock.yaml"),
    );
    await writeFile(
      join(root, "patches", "rabbitmq-client.json"),
      `${JSON.stringify(provenance, null, 2)}\n`,
    );
    return provenance;
  });
}

// GNU tar reads an archive name with a colon before its first slash as
// host:file, so every tar argument is relative to the working directory.
async function inspectArchive(archive, allowed, run) {
  const lines = (text) => text.trimEnd().split(/\r?\n/).filter(Boolean);
  const cwd = dirname(archive);
  const name = basename(archive);
  const names = lines(await run("tar", ["-tzf", name], cwd));
  const details = lines(await run("tar", ["-tvzf", name], cwd));
  validateArchiveMembers(names, details, allowed);
}

async function extractArchive(archive, target, run) {
  const name = relative(target, archive).split(sep).join("/");
  await run("tar", ["-xzf", name], target);
}

async function oneArchive(directory) {
  const entries = (await readdir(directory)).filter((name) =>
    name.endsWith(".tgz"),
  );
  if (entries.length !== 1)
    throw new Error("Native pack must produce exactly one archive.");
  return join(directory, entries[0]);
}

async function licenseInputs(root) {
  let current = root;
  let apache;
  for (;;) {
    const names = await readdir(current);
    if (!apache && names.includes("LICENSE")) {
      const candidate = join(current, "LICENSE");
      if ((await readFile(candidate, "utf8")).includes("Apache License"))
        apache = candidate;
    }
    if (names.includes("pnpm-workspace.yaml")) {
      const inherited = names.filter((name) =>
        /^licen[cs]e(?:\..*)?$/i.test(name),
      );
      if (!apache)
        throw new Error(
          "Public Apache license was not found before the workspace boundary.",
        );
      return {
        apache,
        inherited: inherited.map((name) => ({
          name,
          path: join(current, name),
        })),
      };
    }
    const parent = dirname(current);
    if (parent === current)
      throw new Error("Pack source has no workspace boundary.");
    current = parent;
  }
}

export async function packPatched(request, run = command) {
  const root = await realpath(request.packageRoot);
  const destination = resolve(request.destination);
  if (!isAbsolute(destination))
    throw new Error("Pack destination must be absolute.");
  return withScratch(async (scratch) => {
    let phase = "sourcePack";
    try {
      await validateClientPins(root);
      const provenance = await json(
        join(root, "patches", "rabbitmq-client.json"),
      );
      if (
        provenance.version !== clientVersion ||
        provenance.integrity !== upstreamIntegrity ||
        provenance.patchSha256 !==
          sha256(await readFile(join(root, "patches", patchName)))
      )
        throw new Error("Client patch provenance is stale.");
      const source = join(scratch, "source");
      const stage = join(scratch, "stage");
      const output = join(scratch, "output");
      for (const directory of [source, stage, output]) await mkdir(directory);
      await pnpm(
        ["pack", "--pack-destination", source],
        root,
        { D2_PATCHED_PACK_INTERMEDIATE: source },
        run,
      );
      const intermediate = await oneArchive(source);
      phase = "extract";
      const rootFiles = (await readdir(root)).filter((name) =>
        /^(?:readme|licen[cs]e|copying)(?:\..*)?$/i.test(name),
      );
      const licenses = await licenseInputs(root);
      const inherited = licenses.inherited.filter(
        (license) =>
          !rootFiles.some((name) => /^licen[cs]e(?:\..*)?$/i.test(name)),
      );
      const allowed = [
        "package/package.json",
        ...(await regularFiles(join(root, "dist"))).map(
          (name) => `package/dist/${name}`,
        ),
        ...rootFiles.map((name) => `package/${name}`),
        ...inherited.map((license) => `package/${license.name}`),
      ];
      await inspectArchive(intermediate, allowed, run);
      await extractArchive(intermediate, stage, run);
      const stagedPackage = join(stage, "package");
      for (const license of inherited) {
        const target = join(stagedPackage, license.name);
        if (!(await readFile(target)).equals(await readFile(license.path)))
          throw new Error("Native inherited license differs from its source.");
        await copyFile(licenses.apache, target);
      }
      phase = "install";
      const produced = await producer(root, scratch, false, run);
      const client = join(produced, "node_modules", "rabbitmq-client");
      await verifyClientTree(client, provenance);
      await mkdir(join(stagedPackage, "node_modules"));
      await cp(client, join(stagedPackage, "node_modules", "rabbitmq-client"), {
        recursive: true,
        dereference: false,
      });
      phase = "bundle";
      const manifest = await json(join(stagedPackage, "package.json"));
      for (const field of [
        "dependencies",
        "devDependencies",
        "optionalDependencies",
        "peerDependencies",
      ]) {
        if (manifest[field])
          manifest[field] = Object.fromEntries(
            Object.entries(manifest[field]).sort(([left], [right]) =>
              left < right ? -1 : left > right ? 1 : 0,
            ),
          );
      }
      manifest.bundleDependencies = ["rabbitmq-client"];
      delete manifest.scripts;
      delete manifest.d2PackInputs;
      delete manifest.pnpm;
      await writeFile(
        join(stagedPackage, "package.json"),
        `${JSON.stringify(manifest, null, 2)}\n`,
      );
      await copyFile(
        join(root, "packaging", "producer.pnpm-workspace.yaml"),
        join(stagedPackage, "pnpm-workspace.yaml"),
      );
      await copyFile(
        join(root, "packaging", "producer.npmrc"),
        join(stagedPackage, ".npmrc"),
      );
      await pnpm(
        ["pack", "--pack-destination", output],
        stagedPackage,
        {},
        run,
      );
      const archive = await oneArchive(output);
      phase = "verify";
      await inspectArchive(
        archive,
        [
          ...allowed,
          ...Object.keys(provenance.files).map(
            (name) => `package/node_modules/rabbitmq-client/${name}`,
          ),
        ],
        run,
      );
      const verified = join(scratch, "verified");
      await mkdir(verified);
      await extractArchive(archive, verified, run);
      const verifiedPackage = join(verified, "package");
      for (const license of inherited) {
        if (
          !(await readFile(join(verifiedPackage, license.name))).equals(
            await readFile(licenses.apache),
          )
        )
          throw new Error(
            "Packed public license differs from the Apache source.",
          );
      }
      await verifyClientTree(
        join(verifiedPackage, "node_modules", "rabbitmq-client"),
        provenance,
      );
      const resolvedClient = createRequire(
        join(verifiedPackage, "package.json"),
      ).resolve("rabbitmq-client");
      if (
        !resolvedClient.startsWith(
          join(verifiedPackage, "node_modules", "rabbitmq-client") + sep,
        )
      )
        throw new Error(
          "Packed client resolves outside its bundled directory.",
        );
      phase = "publish";
      await mkdir(destination, { recursive: true });
      const artifact = join(destination, basename(archive));
      await copyFile(archive, artifact, constants.COPYFILE_EXCL);
      return { artifact, sha256: sha256(await readFile(archive)) };
    } catch (error) {
      throw new Error(`Patched pack failed during ${phase}: ${error.message}`, {
        cause: error,
      });
    }
  });
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  const args = process.argv.slice(2).filter((value) => value !== "--");
  try {
    const result =
      args[0] === "--refresh-producer"
        ? await refreshProducer()
        : await packPatched({
            packageRoot: packageDirectory,
            destination: args[0] ?? "artifacts",
          });
    console.log(JSON.stringify(result, null, 2));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
