// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// TS -> .NET cross-runtime fixture emitter (test and fixture only, `.fixture.ts`).
// Produces sealed (v2) frames with @dcsv-io/d2-encryption's PayloadSealer so the .NET
// TsCryptoInterop suite can open them with the real PayloadOpener, which shows that the
// TS encoder is byte-compatible with the .NET decoder. Regenerate via
// `pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures`.
//
// The emitted frames are non-deterministic by design: this script creates a fresh
// recipient keypair, and the sealer creates a fresh per-message ephemeral keypair
// and nonce (it has no injection point), so every emit produces a different frameHex.
// That is expected. The check is functional: the .NET suite opens the frames
// and compares plaintext. The deterministic `sealed-crypto-kat` known-answer
// fixture, not this manifest, provides the byte-exact gate.

import { webcrypto } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { PayloadSealer } from "../src/payload-sealer.js";
import { RecipientPublicKeyring } from "../src/recipient-public-keyring.js";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(SCRIPT_DIR, "..", "..", "..", "..", "..");
const OUT_DIR = join(
  REPO_ROOT,
  "public/packages/dotnet/tests/Integration/Encryption/TsCryptoInterop/fixtures",
);
const OUT_FILE = "sealed-frames.manifest.fixture.json";

const RECIPIENT_SERVICE_ID = "orders";
const RECIPIENT_KID = "ts-seal-kid";
const PLAINTEXTS = [
  "ts-sealed-known-answer",
  "a second sealed payload \u2014 longer, with punctuation & digits 0123456789",
];

async function main(): Promise<void> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const publicSpki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const privatePkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );

  const keyring = await RecipientPublicKeyring.create(
    RECIPIENT_SERVICE_ID,
    RECIPIENT_KID,
    new Map([[RECIPIENT_KID, publicSpki]]),
  );
  const sealer = new PayloadSealer(keyring);

  const frames: { plaintextUtf8: string; frameHex: string }[] = [];
  for (const plaintextUtf8 of PLAINTEXTS) {
    const framed = await sealer.seal(new TextEncoder().encode(plaintextUtf8));
    frames.push({
      plaintextUtf8,
      frameHex: Buffer.from(framed).toString("hex").toUpperCase(),
    });
  }

  const manifest = {
    $comment:
      "Synthetic fixture-only key material; no real PII. TS-produced sealed " +
      "frames opened by the .NET TsCryptoInterop suite.",
    emittedBy:
      "public/packages/typescript/encryption/scripts/emit-sealed-frames.fixture.ts",
    regenerate: "pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures",
    recipientServiceId: RECIPIENT_SERVICE_ID,
    recipientKid: RECIPIENT_KID,
    recipientPrivatePkcs8Base64: Buffer.from(privatePkcs8).toString("base64"),
    frames,
  };

  // Every character outside ASCII is written as a JSON \uXXXX escape, so the
  // manifest is ASCII and JSON.parse returns the same values.
  const json = JSON.stringify(manifest, null, 2).replace(
    /[^\x00-\x7f]/g,
    (c) => "\\u" + c.charCodeAt(0).toString(16).padStart(4, "0"),
  );

  mkdirSync(OUT_DIR, { recursive: true });
  writeFileSync(join(OUT_DIR, OUT_FILE), json + "\n");
  console.log(`  ${OUT_FILE} (${frames.length} sealed frames)`);
}

await main();
