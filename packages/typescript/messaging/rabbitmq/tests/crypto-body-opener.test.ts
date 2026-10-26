// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  PayloadCrypto,
  PayloadCryptoKeyring,
  PayloadOpener,
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import { DlqFailureCauses } from "@dcsv-io/d2-messaging-abstractions";
import { describe, expect, it } from "vitest";

import { PlaintextBodyOpener } from "../src/subscribing/body-opener.js";
import {
  CryptoBodyOpener,
  assertOpenerMatchesDomain,
} from "../src/subscribing/crypto-body-opener.js";
import { MessageBodyDecodeError } from "../src/subscribing/message-body-decode-error.js";

const utf8 = new TextEncoder();

async function sealerOpener(): Promise<{
  sealer: PayloadSealer;
  opener: PayloadOpener;
}> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const spki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const pkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );

  return {
    sealer: new PayloadSealer(
      await RecipientPublicKeyring.create(
        "payload-fixture-sealed",
        "seal-1",
        new Map([["seal-1", spki]]),
      ),
    ),
    opener: new PayloadOpener(
      await RecipientPrivateKeyring.create(
        "payload-fixture-sealed",
        new Map([["seal-1", pkcs8]]),
      ),
    ),
  };
}

function symmetric(): PayloadCrypto {
  return new PayloadCrypto(
    new PayloadCryptoKeyring(
      "sym-1",
      new Map([["sym-1", new Uint8Array(32).fill(9)]]),
      utf8.encode("d2/orders"),
    ),
  );
}

async function decodeError(
  p: Promise<unknown>,
): Promise<MessageBodyDecodeError> {
  const err = await p.then(() => undefined).catch((e: unknown) => e);
  expect(err).toBeInstanceOf(MessageBodyDecodeError);

  return err as MessageBodyDecodeError;
}

describe("CryptoBodyOpener", () => {
  it("opens a sealed body round-trip and exposes its domain", async () => {
    const { sealer, opener } = await sealerOpener();
    const bodyOpener = CryptoBodyOpener.sealed(
      "payload-fixture-sealed",
      opener,
    );
    const frame = await sealer.seal(utf8.encode(JSON.stringify({ x: 1 })));

    expect(bodyOpener.domain).toBe("payload-fixture-sealed");
    expect(await bodyOpener.open(Buffer.from(frame))).toEqual({ x: 1 });
  });

  it("opens a symmetric body round-trip", async () => {
    const crypto = symmetric();
    const bodyOpener = CryptoBodyOpener.symmetric(
      "payload-fixture-sealed",
      crypto,
    );
    const frame = await crypto.encrypt(utf8.encode(JSON.stringify({ y: 2 })));

    expect(await bodyOpener.open(Buffer.from(frame))).toEqual({ y: 2 });
  });

  it("DLQs a plaintext body on a sealed opener (DECRYPT_FAILURE)", async () => {
    const { opener } = await sealerOpener();
    const bodyOpener = CryptoBodyOpener.sealed(
      "payload-fixture-sealed",
      opener,
    );
    const err = await decodeError(
      bodyOpener.open(Buffer.from(utf8.encode('{"x":1}'))),
    );
    expect(err.decodeCause).toBe(DlqFailureCauses.DECRYPT_FAILURE);
  });

  it("DLQs a wrong-version frame on a sealed opener (DECRYPT_FAILURE)", async () => {
    const { opener } = await sealerOpener();
    const v1 = await symmetric().encrypt(utf8.encode("x"));
    const err = await decodeError(
      CryptoBodyOpener.sealed("payload-fixture-sealed", opener).open(
        Buffer.from(v1),
      ),
    );
    expect(err.decodeCause).toBe(DlqFailureCauses.DECRYPT_FAILURE);
  });

  it("DLQs a tampered sealed frame (DECRYPT_FAILURE)", async () => {
    const { sealer, opener } = await sealerOpener();
    const frame = await sealer.seal(utf8.encode(JSON.stringify({ x: 1 })));
    const last = frame.length - 1;
    frame[last] = frame[last]! ^ 0xff;
    const err = await decodeError(
      CryptoBodyOpener.sealed("payload-fixture-sealed", opener).open(
        Buffer.from(frame),
      ),
    );
    expect(err.decodeCause).toBe(DlqFailureCauses.DECRYPT_FAILURE);
  });

  it("DLQs a decrypted-but-non-JSON body (DESERIALIZE_FAILURE)", async () => {
    const { sealer, opener } = await sealerOpener();
    const frame = await sealer.seal(utf8.encode("this is not json"));
    const err = await decodeError(
      CryptoBodyOpener.sealed("payload-fixture-sealed", opener).open(
        Buffer.from(frame),
      ),
    );
    expect(err.decodeCause).toBe(DlqFailureCauses.DESERIALIZE_FAILURE);
  });
});

describe("assertOpenerMatchesDomain (subscriber-vs-opener cross-check)", () => {
  it("passes when a sealed domain has its matching sealed opener", async () => {
    const { opener } = await sealerOpener();
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        CryptoBodyOpener.sealed("payload-fixture-sealed", opener),
      ),
    ).not.toThrow();
  });

  it("throws when a sealed domain is wired with a plaintext opener", () => {
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        new PlaintextBodyOpener(),
      ),
    ).toThrow(/requires a sealed CryptoBodyOpener/);
  });

  it("states the managed sealed opener in the sealed-domain error", () => {
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        new PlaintextBodyOpener(),
      ),
    ).toThrow(
      "Sealed domain 'payload-fixture-sealed' requires a sealed CryptoBodyOpener " +
        "for that domain, but none was wired - every delivery would DLQ. Wire the " +
        "managed sealed opener for this consumer.",
    );
  });

  it("throws when a sealed opener is wired for the wrong domain", async () => {
    const { opener } = await sealerOpener();
    // The public catalog has a single sealed domain (payload-fixture-sealed).
    // A mismatch is a sealed catalog domain with an opener tagged with a
    // different label.
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        CryptoBodyOpener.sealed("other-sealed-label", opener),
      ),
    ).toThrow(/requires a sealed CryptoBodyOpener/);
  });

  it("throws when a sealed domain is wired with a symmetric opener for that domain", () => {
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        CryptoBodyOpener.symmetric("payload-fixture-sealed", symmetric()),
      ),
    ).toThrow(/requires a sealed CryptoBodyOpener/);
  });

  it("enforces a registered sealed application domain, and refuses it when its mode is not passed", async () => {
    const { opener } = await sealerOpener();
    const modes = { "app-orders-fixture": "sealed" } as const;

    expect(() =>
      assertOpenerMatchesDomain(
        "app-orders-fixture",
        new PlaintextBodyOpener(),
        modes,
      ),
    ).toThrow(
      "Sealed domain 'app-orders-fixture' requires a sealed CryptoBodyOpener",
    );
    expect(() =>
      assertOpenerMatchesDomain(
        "app-orders-fixture",
        CryptoBodyOpener.sealed("app-orders-fixture", opener),
        modes,
      ),
    ).not.toThrow();
    expect(() =>
      assertOpenerMatchesDomain(
        "app-orders-fixture",
        new PlaintextBodyOpener(),
      ),
    ).toThrow("Encryption domain 'app-orders-fixture' has no mode");
  });

  it("throws when a registered symmetric domain is wired with a sealed, foreign or plaintext opener", async () => {
    const { opener } = await sealerOpener();

    expect(() =>
      assertOpenerMatchesDomain(
        "app-ledger-fixture",
        CryptoBodyOpener.sealed("app-ledger-fixture", opener),
        { "app-ledger-fixture": "symmetric" },
      ),
    ).toThrow(
      "Symmetric domain 'app-ledger-fixture' is wired with a sealed CryptoBodyOpener",
    );
    expect(() =>
      assertOpenerMatchesDomain(
        "app-ledger-fixture",
        CryptoBodyOpener.symmetric("other-label", symmetric()),
        { "app-ledger-fixture": "symmetric" },
      ),
    ).toThrow("CryptoBodyOpener for domain 'other-label'");
    expect(() =>
      assertOpenerMatchesDomain(
        "app-ledger-fixture",
        new PlaintextBodyOpener(),
        { "app-ledger-fixture": "symmetric" },
      ),
    ).toThrow(
      "Symmetric domain 'app-ledger-fixture' requires a symmetric CryptoBodyOpener",
    );
    expect(() =>
      assertOpenerMatchesDomain(
        "app-ledger-fixture",
        CryptoBodyOpener.symmetric("app-ledger-fixture", symmetric()),
        { "app-ledger-fixture": "symmetric" },
      ),
    ).not.toThrow();
  });

  it("refuses a registration that re-maps a catalog domain to another mode", () => {
    expect(() =>
      assertOpenerMatchesDomain(
        "payload-fixture-sealed",
        new PlaintextBodyOpener(),
        { "payload-fixture-sealed": "symmetric" },
      ),
    ).toThrow(
      "Encryption domain 'payload-fixture-sealed' was already registered as sealed; cannot re-register as symmetric.",
    );
  });

  it("DLQs a symmetric frame on a registered sealed domain's opener (DECRYPT_FAILURE)", async () => {
    const { opener } = await sealerOpener();
    const v1 = await symmetric().encrypt(utf8.encode('{"x":1}'));

    const err = await decodeError(
      CryptoBodyOpener.sealed("app-orders-fixture", opener).open(
        Buffer.from(v1),
      ),
    );

    expect(err.decodeCause).toBe(DlqFailureCauses.DECRYPT_FAILURE);
  });

  it("exposes the mode each factory binds", async () => {
    const { opener } = await sealerOpener();

    expect(CryptoBodyOpener.sealed("d", opener).mode).toBe("sealed");
    expect(CryptoBodyOpener.symmetric("d", symmetric()).mode).toBe("symmetric");
  });

  it("is a no-op for the plaintext domain", () => {
    expect(() =>
      assertOpenerMatchesDomain("plaintext", new PlaintextBodyOpener()),
    ).not.toThrow();
  });

  it("throws for a domain in neither the catalog nor the application's modes", async () => {
    // The publisher refuses the same domain, so a consumer wired for it would
    // otherwise pass wire-up with whatever opener it was handed.
    const { opener } = await sealerOpener();

    expect(() =>
      assertOpenerMatchesDomain("not-a-domain", new PlaintextBodyOpener()),
    ).toThrow(
      "Encryption domain 'not-a-domain' has no mode in the framework catalog or the application's modes",
    );
    expect(() =>
      assertOpenerMatchesDomain(
        "not-a-domain",
        CryptoBodyOpener.sealed("not-a-domain", opener),
        { "app-orders-fixture": "sealed" },
      ),
    ).toThrow("Encryption domain 'not-a-domain' has no mode");
  });
});
