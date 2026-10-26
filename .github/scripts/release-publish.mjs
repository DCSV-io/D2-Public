// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------
//
// Checks the package files a release-manifest.json lists against it and
// publishes them to nuget.org and npm in the manifest's order. Node only, no
// dependencies.
//
//   node .github/scripts/release-publish.mjs check \
//     --manifest release-manifest.json --nuget <dir> --npm <dir>
//   node .github/scripts/release-publish.mjs publish \
//     --manifest release-manifest.json --nuget <dir> --npm <dir>
//   node .github/scripts/release-publish.mjs notes \
//     --manifest release-manifest.json --tag libs/<YYYYMMDD>T<HHMMSS>Z
//   node .github/scripts/release-publish.mjs ecosystems \
//     --manifest release-manifest.json
//
// check validates the manifest and finds exactly one package file per entry:
// <nuget>/<id>.<version>.nupkg or <npm>/<scope>-<name>-<version>.tgz.
//
// publish runs check, then, for every entry before the first push: reads the
// id and version inside the file (and a tarball's repository.url) and compares
// them with the entry, runs npm publish --dry-run on a tarball or dotnet nuget
// push to a new local folder on a nupkg and compares the id and version the
// registry's own client reads with the entry (for a nupkg, it also requires
// the nuspec NuGet extracts to be byte for byte the nuspec this script read),
// and checks that every exact pin on another DcsvIo.D2.* or @dcsv-io/d2-*
// package is a version the registry lists or an earlier entry of this
// manifest. Then, for each entry in the manifest's order, it reads the
// registry, skips a version it lists, and otherwise pushes the file. It stops
// at the first failure, so a re-run publishes only the versions the registry
// does not list. The NuGet key comes from the NUGET_PUSH_KEY environment
// variable and reaches dotnet nuget push only as its --api-key argument; npm
// authenticates with trusted publishing.
// The dry run and the local push get neither the key nor the variables npm
// reads for its OIDC token exchange. The dry run runs with --force and
// --offline, and the local push writes only to a folder, so neither sends a
// request. NuGet verifies a package signature during the local push, and a
// signed nupkg is not supported.
//
// A nupkg's zip central directory is read in this process and the package is
// refused when the archive has a comment or bytes after its end record, a
// central directory that does not end where the end record starts, or an
// entry whose local header has another name, flags, compression method,
// CRC-32 or size than its central record, whose sizes are in a data
// descriptor, or whose central record has a ZIP64 size or offset or a disk
// other than 0; an entry name has a NUL, a %, a space, a backslash, a
// character other than printable ASCII, an empty, . or .. segment, or a
// segment that starts with -; two names are equal in any letter case; unzip
// -Z1 lists other names; or there is not exactly one root .nuspec.
//
// A tarball is read in this process and refused when it is larger than 256
// MiB packed or unpacked, or has an entry header with a bad checksum, a header
// that is not POSIX ustar, an entry that is not a regular file or a directory
// (a link, or a pax or GNU extended header), a path outside package/, a path
// with an empty, . or .. segment, a space, a backslash or a character other
// than printable ASCII, a header that is not byte for byte the one pnpm pack
// or npm pack writes for the values read from it, a path listed twice in any
// letter case, a truncated entry, or data after the end of the archive. The
// nuspec and package.json reads are capped at 1 MiB.
//
// Every error is printed between ::stop-commands:: and its resume token, so
// text from a package file or a push cannot run a GitHub Actions workflow
// command.
//
// notes writes the GitHub Release body to standard output. ecosystems writes
// nuget=true or nuget=false to the file at the path in GITHUB_OUTPUT.
//
// Exit 0 on success, 1 when a publish step fails (the identity and pin checks
// included), 2 on a usage error or a problem check finds in the manifest or
// the package file names.

import { spawnSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import {
  appendFileSync,
  closeSync,
  fstatSync,
  lstatSync,
  mkdirSync,
  mkdtempSync,
  openSync,
  readdirSync,
  readFileSync,
  readSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { gunzipSync } from "node:zlib";

/** NuGet and npm package id forms. */
export const NUGET_ID_RE = /^[A-Za-z0-9_.-]+$/;
export const NPM_ID_RE = /^@[a-z0-9][a-z0-9._-]*\/[a-z0-9][a-z0-9._-]*$/;

/** SemVer 2.0 MAJOR.MINOR.PATCH with an optional prerelease and no build metadata. */
export const SEMVER_RE =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?$/;

/** The release tag form. */
export const TAG_RE = /^libs\/[0-9]{8}T[0-9]{6}Z$/;

/** The repository.url of every published npm package. */
export const REPOSITORY_URL = "git+https://github.com/DCSV-io/D2-Public.git";

export const NUGET_SOURCE = "https://api.nuget.org/v3/index.json";
export const NUGET_FLAT_CONTAINER = "https://api.nuget.org/v3-flatcontainer/";
export const NPM_REGISTRY = "https://registry.npmjs.org/";

export const READ_TIMEOUT_MS = 30_000;
export const PUSH_TIMEOUT_MS = 10 * 60_000;
export const EXTRACT_TIMEOUT_MS = 60_000;

/** The most output read from a package file: 1 MiB. */
export const MAX_EMBEDDED_BYTES = 1024 * 1024;

/** The most bytes a tarball may hold, packed or unpacked: 256 MiB. */
export const MAX_TARBALL_BYTES = 256 * 1024 * 1024;

/** The most output kept from a push command: 16 MiB. */
export const MAX_PUSH_OUTPUT_BYTES = 16 * 1024 * 1024;

/** The longest npm publish --dry-run may run: 2 minutes. */
export const DRY_RUN_TIMEOUT_MS = 2 * 60_000;

/** The most output read from npm publish --dry-run: 16 MiB. */
export const MAX_DRY_RUN_OUTPUT_BYTES = 16 * 1024 * 1024;

/**
 * The npm version whose npm publish --dry-run --json output checkNpmReading
 * reads, and the version release-libs.yml installs. Before changing it, run
 * the new version's dry run on a pnpm pack tarball with npmDryRunArgs, the
 * NPM_OIDC_ENV variables unset and no network, and check that checkNpmReading
 * reads the output.
 */
export const NPM_DRY_RUN_VERIFIED_VERSION = "11.21.0";

/** The longest the local dotnet nuget push that reads a nupkg may run: 2 minutes. */
export const NUGET_READING_TIMEOUT_MS = 2 * 60_000;

/** The most output read from that local push: 16 MiB. */
export const MAX_NUGET_READING_OUTPUT_BYTES = 16 * 1024 * 1024;

/**
 * The folder whose {name}/{version}/{name}.nuspec file makes dotnet nuget push
 * treat a local folder as a hierarchical feed, in which it writes a package to
 * {id}/{version}/ in lower case, with the version in NuGet's normalized form
 * (IsV2LocalRepository in NuGet.Protocol's PackageUpdateResource). In an empty
 * folder NuGet writes {id}.{version}.nupkg, and that name does not show where
 * the id ends and the version starts.
 */
const NUGET_LAYOUT_SEED = "release-publish-layout";

/**
 * The arguments of the npm publish --dry-run that reads a tarball's name and
 * version. --force skips npm's read of the registry's version list, which
 * fails the dry run for a version already published, as on a re-run, and
 * --offline stops npm's registry client from sending any other request.
 */
export function npmDryRunArgs(file) {
  return [
    "publish",
    file,
    "--dry-run",
    "--json",
    "--ignore-scripts",
    "--force",
    "--offline",
  ];
}

/**
 * The environment variables npm reads to exchange a CI identity token for a
 * publish token. npm publish --dry-run makes that exchange when they are set,
 * with or without --offline, so the dry run's environment has none of them.
 */
export const NPM_OIDC_ENV = [
  "ACTIONS_ID_TOKEN_REQUEST_URL",
  "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
  "NPM_ID_TOKEN",
];

/** Seconds to wait before each retry of a rate-limited or failing request. */
export const BACKOFF_SECONDS = [1, 2, 4, 8, 16, 32];

/** The longest Retry-After wait honored, in seconds. */
export const MAX_RETRY_AFTER_SECONDS = 60;

/** The prefix of each id this release publishes, per ecosystem. */
const OUR_PREFIX = { nuget: "dcsvio.d2.", npm: "@dcsv-io/d2-" };

// dotnet nuget push prints an HTTP 429 as "Response status code does not
// indicate success: 429 (Too Many Requests).", or without the reason phrase.
// A reason phrase alone is not matched, because it can follow another status.
const RATE_LIMITED = {
  nuget: /Response status code does not indicate success: 429\b/i,
  npm: /E429|Too Many Requests|rate limit/i,
};

const ALREADY_EXISTS = {
  nuget:
    /already exists at feed|already exists and cannot be modified|409 \(Conflict/i,
  npm: /cannot publish over existing|EPUBLISHCONFLICT|previously published|403 Forbidden.*cannot publish/i,
};

const AUTHORIZATION_REJECTED = /\b40[134]\b|E401|E403|E404|ENEEDAUTH/;

/** A usage or input problem found before anything is published: exit 2. */
export class UsageError extends Error {
  name = "UsageError";
}

/** A publish step failed: exit 1. */
export class PublishFailure extends Error {
  name = "PublishFailure";
}

/** The ecosystem of an id, or undefined when it is neither form. */
export function idEcosystem(id) {
  if (id.startsWith("@")) return NPM_ID_RE.test(id) ? "npm" : undefined;

  return NUGET_ID_RE.test(id) ? "nuget" : undefined;
}

/** True when `id` is a valid id of `ecosystem` that starts with @, a letter or a digit. */
export function isValidId(id, ecosystem) {
  return idEcosystem(id) === ecosystem && /^[@A-Za-z0-9]/.test(id);
}

/** True for a SemVer 2.0 version without build metadata. */
export function isSemver(version) {
  return typeof version === "string" && SEMVER_RE.test(version);
}

/** The id as the registry compares it: lower case for NuGet. */
function idKey(ecosystem, id) {
  return ecosystem === "nuget" ? id.toLowerCase() : id;
}

/**
 * The problems of a parsed release-manifest.json, each a sentence; an empty
 * list when it is valid.
 */
export function manifestProblems(value) {
  if (value === null || typeof value !== "object" || Array.isArray(value))
    return ["the manifest is not a JSON object"];

  if (!Array.isArray(value.packages) || value.packages.length === 0)
    return ["the manifest has no non-empty packages array"];

  const problems = [];
  // The ids of the earlier entries as written, which deps name, and as the
  // registry compares them, which finds an id listed twice.
  const earlierIds = new Set();
  const earlierKeys = new Set();

  value.packages.forEach((entry, index) => {
    const at = `packages[${index}]`;

    if (entry === null || typeof entry !== "object" || Array.isArray(entry)) {
      problems.push(`${at} is not an object`);

      return;
    }

    const keys = Object.keys(entry).sort().join(",");

    if (keys !== "deps,ecosystem,id,version") {
      problems.push(
        `${at} has the fields ${keys || "(none)"}; it must have exactly deps, ecosystem, id and version`,
      );

      return;
    }

    const { deps, ecosystem, id, version } = entry;

    if (ecosystem !== "nuget" && ecosystem !== "npm") {
      problems.push(`${at} has the ecosystem ${JSON.stringify(ecosystem)}`);

      return;
    }

    if (typeof id !== "string" || !isValidId(id, ecosystem)) {
      problems.push(
        `${at} has the id ${JSON.stringify(id)}, which is not a valid ${ecosystem} package id`,
      );

      return;
    }

    if (!isSemver(version)) {
      problems.push(
        `${id} has the version ${JSON.stringify(version)}, which is not a SemVer version without build metadata`,
      );
    } else if (ecosystem === "npm" && version.includes("-")) {
      problems.push(
        `${id} has the prerelease version ${version}; an npm prerelease is not published`,
      );
    }

    const key = idKey(ecosystem, id);

    if (earlierKeys.has(key)) problems.push(`${id} is listed twice`);

    if (!Array.isArray(deps) || deps.some((dep) => typeof dep !== "string")) {
      problems.push(`${id} has deps that are not a list of ids`);
    } else {
      for (const dep of deps) {
        if (!earlierIds.has(dep)) {
          problems.push(
            `${id} depends on ${dep}, which is not an earlier entry of the manifest`,
          );
        }
      }
    }

    earlierIds.add(id);
    earlierKeys.add(key);
  });

  return problems;
}

/** Read and validate the manifest file. Throws UsageError. */
export function readManifest(file) {
  let text;

  try {
    text = readFileSync(file, "utf-8");
  } catch (error) {
    throw new UsageError(`cannot read the manifest ${file}: ${error.message}`);
  }

  let value;

  try {
    value = JSON.parse(text);
  } catch (error) {
    throw new UsageError(`the manifest ${file} is not JSON: ${error.message}`);
  }

  const problems = manifestProblems(value);

  if (problems.length > 0) {
    throw new UsageError(
      `the manifest ${file} is not valid:\n${problems.map((p) => `  ${p}`).join("\n")}`,
    );
  }

  return value.packages;
}

/** The file name of an entry's package: the nupkg dotnet pack writes, or the tarball pnpm pack writes. */
export function packageFileName(entry) {
  return entry.ecosystem === "nuget"
    ? `${entry.id}.${entry.version}.nupkg`
    : `${entry.id.replace(/^@/, "").replace(/\//g, "-")}-${entry.version}.tgz`;
}

/**
 * The package file of each entry, in manifest order. Throws UsageError that
 * lists every entry with no file, more than one candidate, a link or a
 * directory in place of the file, and every file name two entries share.
 */
export function locateFiles(packages, dirs) {
  const problems = [];
  const owners = new Map();
  const listings = new Map();
  const listing = (dir) => {
    if (!listings.has(dir)) {
      try {
        listings.set(dir, readdirSync(dir));
      } catch {
        listings.set(dir, []);
      }
    }

    return listings.get(dir);
  };
  const files = [];

  for (const entry of packages) {
    const dir = dirs[entry.ecosystem];
    const name = packageFileName(entry);
    const lower = name.toLowerCase();
    const owner = owners.get(lower);

    if (owner !== undefined) {
      problems.push(`${entry.id} and ${owner} both map to the file ${name}`);
      continue;
    }

    owners.set(lower, entry.id);

    const candidates = listing(dir).filter(
      (file) => file.toLowerCase() === lower,
    );

    if (candidates.length > 1) {
      problems.push(
        `${entry.id} has more than one candidate file in ${dir}: ${candidates.sort().join(", ")}`,
      );
      continue;
    }

    if (candidates.length === 0 || candidates[0] !== name) {
      problems.push(
        `${entry.id} ${entry.version} has no file ${name} in ${dir}`,
      );
      continue;
    }

    const file = path.resolve(dir, name);
    const stat = lstatSync(file);

    if (stat.isSymbolicLink()) {
      problems.push(`${file} is a symbolic link`);
    } else if (!stat.isFile()) {
      problems.push(`${file} is not a regular file`);
    } else {
      files.push({ entry, file });
    }
  }

  if (problems.length > 0) {
    throw new UsageError(
      `the package files do not match the manifest:\n${problems.map((p) => `  ${p}`).join("\n")}`,
    );
  }

  return files;
}

/** The `check` command: the manifest and one file per entry. */
export function check(options) {
  const packages = readManifest(options.manifest);

  return locateFiles(packages, { nuget: options.nuget, npm: options.npm });
}

/**
 * Run a program with no shell, under a timeout and an output limit, in
 * `options.env` or this process's environment without NUGET_PUSH_KEY.
 */
export function spawnRunner(program, args, options) {
  const { NUGET_PUSH_KEY: _key, ...env } = options.env ?? process.env;
  const result = spawnSync(program, args, {
    encoding: "utf-8",
    shell: false,
    timeout: options.timeoutMs,
    maxBuffer: options.maxBuffer,
    windowsHide: true,
    env,
  });

  return {
    status: result.status,
    signal: result.signal,
    stdout: result.stdout ?? "",
    stderr: result.stderr ?? "",
    ...(result.error === undefined ? {} : { error: result.error }),
  };
}

/** Run an extraction command and return its standard output; throws PublishFailure. */
function extract(run, what, program, args) {
  const result = run(program, args, {
    timeoutMs: EXTRACT_TIMEOUT_MS,
    maxBuffer: MAX_EMBEDDED_BYTES,
  });

  if (result.error?.code === "ENOBUFS") {
    throw new PublishFailure(
      `${what} is larger than ${MAX_EMBEDDED_BYTES} bytes`,
    );
  }

  if (result.error !== undefined)
    throw new PublishFailure(`cannot read ${what}: ${result.error.message}`);

  if (result.signal !== null || result.status !== 0) {
    throw new PublishFailure(
      `cannot read ${what}: ${program} exited ${String(result.status ?? result.signal)}: ${result.stderr.trim().slice(-2000)}`,
    );
  }

  return result.stdout;
}

const XML_ENTITIES = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'" };

/**
 * A character other than XML whitespace (space, tab, CR, LF). JavaScript's \s
 * and String.trim also match U+FEFF and other Unicode spaces, which XML and
 * .NET read as text.
 */
const XML_NON_WHITESPACE_RE = /[^ \t\r\n]/;

/** `text` without leading and trailing XML whitespace. */
function trimXmlWhitespace(text) {
  return text.replace(/^[ \t\r\n]+|[ \t\r\n]+$/g, "");
}

/** An element or attribute name without a namespace prefix. */
const XML_NAME_RE = /^[A-Za-z_][A-Za-z0-9._-]*$/;

/** The XML declaration of a UTF-8 document, the only one accepted. */
const XML_DECLARATION_RE =
  /^<\?xml\s+version\s*=\s*(["'])1\.0\1(?:\s+encoding\s*=\s*(["'])[Uu][Tt][Ff]-8\2)?(?:\s+standalone\s*=\s*(["'])(?:yes|no)\3)?\s*\?>/;

/** A character XML 1.0 does not allow in a document. */
const XML_INVALID_CHAR_RE =
  /[^\t\n\r\x20-\uD7FF\uE000-\uFFFD\u{10000}-\u{10FFFF}]/u;

const XML_START_TAG_RE =
  /<([^\s/>]+)((?:\s+[^\s=/>]+\s*=\s*(?:"[^"<]*"|'[^'<]*'))*)\s*(\/?)>/y;
const XML_END_TAG_RE = /<\/([^\s>]+)\s*>/y;
const XML_ATTRIBUTE_RE = /\s+([^\s=/>]+)\s*=\s*(?:"([^"<]*)"|'([^'<]*)')/g;
const XML_REFERENCE_RE =
  /&(?:#x([0-9A-Fa-f]+)|#([0-9]+)|(amp|lt|gt|quot|apos));/y;

/**
 * The element tree of an XML document, each element `{ name, attributes,
 * children }` with text children as strings. NuGet reads a nuspec with a full
 * XML parser, so this reader accepts only what it reads the same way:
 * elements, attributes, text, the predefined entities and character
 * references, a UTF-8 XML declaration and a default xmlns on the root. It
 * throws PublishFailure for a comment, a CDATA section, a processing
 * instruction, a DOCTYPE, a namespace prefix, an xmlns below the root and
 * anything that is not well formed.
 */
export function readXml(text, what) {
  const fail = (problem) => {
    throw new PublishFailure(
      `${what} ${problem}; only elements, attributes, text and the predefined entities are read`,
    );
  };
  const xml = text.startsWith("\uFEFF") ? text.slice(1) : text;

  if (XML_INVALID_CHAR_RE.test(xml))
    fail("has a character that XML does not allow");

  const decode = (raw) => {
    let out = "";
    let at = 0;

    for (let amp = raw.indexOf("&"); amp !== -1; amp = raw.indexOf("&", at)) {
      XML_REFERENCE_RE.lastIndex = amp;

      const match = XML_REFERENCE_RE.exec(raw);

      if (match === null)
        fail("has an entity reference other than the predefined ones");

      let decoded = XML_ENTITIES[match[3]];

      if (decoded === undefined) {
        const code =
          match[1] === undefined ? Number(match[2]) : parseInt(match[1], 16);

        decoded = code <= 0x10ffff ? String.fromCodePoint(code) : "\u0000";

        if (XML_INVALID_CHAR_RE.test(decoded))
          fail("has a character reference to a character XML does not allow");
      }

      out += raw.slice(at, amp) + decoded;
      at = amp + match[0].length;
    }

    return out + raw.slice(at);
  };

  let at = 0;

  if (xml.startsWith("<?xml")) {
    const declaration = XML_DECLARATION_RE.exec(xml);

    if (declaration === null)
      fail("has an XML declaration other than version 1.0 in UTF-8");

    at = declaration[0].length;
  }

  const stack = [];
  let root;

  while (at < xml.length) {
    const lt = xml.indexOf("<", at);
    const textEnd = lt === -1 ? xml.length : lt;

    if (textEnd > at) {
      const raw = xml.slice(at, textEnd);

      if (stack.length === 0) {
        if (XML_NON_WHITESPACE_RE.test(raw))
          fail(
            `has text ${root === undefined ? "before" : "after"} the root element`,
          );
      } else {
        if (raw.includes("]]>")) fail("has ]]> in its text");

        stack.at(-1).children.push(decode(raw));
      }

      at = textEnd;
      continue;
    }

    if (xml.startsWith("<!--", at)) fail("has a comment");
    if (xml.startsWith("<![CDATA[", at)) fail("has a CDATA section");
    if (xml.startsWith("<!", at)) fail("has a DOCTYPE or another declaration");
    if (xml.startsWith("<?", at)) fail("has a processing instruction");

    if (xml.startsWith("</", at)) {
      XML_END_TAG_RE.lastIndex = at;

      const end = XML_END_TAG_RE.exec(xml);

      if (end === null || stack.length === 0 || stack.at(-1).name !== end[1])
        fail("has a closing tag that does not match the open element");

      stack.pop();
      at += end[0].length;
      continue;
    }

    if (root !== undefined && stack.length === 0)
      fail("has an element after the root element");

    XML_START_TAG_RE.lastIndex = at;

    const start = XML_START_TAG_RE.exec(xml);

    if (start === null) fail("has a start tag that is not well formed");

    const [whole, name, attributeText, selfClosing] = start;

    if (!XML_NAME_RE.test(name))
      fail(
        `has the element name ${JSON.stringify(name)}, with a namespace prefix or a character a nuspec does not use`,
      );

    const attributes = new Map();

    for (const attribute of attributeText.matchAll(XML_ATTRIBUTE_RE)) {
      const attributeName = attribute[1];

      if (!XML_NAME_RE.test(attributeName))
        fail(
          `has the attribute name ${JSON.stringify(attributeName)} on <${name}>, with a namespace prefix or a character a nuspec does not use`,
        );

      if (attributes.has(attributeName))
        fail(`has the attribute ${attributeName} twice on <${name}>`);

      if (attributeName === "xmlns" && stack.length > 0)
        fail(`declares xmlns on <${name}>, below the root element`);

      attributes.set(
        attributeName,
        decode(attribute[2] ?? attribute[3]).replace(/[\t\n\r]/g, " "),
      );
    }

    const element = { name, attributes, children: [] };

    if (stack.length === 0) root = element;
    else stack.at(-1).children.push(element);

    if (selfClosing === "") stack.push(element);

    at += whole.length;
  }

  if (stack.length > 0) fail(`has the element <${stack.at(-1).name}> unclosed`);
  if (root === undefined) fail("has no root element");

  return root;
}

/** The id, version and dependencies of a nuspec. Throws PublishFailure. */
export function parseNuspec(xml, what) {
  const root = readXml(xml, what);
  const fail = (problem) => {
    throw new PublishFailure(`${what} ${problem}`);
  };
  const childElements = (parent, name) =>
    parent.children.filter(
      (child) => typeof child === "object" && child.name === name,
    );
  const single = (parent, name) => {
    const found = childElements(parent, name);

    if (found.length > 1)
      fail(`has more than one <${name}> in its <${parent.name}>`);

    return found[0];
  };
  const textOf = (element) => {
    if (element === undefined) return undefined;

    if (element.children.some((child) => typeof child === "object"))
      fail(`has an element inside its <${element.name}>`);

    return trimXmlWhitespace(element.children.join(""));
  };

  if (root.name !== "package")
    fail(`has the root element <${root.name}>, not <package>`);

  const metadata = single(root, "metadata");
  const id =
    metadata === undefined ? undefined : textOf(single(metadata, "id"));
  const version =
    metadata === undefined ? undefined : textOf(single(metadata, "version"));

  if (id === undefined || version === undefined)
    fail("has no <id> or <version> in its <metadata>");

  // NuGet reads the dependencies inside each <group> and, in a nuspec with no
  // groups, directly under <dependencies>; both are read here.
  const dependencies = [];

  for (const block of childElements(metadata, "dependencies")) {
    for (const child of block.children) {
      if (typeof child !== "object") continue;

      const declared =
        child.name === "group"
          ? childElements(child, "dependency")
          : child.name === "dependency"
            ? [child]
            : [];

      for (const dependency of declared) {
        if (dependency.attributes.has("id")) {
          dependencies.push({
            id: dependency.attributes.get("id"),
            version: dependency.attributes.get("version"),
          });
        }
      }
    }
  }

  return { id, version, dependencies };
}

const ZIP_END_SIZE = 22;
const ZIP_CENTRAL_SIZE = 46;
const ZIP_LOCAL_SIZE = 30;

/** The fields a zip local header and central record both have, with their offsets in each. */
const ZIP_SHARED_FIELDS = [
  ["general purpose flags", 6, 8, 2],
  ["compression method", 8, 10, 2],
  ["CRC-32", 14, 16, 4],
  ["compressed size", 18, 20, 4],
  ["uncompressed size", 22, 24, 4],
];

/**
 * The entry names of a zip's central directory, as raw bytes in directory
 * order, read in this process. Throws PublishFailure unless the end record
 * ends the file, has no comment, a single disk and an entry count equal to
 * the number of central records, the central directory ends where the end
 * record starts and is at most MAX_EMBEDDED_BYTES, and for each entry: it is
 * not encrypted, its sizes and CRC-32 are not in a data descriptor, its
 * central record has no ZIP64 size or offset (0xFFFFFFFF, or disk 0xFFFF) and
 * says disk 0, and its local header has the name, flags, compression method,
 * CRC-32 and sizes of its central record. unzip reads an entry's data by its
 * local header. NuGet's ZipArchive takes the start of the data from the local
 * header's name and extra field lengths, and the compression method and sizes
 * from the central record. The two read the same bytes only when those fields
 * agree.
 */
export function zipEntryNames(file) {
  const fail = (problem) => {
    throw new PublishFailure(`${file} ${problem}`);
  };
  let fd;

  try {
    fd = openSync(file, "r");

    const size = fstatSync(fd).size;
    const read = (offset, length, what) => {
      const bytes = Buffer.alloc(length);

      if (
        offset < 0 ||
        offset + length > size ||
        readSync(fd, bytes, 0, length, offset) !== length
      )
        fail(`ends inside ${what}`);

      return bytes;
    };

    if (size < ZIP_END_SIZE) fail("is too short to be a zip");

    const end = read(size - ZIP_END_SIZE, ZIP_END_SIZE, "the end record");

    if (end.readUInt32LE(0) !== 0x06054b50)
      fail("does not end with a zip end record without a comment");

    const count = end.readUInt16LE(10);
    const directorySize = end.readUInt32LE(12);
    const directoryOffset = end.readUInt32LE(16);

    if (
      end.readUInt16LE(4) !== 0 ||
      end.readUInt16LE(6) !== 0 ||
      end.readUInt16LE(8) !== count
    )
      fail("is a zip of more than one disk");

    if (directoryOffset + directorySize !== size - ZIP_END_SIZE)
      fail(
        "has a central directory that does not end where the end record starts",
      );

    if (directorySize > MAX_EMBEDDED_BYTES)
      fail(`has a central directory larger than ${MAX_EMBEDDED_BYTES} bytes`);

    const directory = read(
      directoryOffset,
      directorySize,
      "the central directory",
    );
    const names = [];
    let at = 0;

    while (at < directory.length) {
      if (
        directory.length - at < ZIP_CENTRAL_SIZE ||
        directory.readUInt32LE(at) !== 0x02014b50
      )
        fail(`has a malformed central record at byte ${directoryOffset + at}`);

      const nameLength = directory.readUInt16LE(at + 28);
      const recordEnd =
        at +
        ZIP_CENTRAL_SIZE +
        nameLength +
        directory.readUInt16LE(at + 30) +
        directory.readUInt16LE(at + 32);

      if (recordEnd > directory.length)
        fail(`has a malformed central record at byte ${directoryOffset + at}`);

      const name = directory.subarray(
        at + ZIP_CENTRAL_SIZE,
        at + ZIP_CENTRAL_SIZE + nameLength,
      );
      const shown = JSON.stringify(name.toString("latin1"));

      if ((directory.readUInt16LE(at + 8) & 1) !== 0)
        fail(`has the encrypted entry ${shown}`);

      if ((directory.readUInt16LE(at + 8) & 8) !== 0)
        fail(
          `has the entry ${shown}, whose sizes and CRC-32 are in a data descriptor`,
        );

      if (
        directory.readUInt32LE(at + 20) === 0xffffffff ||
        directory.readUInt32LE(at + 24) === 0xffffffff ||
        directory.readUInt32LE(at + 42) === 0xffffffff ||
        directory.readUInt16LE(at + 34) === 0xffff
      )
        fail(
          `has the entry ${shown}, whose central record has a ZIP64 size or offset`,
        );

      if (directory.readUInt16LE(at + 34) !== 0)
        fail(
          `has the entry ${shown}, whose central record says it starts on disk ${directory.readUInt16LE(at + 34)}`,
        );

      const localOffset = directory.readUInt32LE(at + 42);
      const local = read(
        localOffset,
        ZIP_LOCAL_SIZE,
        `the local header of ${shown}`,
      );
      const localName = read(
        localOffset + ZIP_LOCAL_SIZE,
        local.readUInt16LE(26),
        `the local header of ${shown}`,
      );

      if (local.readUInt32LE(0) !== 0x04034b50 || !localName.equals(name))
        fail(
          `has the entry ${shown}, whose local header does not have that name`,
        );

      for (const [field, localAt, centralAt, width] of ZIP_SHARED_FIELDS) {
        if (
          local.readUIntLE(localAt, width) !==
          directory.readUIntLE(at + centralAt, width)
        )
          fail(
            `has the entry ${shown}, whose local header has another ${field} than its central record`,
          );
      }

      names.push(name);
      at = recordEnd;
    }

    if (names.length !== count)
      fail(
        `has ${names.length} central records, and its end record says ${count}`,
      );

    return names;
  } catch (error) {
    if (error instanceof PublishFailure) throw error;

    throw new PublishFailure(`cannot read ${file}: ${error.message}`);
  } finally {
    if (fd !== undefined) closeSync(fd);
  }
}

/**
 * Read the single root nuspec of a nupkg, and return its id, version and
 * dependencies and, as `nuspec`, the text unzip printed for it. The entry
 * names come from the zip's central directory, read in this process, and the
 * package is refused for a name that NuGet and unzip can read differently:
 * unzip ends a name at a NUL and turns a backslash into a slash in an archive
 * whose entries say they were made on MS-DOS or Windows, NuGet decodes each
 * %XX sequence, and unzip reads an argument that starts with - as an option,
 * so unzip -p prints every entry for a nuspec named -d.nuspec. With no % in
 * any name, NuGet's decoding leaves each name unchanged; NuGet matches the
 * .nuspec extension and folder names in any letter case, so two names equal in
 * any letter case are refused. Throws PublishFailure.
 */
export function readNupkg(run, file) {
  const fail = (problem) => {
    throw new PublishFailure(`${file} ${problem}`);
  };
  const names = zipEntryNames(file).map((name) => name.toString("latin1"));
  const seen = new Set();

  for (const name of names) {
    if (!/^[\x21-\x24\x26-\x5b\x5d-\x7e]+$/.test(name))
      fail(
        `has the entry name ${JSON.stringify(name)}, with a NUL, a %, a space, a backslash or a character other than printable ASCII`,
      );

    if (name.split("/").some((s) => s === "" || s === "." || s === ".."))
      fail(
        `has the entry name ${JSON.stringify(name)}, with an empty, . or .. segment`,
      );

    if (name.split("/").some((s) => s.startsWith("-")))
      fail(
        `has the entry name ${JSON.stringify(name)}, with a segment that starts with -`,
      );

    const key = name.toLowerCase();

    if (seen.has(key))
      fail(`has the entry ${JSON.stringify(name)} more than once`);

    seen.add(key);
  }

  const listing = extract(run, `the listing of ${file}`, "unzip", ["-Z1", file])
    .split(/\r?\n/)
    .filter((line) => line.length > 0);

  if (listing.length !== names.length)
    fail(
      `has ${names.length} entries in its central directory, and unzip lists ${listing.length}`,
    );

  const differing = listing.findIndex((name, index) => name !== names[index]);

  if (differing !== -1)
    fail(
      `has the entry ${JSON.stringify(names[differing])}, and unzip lists ${JSON.stringify(listing[differing])} in its place`,
    );

  const nuspecs = names.filter((name) => /^[^/]+\.nuspec$/i.test(name));

  if (nuspecs.length !== 1) {
    throw new PublishFailure(
      `${file} has ${nuspecs.length} root .nuspec files; a package has exactly one`,
    );
  }

  if (!/^[A-Za-z0-9_.-]+\.nuspec$/.test(nuspecs[0]))
    throw new PublishFailure(
      `${file} has the nuspec name ${JSON.stringify(nuspecs[0])}`,
    );

  const what = `${nuspecs[0]} in ${file}`;
  const nuspec = extract(run, what, "unzip", ["-p", file, nuspecs[0]]);

  return { ...parseNuspec(nuspec, what), nuspec };
}

const TAR_BLOCK = 512;

/** A NUL-terminated text field of a tar header. */
function tarText(header, offset, length) {
  const bytes = header.subarray(offset, offset + length);
  const end = bytes.indexOf(0);

  return bytes.toString("latin1", 0, end === -1 ? length : end);
}

/** An octal number field of a tar header, or undefined when it is not one. */
function tarNumber(header, offset, length) {
  const text = header
    .toString("latin1", offset, offset + length)
    .replace(/^ +/, "")
    .replace(/[\0 ][\s\S]*$/, "");

  return /^[0-7]{1,12}$/.test(text) ? parseInt(text, 8) : undefined;
}

/**
 * A number as tar-stream, the tar writer of pnpm pack, writes it: `digits`
 * octal digits padded with zeros, then a space. Undefined when it does not fit.
 */
function pnpmOctal(value, digits) {
  const text = value?.toString(8);

  return text === undefined || text.length > digits
    ? undefined
    : `${text.padStart(digits, "0")} `;
}

/**
 * A number as node-tar, the tar writer of npm pack, writes it in a field of
 * `width` bytes: width - 2 octal digits padded with zeros, a space and a NUL,
 * or width - 1 digits and a NUL. Undefined when it does not fit.
 */
function npmOctal(value, width) {
  const text = value?.toString(8);

  if (text === undefined || text.length > width - 1) return undefined;

  return text.length === width - 1
    ? `${text}\0`
    : `${text.padStart(width - 2, "0")} \0`;
}

/**
 * The name and prefix tar-stream writes for a path: it moves leading folders
 * into the prefix until the name fits in 100 bytes.
 */
function pnpmPathSplit(entryPath) {
  let name = entryPath;
  let prefix = "";

  while (name.length > 100) {
    const slash = name.indexOf("/");

    if (slash === -1) return undefined;

    prefix += prefix === "" ? name.slice(0, slash) : `/${name.slice(0, slash)}`;
    name = name.slice(slash + 1);
  }

  return prefix.length > 155 ? undefined : [name, prefix];
}

/**
 * The name and prefix node-tar writes for a path: the whole path when it is
 * shorter than 100 bytes, or else the split its splitPrefix finds, moving one
 * folder at a time from the prefix into the name. Undefined where node-tar
 * writes a pax header instead.
 */
function npmPathSplit(entryPath) {
  if (entryPath.length < 100) return [entryPath, ""];

  let prefix = path.posix.dirname(entryPath);
  let name = path.posix.basename(entryPath);

  do {
    if (name.length <= 100 && prefix.length <= 155) return [name, prefix];
    if (prefix.length <= 155) return undefined;

    name = path.posix.join(path.posix.basename(prefix), name);
    prefix = path.posix.dirname(prefix);
  } while (prefix !== ".");

  return undefined;
}

/**
 * The 512 bytes the writer of `form` ("pnpm" or "npm") writes for an entry,
 * or undefined when that writer cannot write it in a ustar header alone. Both
 * writers leave the link name, the user and group names and the bytes after
 * the prefix empty, and write the checksum as six octal digits, a space and a
 * NUL, and both end a directory's path with a slash. tar-stream writes the user
 * and group ids; node-tar, packing for npm in its portable mode, leaves them
 * empty.
 */
function canonicalTarHeader(form, fields) {
  if (fields.type === "5" && !fields.path.endsWith("/")) return undefined;

  const pnpm = form === "pnpm";
  const split = (pnpm ? pnpmPathSplit : npmPathSplit)(fields.path);
  // tar-stream writes mode & 0o7777, so a pnpm-form header whose mode has a
  // bit above 0o7777 differs from the header built here and is refused.
  const numbers = pnpm
    ? [
        [
          100,
          pnpmOctal(
            fields.mode === undefined ? undefined : fields.mode & 0o7777,
            6,
          ),
        ],
        [108, pnpmOctal(fields.uid, 6)],
        [116, pnpmOctal(fields.gid, 6)],
        [124, pnpmOctal(fields.size, 11)],
        [136, pnpmOctal(fields.mtime, 11)],
        [329, pnpmOctal(fields.devmajor, 6)],
        [337, pnpmOctal(fields.devminor, 6)],
      ]
    : [
        [100, npmOctal(fields.mode, 8)],
        [124, npmOctal(fields.size, 12)],
        [136, npmOctal(fields.mtime, 12)],
        [329, npmOctal(fields.devmajor, 8)],
        [337, npmOctal(fields.devminor, 8)],
      ];

  if (split === undefined || numbers.some(([, text]) => text === undefined))
    return undefined;

  const header = Buffer.alloc(TAR_BLOCK);

  header.write(split[0], 0, 100, "latin1");
  header.write(split[1], 345, 155, "latin1");
  header[156] = fields.type.charCodeAt(0);
  header.write("ustar\u000000", 257, 8, "latin1");

  for (const [offset, text] of numbers) header.write(text, offset, "latin1");

  header.write("        ", 148, 8, "latin1");

  let sum = 0;

  for (const byte of header) sum += byte;

  header.write(`${sum.toString(8).padStart(6, "0")} \0`, 148, 8, "latin1");

  return header;
}

/** The start offset and name of each field of a ustar header, in order. */
const TAR_FIELDS = [
  [0, "name"],
  [100, "mode"],
  [108, "uid"],
  [116, "gid"],
  [124, "size"],
  [136, "mtime"],
  [148, "checksum"],
  [156, "type"],
  [157, "linkname"],
  [257, "magic"],
  [263, "version"],
  [265, "uname"],
  [297, "gname"],
  [329, "devmajor"],
  [337, "devminor"],
  [345, "prefix"],
  [500, "padding"],
];

/**
 * Undefined when `header` is byte for byte the header pnpm pack or npm pack
 * writes for the fields read from it; otherwise the reason it is not, which
 * states the first field that differs from the form whose header differs in
 * the fewest bytes. The checksum field is stated only when no other field
 * differs, because any other difference also changes the checksum. A header
 * in any other form is refused, because node-tar, which npm publish unpacks
 * with, skips a header it cannot read and reads the entry's data as the next
 * header.
 */
function tarHeaderFormProblem(header, fields) {
  let closest;

  for (const form of ["pnpm", "npm"]) {
    const expected = canonicalTarHeader(form, fields);

    if (expected === undefined) continue;

    let differing = 0;
    let first;
    let checksum;

    for (let i = 0; i < TAR_BLOCK; i++) {
      if (expected[i] === header[i]) continue;

      differing++;

      if (i >= 148 && i < 156) checksum ??= i;
      else first ??= i;
    }

    if (differing === 0) return undefined;

    if (closest === undefined || differing < closest.differing)
      closest = { form, differing, first: first ?? checksum };
  }

  if (closest === undefined)
    return "neither writes this entry in a ustar header alone";

  const [start, field] = TAR_FIELDS.findLast(
    ([offset]) => offset <= closest.first,
  );

  return `its ${field} field, at byte ${closest.first} of the header (the field starts at byte ${start}), differs from the header ${closest.form} pack writes`;
}

/**
 * The entries of an unpacked tarball, each `{ path, type, data }`. npm
 * publishes a tarball by unpacking it with its first path segment removed and
 * reading package.json from the result, so an entry under another top-level
 * folder, a second entry of one path, or a link can replace the package.json
 * read here. This reader throws PublishFailure for an entry that is not a
 * regular file ("0") or a directory ("5") in a POSIX ustar header with a valid
 * checksum; for a path outside package/, with an empty, . or .. segment, a
 * space, a backslash or a character other than printable ASCII; for a header
 * that is not byte for byte the one pnpm pack or npm pack writes for the
 * values read from it; for a path listed twice in any letter case; and for any
 * byte other than zero after the end of the archive.
 */
export function tarEntries(archive, file) {
  const fail = (problem) => {
    throw new PublishFailure(`${file} ${problem}`);
  };
  const entries = [];
  const seen = new Set();
  let at = 0;

  while (at < archive.length) {
    if (archive.length - at < TAR_BLOCK) fail("ends inside an entry header");

    const header = archive.subarray(at, at + TAR_BLOCK);

    if (header.every((byte) => byte === 0)) {
      if (!archive.subarray(at).every((byte) => byte === 0))
        fail("has data after the end of the archive");

      break;
    }

    let sum = 0;

    for (let i = 0; i < TAR_BLOCK; i++)
      sum += i >= 148 && i < 156 ? 0x20 : header[i];

    if (tarNumber(header, 148, 8) !== sum)
      fail(`has an entry header with a bad checksum at byte ${at}`);

    if (header.toString("latin1", 257, 265) !== "ustar\u000000")
      fail(`has an entry at byte ${at} that is not a POSIX ustar header`);

    const name = tarText(header, 0, 100);
    const prefix = tarText(header, 345, 155);
    const entryPath = prefix.length > 0 ? `${prefix}/${name}` : name;
    const type = String.fromCharCode(header[156]);

    if (type !== "0" && type !== "5") {
      const extended = "xgLK".includes(type)
        ? ", a pax or GNU extended header, which a packer writes for a non-ASCII file name or a path component longer than 100 characters"
        : "";

      fail(
        `has the entry ${JSON.stringify(entryPath)} of entry type ${JSON.stringify(type)}${extended}; only regular files and directories are read`,
      );
    }

    const size = tarNumber(header, 124, 12);

    if (size === undefined || (type === "5" && size !== 0))
      fail(
        `has the entry ${JSON.stringify(entryPath)} with an unusable size field`,
      );

    const dataStart = at + TAR_BLOCK;

    if (dataStart + size > archive.length)
      fail(`ends inside the entry ${JSON.stringify(entryPath)}`);

    const trimmed =
      type === "5" && entryPath.endsWith("/")
        ? entryPath.slice(0, -1)
        : entryPath;
    const segments = trimmed.split("/");

    if (!/^[\x21-\x5b\x5d-\x7e]+$/.test(trimmed)) {
      fail(
        `has the entry path ${JSON.stringify(entryPath)}, with a space, a backslash or a character other than printable ASCII`,
      );
    }

    if (segments[0] !== "package" || (type === "0" && segments.length < 2))
      fail(
        `has the entry path ${JSON.stringify(entryPath)}, which is not under package/`,
      );

    if (segments.some((s) => s === "" || s === "." || s === ".."))
      fail(
        `has the entry path ${JSON.stringify(entryPath)}, with an empty, . or .. segment`,
      );

    const fields = {
      path: entryPath,
      type,
      size,
      mode: tarNumber(header, 100, 8),
      uid: tarNumber(header, 108, 8),
      gid: tarNumber(header, 116, 8),
      mtime: tarNumber(header, 136, 12),
      devmajor: tarNumber(header, 329, 8),
      devminor: tarNumber(header, 337, 8),
    };

    const formProblem = tarHeaderFormProblem(header, fields);

    if (formProblem !== undefined)
      fail(
        `has the entry ${JSON.stringify(entryPath)} at byte ${at} in a header that is not in the form pnpm pack or npm pack writes: ${formProblem}`,
      );

    const key = trimmed.toLowerCase();

    if (seen.has(key))
      fail(`has the entry ${JSON.stringify(trimmed)} more than once`);

    seen.add(key);
    entries.push({
      path: trimmed,
      type,
      data: archive.subarray(dataStart, dataStart + size),
    });
    at = dataStart + Math.ceil(size / TAR_BLOCK) * TAR_BLOCK;
  }

  return entries;
}

/**
 * Read package/package.json of a tarball, in this process, from the same bytes
 * npm publishes; `tarEntries` refuses any layout in which npm would read
 * another package.json. `maxBytes` caps both the file's size and the size of
 * the archive it unpacks to. Throws PublishFailure.
 */
export function readTarball(file, maxBytes = MAX_TARBALL_BYTES) {
  const what = `package/package.json in ${file}`;
  let compressed;

  try {
    if (statSync(file).size > maxBytes)
      throw new PublishFailure(`${file} is larger than ${maxBytes} bytes`);

    compressed = readFileSync(file);
  } catch (error) {
    if (error instanceof PublishFailure) throw error;

    throw new PublishFailure(`cannot read ${file}: ${error.message}`);
  }

  let archive;

  try {
    archive = gunzipSync(compressed, { maxOutputLength: maxBytes });
  } catch (error) {
    if (error.code === "ERR_BUFFER_TOO_LARGE")
      throw new PublishFailure(
        `${file} unpacks to more than ${maxBytes} bytes`,
      );

    throw new PublishFailure(`${file} is not a gzip file: ${error.message}`);
  }

  const entry = tarEntries(archive, file).find(
    (e) => e.path === "package/package.json" && e.type === "0",
  );

  if (entry === undefined)
    throw new PublishFailure(`${file} has no package/package.json`);

  if (entry.data.length > MAX_EMBEDDED_BYTES) {
    throw new PublishFailure(
      `${what} is larger than ${MAX_EMBEDDED_BYTES} bytes`,
    );
  }

  let manifest;

  try {
    manifest = JSON.parse(
      new TextDecoder("utf-8", { fatal: true }).decode(entry.data),
    );
  } catch (error) {
    throw new PublishFailure(`${what} is not JSON: ${error.message}`);
  }

  if (
    manifest === null ||
    typeof manifest !== "object" ||
    Array.isArray(manifest)
  )
    throw new PublishFailure(`${what} is not a JSON object`);

  const dependencies = [];

  for (const field of ["dependencies", "peerDependencies"]) {
    const deps = manifest[field];

    if (deps === null || typeof deps !== "object") continue;

    for (const [id, version] of Object.entries(deps))
      dependencies.push({ id, version, field });
  }

  return {
    id: manifest.name,
    version: manifest.version,
    repositoryUrl: manifest.repository?.url,
    dependencies,
  };
}

/**
 * `env` (this process's environment when it is undefined, as for spawnRunner)
 * without NUGET_PUSH_KEY and the variables of NPM_OIDC_ENV, in any letter case.
 */
function readingEnvironment(env) {
  const removed = new Set(["NUGET_PUSH_KEY", ...NPM_OIDC_ENV]);

  return Object.fromEntries(
    Object.entries(env ?? process.env).filter(
      ([name]) => !removed.has(name.toUpperCase()),
    ),
  );
}

/**
 * Throw PublishFailure unless a reading command exited 0; the message of a
 * non-zero exit states the exit code or signal and ends with at most 2000
 * characters from the end of `output`, or says that it printed nothing.
 */
function checkExited(result, what, maxBuffer, output) {
  if (result.error?.code === "ENOBUFS")
    throw new PublishFailure(`${what} printed more than ${maxBuffer} bytes`);

  if (result.error !== undefined)
    throw new PublishFailure(
      `${what} could not run or timed out (${result.error.message})`,
    );

  if (result.signal !== null || result.status !== 0) {
    const how = `${what} exited ${String(result.status ?? result.signal)}`;
    const printed = output.trim();

    throw new PublishFailure(
      printed.length === 0
        ? `${how} and printed nothing`
        : `${how}: ${printed.slice(-2000)}`,
    );
  }
}

/**
 * Run npm publish --dry-run on a tarball and throw PublishFailure unless it
 * exits 0 and prints the JSON npm NPM_DRY_RUN_VERIFIED_VERSION prints: an
 * object with one key, the package name, whose value is an object with that
 * name and the version, which must equal the entry's. That npm prints an
 * object without the key only for a package with no name (logTar in its
 * lib/utils/tar.js, called from lib/commands/publish.js). npm reads the name
 * and version with the code a real npm publish runs: it unpacks the file with
 * node-tar and reads the package.json that results, so a tarball that node-tar
 * reads differently from tarEntries fails here.
 */
export function checkNpmReading(deps, entry, file) {
  const what = `npm publish --dry-run of ${file}`;
  const result = deps.run("npm", npmDryRunArgs(file), {
    timeoutMs: DRY_RUN_TIMEOUT_MS,
    maxBuffer: MAX_DRY_RUN_OUTPUT_BYTES,
    env: readingEnvironment(deps.env),
  });

  checkExited(result, what, MAX_DRY_RUN_OUTPUT_BYTES, result.stderr);

  let reading;

  try {
    reading = JSON.parse(result.stdout);
  } catch (error) {
    throw new PublishFailure(`${what} did not print JSON: ${error.message}`);
  }

  const keys =
    reading !== null && typeof reading === "object" && !Array.isArray(reading)
      ? Object.keys(reading)
      : [];
  const value = keys.length === 1 ? reading[keys[0]] : undefined;

  if (
    value === null ||
    typeof value !== "object" ||
    Array.isArray(value) ||
    typeof value.name !== "string" ||
    typeof value.version !== "string"
  )
    throw new PublishFailure(
      `${what} did not print a JSON object with one key whose value has a name and a version, the form npm ${NPM_DRY_RUN_VERIFIED_VERSION} prints`,
    );

  if (keys[0] !== value.name)
    throw new PublishFailure(
      `${what} printed the key ${JSON.stringify(keys[0])} for the name ${JSON.stringify(value.name)}`,
    );

  if (value.name !== entry.id || value.version !== entry.version)
    throw new PublishFailure(
      `npm reads ${JSON.stringify(value.name)} ${JSON.stringify(value.version)} from ${file}, and the manifest entry is ${entry.id} ${entry.version}`,
    );
}

/** The arguments of the dotnet nuget push that reads a nupkg's id and version into a local folder. */
export function nugetReadingArgs(file, folder, configFile) {
  return [
    "nuget",
    "push",
    file,
    "--source",
    folder,
    "--configfile",
    configFile,
    "--no-symbols",
    "--force-english-output",
  ];
}

/** `text` with A to Z in lower case and every other character unchanged. */
function asciiLowerCase(text) {
  return text.replace(/[A-Z]/g, (letter) => letter.toLowerCase());
}

/**
 * Push a nupkg with dotnet nuget push to a new local folder, and throw
 * PublishFailure unless NuGet exits 0, writes the package to the folder
 * {id}/{version}/, whose names are printable ASCII and equal the entry's id
 * and version with A to Z in lower case, and writes {id}.nuspec beside it,
 * whose bytes equal `nuspec` (the text readNupkg parsed) in UTF-8. NuGet reads
 * the id and version with PackageArchiveReader, the package reader of
 * NuGet.Packaging, and copies the nuspec entry's bytes unchanged
 * (PackageExtractor.InstallFromSourceAsync, with PackageSaveMode.Defaultv3), so
 * a nupkg whose nuspec NuGet reads differently from readNupkg fails here. A
 * nuspec that is not valid UTF-8 fails, because the text decoded from it no
 * longer has its bytes. The push gets an empty NuGet.Config, which lists no
 * source, and an environment without NUGET_PUSH_KEY or an npm OIDC variable,
 * and runs with .NET telemetry, the workload update check and online
 * certificate revocation checks off, so it sends no request; NuGet sends none
 * for a push to a folder. NuGet verifies a package signature during the push,
 * and a signed nupkg is not supported. The folder is deleted afterwards with
 * `deps.rm` (rmSync when it is undefined), and a failure to delete it fails
 * the check; after an earlier failure, the message states both.
 */
export function checkNugetReading(deps, entry, file, nuspec) {
  const what = `dotnet nuget push of ${file} to a local folder`;
  let work;
  let failure;

  try {
    work = mkdtempSync(path.join(tmpdir(), "release-publish-nuget-"));

    const feed = path.join(work, "feed");
    const seed = path.join(feed, NUGET_LAYOUT_SEED, "0.0.0");
    const configFile = path.join(work, "NuGet.Config");

    mkdirSync(seed, { recursive: true });
    writeFileSync(path.join(seed, `${NUGET_LAYOUT_SEED}.nuspec`), "");
    writeFileSync(configFile, "<configuration />\n");

    const result = deps.run(
      "dotnet",
      nugetReadingArgs(file, feed, configFile),
      {
        timeoutMs: NUGET_READING_TIMEOUT_MS,
        maxBuffer: MAX_NUGET_READING_OUTPUT_BYTES,
        env: {
          ...readingEnvironment(deps.env),
          DOTNET_CLI_TELEMETRY_OPTOUT: "1",
          DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: "1",
          DOTNET_NOLOGO: "1",
          NUGET_CERT_REVOCATION_MODE: "offline",
        },
      },
    );

    // dotnet nuget push prints its errors to standard output.
    checkExited(
      result,
      what,
      MAX_NUGET_READING_OUTPUT_BYTES,
      `${result.stdout}\n${result.stderr}`,
    );

    const written = readdirSync(feed).filter(
      (name) => name !== NUGET_LAYOUT_SEED,
    );
    const versions =
      written.length === 1 &&
      statSync(path.join(feed, written[0])).isDirectory()
        ? readdirSync(path.join(feed, written[0]))
        : [];
    // .NET and JavaScript turn some non-ASCII letters into ASCII ones in
    // lower case (the Kelvin sign into k), so only printable ASCII names are
    // compared.
    const nonAscii = [...written, ...versions].find(
      (name) => !/^[\x21-\x7e]+$/.test(name),
    );

    if (nonAscii !== undefined)
      throw new PublishFailure(
        `${what} wrote the folder name ${JSON.stringify(nonAscii)}, which is not printable ASCII`,
      );

    if (versions.length !== 1)
      throw new PublishFailure(
        `${what} wrote ${JSON.stringify(written)} with ${JSON.stringify(versions)} inside, not one {id}/{version}/ folder`,
      );

    const [id] = written;
    const [version] = versions;

    if (
      id !== asciiLowerCase(entry.id) ||
      version !== asciiLowerCase(entry.version)
    )
      throw new PublishFailure(
        `NuGet reads ${JSON.stringify(id)} ${JSON.stringify(version)} from ${file}, and the manifest entry is ${entry.id} ${entry.version}`,
      );

    if (
      !statSync(path.join(feed, id, version, `${id}.${version}.nupkg`), {
        throwIfNoEntry: false,
      })?.isFile()
    )
      throw new PublishFailure(
        `${what} wrote no ${id}.${version}.nupkg in ${id}/${version}/`,
      );

    const copy = path.join(feed, id, version, `${id}.nuspec`);
    const copyStat = lstatSync(copy, { throwIfNoEntry: false });

    if (!copyStat?.isFile())
      throw new PublishFailure(
        `${what} wrote no ${id}.nuspec in ${id}/${version}/`,
      );

    if (copyStat.size > MAX_EMBEDDED_BYTES)
      throw new PublishFailure(
        `NuGet's copy of the nuspec of ${file} is larger than ${MAX_EMBEDDED_BYTES} bytes`,
      );

    if (!readFileSync(copy).equals(Buffer.from(nuspec, "utf-8")))
      throw new PublishFailure(
        `NuGet's copy of the nuspec of ${file} differs from the nuspec read with unzip`,
      );
  } catch (error) {
    failure =
      error instanceof PublishFailure
        ? error
        : new PublishFailure(`${what} failed: ${error.message}`);
  }

  if (work !== undefined) {
    try {
      (deps.rm ?? rmSync)(work, {
        recursive: true,
        force: true,
        maxRetries: 3,
      });
    } catch (error) {
      const notDeleted = `cannot delete the folder ${work} after ${what}: ${error.message}`;

      failure = new PublishFailure(
        failure === undefined
          ? notDeleted
          : `${failure.message}\n  ${notDeleted}`,
      );
    }
  }

  if (failure !== undefined) throw failure;
}

/** Throw PublishFailure when the identity inside a file differs from its entry. */
export function checkIdentity(entry, identity, file) {
  if (identity.id !== entry.id || identity.version !== entry.version) {
    throw new PublishFailure(
      `${file} holds ${JSON.stringify(identity.id)} ${JSON.stringify(identity.version)}, and the manifest entry is ${entry.id} ${entry.version}`,
    );
  }

  if (entry.ecosystem === "npm" && identity.repositoryUrl !== REPOSITORY_URL) {
    throw new PublishFailure(
      `${file} has repository.url ${JSON.stringify(identity.repositoryUrl)}, not ${REPOSITORY_URL}`,
    );
  }
}

/** True when a dependency id is one of the packages this repository publishes. */
export function isOurs(ecosystem, id) {
  return (
    typeof id === "string" &&
    idEcosystem(id) === ecosystem &&
    idKey(ecosystem, id).startsWith(OUR_PREFIX[ecosystem])
  );
}

/**
 * The exact version of a pin, or undefined when it is not one: a nuspec
 * version must be written [x.y.z] or [x.y.z-pre], an npm version x.y.z.
 */
export function exactPin(ecosystem, version) {
  if (typeof version !== "string") return undefined;

  if (ecosystem === "nuget") {
    const match = /^\[([^,\]\s]+)\]$/.exec(version);

    return match !== null && isSemver(match[1]) ? match[1] : undefined;
  }

  return isSemver(version) ? version : undefined;
}

/**
 * The seconds a Retry-After value asks for, or undefined when the value is not
 * a whole number of seconds or an HTTP date at most MAX_RETRY_AFTER_SECONDS away.
 */
export function retryAfterSeconds(value, nowMs) {
  if (typeof value !== "string") return undefined;

  const text = value.trim();

  if (/^\d+$/.test(text)) {
    const seconds = Number(text);

    return seconds <= MAX_RETRY_AFTER_SECONDS ? seconds : undefined;
  }

  if (
    !/^[A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(
      text,
    )
  )
    return undefined;

  const at = Date.parse(text);

  if (Number.isNaN(at)) return undefined;

  const seconds = Math.max(0, Math.ceil((at - nowMs) / 1000));

  return seconds <= MAX_RETRY_AFTER_SECONDS ? seconds : undefined;
}

/** The registry URL of an id's version list. */
export function registryUrl(ecosystem, id) {
  return ecosystem === "nuget"
    ? `${NUGET_FLAT_CONTAINER}${id.toLowerCase()}/index.json`
    : `${NPM_REGISTRY}${id.replace("/", "%2F")}`;
}

/**
 * The versions a registry lists for an id, lower case for NuGet; an empty list
 * when the registry returns 404. HTTP 429 and 5xx are retried after the backoff
 * or a usable Retry-After. Throws PublishFailure for anything else.
 */
export async function readRegistry(deps, ecosystem, id) {
  const url = registryUrl(ecosystem, id);
  const headers =
    ecosystem === "npm"
      ? { accept: "application/vnd.npm.install-v1+json" }
      : {};

  for (let attempt = 0; ; attempt++) {
    let response;

    try {
      response = await deps.fetch(url, {
        headers,
        signal: AbortSignal.timeout(READ_TIMEOUT_MS),
      });
    } catch (error) {
      throw new PublishFailure(`reading ${url} failed: ${error.message}`);
    }

    if (response.status === 404) return [];

    if (response.status === 429 || response.status >= 500) {
      if (attempt >= BACKOFF_SECONDS.length) {
        throw new PublishFailure(
          `reading ${url} returned HTTP ${response.status} after ${BACKOFF_SECONDS.length} retries`,
        );
      }

      const wait =
        retryAfterSeconds(response.headers.get("retry-after"), deps.now()) ??
        BACKOFF_SECONDS[attempt];

      deps.log(`HTTP ${response.status} from ${url}; retrying in ${wait} s`);
      await deps.sleep(wait * 1000);
      continue;
    }

    if (response.status !== 200)
      throw new PublishFailure(
        `reading ${url} returned HTTP ${response.status}`,
      );

    let body;

    try {
      body = JSON.parse(await response.text());
    } catch (error) {
      throw new PublishFailure(`${url} did not return JSON: ${error.message}`);
    }

    if (ecosystem === "nuget") {
      if (
        !Array.isArray(body?.versions) ||
        body.versions.some((v) => typeof v !== "string")
      )
        throw new PublishFailure(`${url} has no versions list`);

      return body.versions.map((v) => v.toLowerCase());
    }

    if (
      body?.versions === null ||
      typeof body?.versions !== "object" ||
      Array.isArray(body.versions)
    )
      throw new PublishFailure(`${url} has no versions list`);

    return Object.keys(body.versions);
  }
}

/** The registry version lists read so far in this run, one read per id. */
function registryCache(deps) {
  const cache = new Map();

  return {
    async versions(ecosystem, id) {
      const key = `${ecosystem}:${idKey(ecosystem, id)}`;

      if (!cache.has(key))
        cache.set(key, await readRegistry(deps, ecosystem, id));

      return cache.get(key);
    },
    async reread(ecosystem, id) {
      const key = `${ecosystem}:${idKey(ecosystem, id)}`;

      cache.set(key, await readRegistry(deps, ecosystem, id));

      return cache.get(key);
    },
  };
}

function lists(versions, ecosystem, version) {
  return versions.includes(
    ecosystem === "nuget" ? version.toLowerCase() : version,
  );
}

/** Throw PublishFailure for a pin on one of our packages that no registry or earlier entry satisfies. */
async function checkPins(entry, identity, earlier, registry) {
  const checked = new Set();

  for (const dep of identity.dependencies) {
    if (!isOurs(entry.ecosystem, dep.id)) continue;

    const version = exactPin(entry.ecosystem, dep.version);

    if (version === undefined) {
      throw new PublishFailure(
        `${entry.id} ${entry.version} depends on ${dep.id} at ${JSON.stringify(dep.version)}, which is not an exact version`,
      );
    }

    const key = `${idKey(entry.ecosystem, dep.id)}@${idKey(entry.ecosystem, version)}`;

    if (checked.has(key)) continue;

    checked.add(key);

    const inManifest = earlier.some(
      (e) =>
        e.ecosystem === entry.ecosystem &&
        idKey(e.ecosystem, e.id) === idKey(entry.ecosystem, dep.id) &&
        idKey(e.ecosystem, e.version) === idKey(entry.ecosystem, version),
    );

    if (inManifest) continue;

    if (
      !lists(
        await registry.versions(entry.ecosystem, dep.id),
        entry.ecosystem,
        version,
      )
    ) {
      throw new PublishFailure(
        `${entry.id} ${entry.version} depends on ${dep.id} ${version}, which the registry does not list and no earlier entry of this release publishes; an earlier release has not finished publishing, so its run must finish first (or the registry has not listed a version published moments ago, and a re-run passes)`,
      );
    }
  }
}

/** The first-publish procedure, printed when a never-published id is refused. */
export function firstPublishProcedure(entry, fileName) {
  const command =
    entry.ecosystem === "npm"
      ? `npm login, then npm publish ${fileName} --access public`
      : `dotnet nuget push ${fileName} --source ${NUGET_SOURCE} --api-key <a short-lived key limited to ${entry.id}>`;
  const lines = [
    `First publish of ${entry.id}: the registry has never published it, and it refused this run's trusted publishing credentials.`,
    `  1. Download this run's artifact whose name starts with packages- and take ${fileName} from it.`,
    `  2. Publish that one file with your own credentials: ${command}.`,
  ];

  if (entry.ecosystem === "npm") {
    lines.push(
      `  3. On npmjs.com, open ${entry.id} > Settings > Trusted Publisher and add GitHub Actions: organization DCSV-io, repository D2-Public, workflow release-libs.yml, environment publish.`,
    );
  }

  lines.push(
    `  ${entry.ecosystem === "npm" ? 4 : 3}. Re-run the failed jobs of this run. The re-run skips every version the registry lists.`,
  );

  return lines.join("\n");
}

/** Replace every occurrence of `secret` in `text`. */
function redact(text, secret) {
  return secret === undefined || secret.length === 0
    ? text
    : text.split(secret).join("***");
}

/** Push one file, retrying a rate limit; returns "published" or "skipped". Throws PublishFailure. */
async function push(deps, registry, entry, file, neverPublished) {
  const key = deps.env.NUGET_PUSH_KEY;

  if (entry.ecosystem === "nuget" && (key === undefined || key.length === 0))
    throw new PublishFailure("NUGET_PUSH_KEY is not set");

  const [program, args] =
    entry.ecosystem === "nuget"
      ? [
          "dotnet",
          [
            "nuget",
            "push",
            file,
            "--source",
            NUGET_SOURCE,
            "--api-key",
            key,
            "--skip-duplicate",
            "--no-symbols",
          ],
        ]
      : ["npm", ["publish", file, "--access", "public", "--ignore-scripts"]];

  for (let attempt = 0; ; attempt++) {
    const result = deps.run(program, args, {
      timeoutMs: PUSH_TIMEOUT_MS,
      maxBuffer: MAX_PUSH_OUTPUT_BYTES,
    });
    const output = redact(`${result.stdout}\n${result.stderr}`.trim(), key);
    const exited = result.error === undefined && result.signal === null;

    if (exited && ALREADY_EXISTS[entry.ecosystem].test(output)) {
      if (
        lists(
          await registry.reread(entry.ecosystem, entry.id),
          entry.ecosystem,
          entry.version,
        )
      )
        return "skipped";

      throw new PublishFailure(
        `${entry.id} ${entry.version}: the push says the version exists, and the registry does not list it:\n${output}`,
      );
    }

    if (exited && result.status === 0) return "published";

    if (exited && RATE_LIMITED[entry.ecosystem].test(output)) {
      if (attempt >= BACKOFF_SECONDS.length) {
        throw new PublishFailure(
          `${entry.id} ${entry.version}: still rate limited after ${BACKOFF_SECONDS.length} retries:\n${output}`,
        );
      }

      deps.log(
        `${entry.id} ${entry.version}: rate limited; retrying in ${BACKOFF_SECONDS[attempt]} s`,
      );
      await deps.sleep(BACKOFF_SECONDS[attempt] * 1000);
      continue;
    }

    const how =
      result.error !== undefined
        ? `could not run or timed out (${redact(result.error.message, key)})`
        : result.signal !== null
          ? `ended by signal ${result.signal}`
          : `exited ${result.status}`;
    const procedure =
      neverPublished && exited && AUTHORIZATION_REJECTED.test(output)
        ? `\n${firstPublishProcedure(entry, path.basename(file))}`
        : "";

    throw new PublishFailure(
      `${entry.id} ${entry.version}: ${program} ${args[0]} ${how}:\n${output}${procedure}`,
    );
  }
}

/** The `publish` command. Returns the per-entry outcomes; throws UsageError or PublishFailure. */
export async function publish(options, deps) {
  const files = check(options);
  const registry = registryCache(deps);
  const outcomes = [];

  // The identity and pin checks run over every entry before the first push,
  // so a bad file anywhere in the manifest publishes nothing.
  const identities = files.map(({ entry, file }) => {
    const identity =
      entry.ecosystem === "nuget"
        ? readNupkg(deps.run, file)
        : readTarball(file);

    if (entry.ecosystem === "npm") checkNpmReading(deps, entry, file);

    checkIdentity(entry, identity, file);

    // NuGet unpacks the package into the local folder, so it reads only a
    // package whose entry names and nuspec passed the checks above.
    if (entry.ecosystem === "nuget")
      checkNugetReading(deps, entry, file, identity.nuspec);

    return identity;
  });

  for (const [index, { entry }] of files.entries()) {
    await checkPins(
      entry,
      identities[index],
      files.slice(0, index).map((f) => f.entry),
      registry,
    );
  }

  for (const { entry, file } of files) {
    const versions = await registry.versions(entry.ecosystem, entry.id);

    if (lists(versions, entry.ecosystem, entry.version)) {
      deps.log(`${entry.id} ${entry.version}: skipped, the registry lists it`);
      outcomes.push({
        id: entry.id,
        version: entry.version,
        outcome: "skipped",
      });
      continue;
    }

    const outcome = await push(
      deps,
      registry,
      entry,
      file,
      versions.length === 0,
    );

    deps.log(
      outcome === "published"
        ? `${entry.id} ${entry.version}: published`
        : `${entry.id} ${entry.version}: skipped, the registry lists it after the push`,
    );
    outcomes.push({ id: entry.id, version: entry.version, outcome });
  }

  return outcomes;
}

/** The registry page of a package version. */
export function registryLink(entry) {
  return entry.ecosystem === "nuget"
    ? `https://www.nuget.org/packages/${entry.id}/${entry.version}`
    : `https://www.npmjs.com/package/${entry.id}/v/${entry.version}`;
}

/** The `notes` command: the Release body. */
export function notes(options) {
  if (typeof options.tag !== "string" || !TAG_RE.test(options.tag))
    throw new UsageError(
      `--tag ${JSON.stringify(options.tag)} is not libs/<YYYYMMDD>T<HHMMSS>Z`,
    );

  const packages = readManifest(options.manifest);

  return [
    `Release ${options.tag}`,
    "",
    ...packages.map(
      (entry) => `- ${entry.id} ${entry.version}: ${registryLink(entry)}`,
    ),
    "",
  ].join("\n");
}

/** The `ecosystems` command: append nuget=true|false to the GITHUB_OUTPUT file. */
export function ecosystems(options, env) {
  const packages = readManifest(options.manifest);
  const output = env.GITHUB_OUTPUT;

  if (typeof output !== "string" || output.length === 0)
    throw new UsageError("GITHUB_OUTPUT is not set");

  const nuget = packages.some((entry) => entry.ecosystem === "nuget");

  appendFileSync(output, `nuget=${nuget}\n`);

  return nuget;
}

const COMMAND_OPTIONS = {
  check: ["manifest", "nuget", "npm"],
  publish: ["manifest", "nuget", "npm"],
  notes: ["manifest", "tag"],
  ecosystems: ["manifest"],
};

/** Parse `<command> --name value ...`. Throws UsageError. */
export function parseArgs(argv) {
  const [command, ...rest] = argv;
  const names = COMMAND_OPTIONS[command];

  if (names === undefined)
    throw new UsageError(
      `the command must be one of ${Object.keys(COMMAND_OPTIONS).join(", ")}`,
    );

  const options = {};

  for (let i = 0; i < rest.length; i += 2) {
    const name = rest[i].startsWith("--") ? rest[i].slice(2) : undefined;
    const value = rest[i + 1];

    if (name === undefined || !names.includes(name))
      throw new UsageError(`${command} does not take ${rest[i]}`);

    if (value === undefined || value.startsWith("--"))
      throw new UsageError(`${rest[i]} needs a value`);

    if (name in options) throw new UsageError(`${rest[i]} is given twice`);

    options[name] = value;
  }

  for (const name of names) {
    if (!(name in options)) throw new UsageError(`${command} needs --${name}`);
  }

  return { command, options };
}

/** The CLI. Returns the exit code. */
export async function main(argv, deps) {
  try {
    const { command, options } = parseArgs(argv);

    switch (command) {
      case "check":
        for (const { entry, file } of check(options))
          deps.log(`${entry.id} ${entry.version}: ${file}`);
        break;
      case "publish":
        await publish(options, deps);
        break;
      case "notes":
        deps.out(notes(options));
        break;
      case "ecosystems":
        deps.log(`nuget=${ecosystems(options, deps.env)}`);
        break;
    }

    return 0;
  } catch (error) {
    if (error instanceof UsageError || error instanceof PublishFailure) {
      // The message can hold text from a package file or a push. The GitHub
      // Actions runner runs no workflow command between these lines, and the
      // token is generated after that text exists.
      const token = randomUUID();

      deps.error(
        `::stop-commands::${token}\nrelease-publish: ${error.message}\n::${token}::`,
      );

      return error instanceof UsageError ? 2 : 1;
    }

    throw error;
  }
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  process.exitCode = await main(process.argv.slice(2), {
    fetch: globalThis.fetch,
    run: spawnRunner,
    sleep: (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
    now: () => Date.now(),
    env: process.env,
    log: (text) => process.stdout.write(`${text}\n`),
    out: (text) => process.stdout.write(text),
    error: (text) => process.stderr.write(`${text}\n`),
  });
}
