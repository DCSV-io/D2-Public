// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { IPayloadCrypto, IPayloadOpener } from "@dcsv-io/d2-encryption";
import type { EncryptionDomainMode } from "@dcsv-io/d2-encryption-abstractions";
import { DlqFailureCauses } from "@dcsv-io/d2-messaging-abstractions";

import {
  type ApplicationDomainModes,
  resolveDomainModes,
} from "../encryption-domain-modes.js";
import type { BodyOpener } from "./body-opener.js";
import { MessageBodyDecodeError } from "./message-body-decode-error.js";

const _decoder = new TextDecoder("utf-8");
const _PLAINTEXT = "plaintext";

/**
 * A mode-aware decrypting {@link BodyOpener}. It is the consumer twin of the
 * .NET `EncryptedBodyComposer.Decompose` sealed/symmetric branch, and the
 * decrypting opener for the body-decompose seam. Built for one domain with one
 * port:
 *
 * - a SEALED domain accepts ONLY v2 frames (the {@link IPayloadOpener} rejects
 *   any other version); a SYMMETRIC domain accepts ONLY v1 (the
 *   {@link IPayloadCrypto} rejects any other version);
 * - a plaintext body on an encrypted domain, tampering, an unknown kid, or the
 *   wrong frame version all surface as {@link MessageBodyDecodeError} with
 *   `DECRYPT_FAILURE`, and the delivery routes to the DLQ, so the body is never
 *   mis-decoded silently; a decrypted-but-non-JSON body surfaces as
 *   `DESERIALIZE_FAILURE`.
 *
 * Deps: `@dcsv-io/d2-encryption` only. The host composes the port instance;
 * this shared library never depends on a service-owned package.
 */
export class CryptoBodyOpener implements BodyOpener {
  readonly #domain: string;
  readonly #mode: EncryptionDomainMode;
  readonly #decrypt: (body: Uint8Array) => Promise<Uint8Array>;

  private constructor(
    domain: string,
    mode: EncryptionDomainMode,
    decrypt: (body: Uint8Array) => Promise<Uint8Array>,
  ) {
    this.#domain = domain;
    this.#mode = mode;
    this.#decrypt = decrypt;
  }

  /**
   * Builds a sealed-domain opener over a private-keyring {@link IPayloadOpener}.
   *
   * @param domain The sealed encryption domain this opener serves.
   * @param opener The sealed payload opener, composed by the host.
   */
  static sealed(domain: string, opener: IPayloadOpener): CryptoBodyOpener {
    return new CryptoBodyOpener(domain, "sealed", (body) => opener.open(body));
  }

  /**
   * Builds a symmetric-domain opener over a keyring {@link IPayloadCrypto}.
   *
   * @param domain The symmetric encryption domain this opener serves.
   * @param crypto The symmetric payload crypto, composed by the host.
   */
  static symmetric(domain: string, crypto: IPayloadCrypto): CryptoBodyOpener {
    return new CryptoBodyOpener(domain, "symmetric", (body) =>
      crypto.decrypt(body),
    );
  }

  /** The encryption domain this opener is bound to. */
  get domain(): string {
    return this.#domain;
  }

  /** The encryption mode this opener decrypts: `sealed` or `symmetric`. */
  get mode(): EncryptionDomainMode {
    return this.#mode;
  }

  /** @inheritdoc */
  async open(body: Buffer): Promise<unknown> {
    let plaintext: Uint8Array;

    try {
      plaintext = await this.#decrypt(body);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DECRYPT_FAILURE,
        `encrypted body for domain '${this.#domain}' could not be opened`,
        err,
      );
    }

    const text = _decoder.decode(plaintext);

    try {
      return JSON.parse(text);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DESERIALIZE_FAILURE,
        `decrypted body for domain '${this.#domain}' is not valid UTF-8 JSON`,
        err,
      );
    }
  }
}

/**
 * Asserts that a body opener matches an encryption domain's mode. This is the
 * consumer-side runtime cross-check, the TS twin of the .NET
 * subscriber-vs-opener boot check. The domain's mode resolves from the
 * framework `EncryptionDomainModes` catalog plus `modes`, the application's own domain modes (the same map passed to
 * `createPublisher({ modes })`):
 *
 * - a SEALED domain must be wired with a sealed {@link CryptoBodyOpener} for that
 *   domain, never a plaintext or symmetric one;
 * - a SYMMETRIC domain must be wired with a symmetric {@link CryptoBodyOpener}
 *   for that domain, never a plaintext or sealed one;
 * - a domain with no resolved mode is refused, as the publisher refuses it;
 * - `plaintext` is not checked.
 *
 * A mismatch throws at wire-up, when a subscription is composed.
 *
 * @param domain The message's encryption domain (from its descriptor).
 * @param opener The body opener the host wired for the subscription.
 * @param modes The application's own domain modes; omitted, the framework catalog alone.
 * @throws When the domain is not `plaintext` and resolves to no mode, when the
 *   opener does not match the domain's required mode, or when `modes` fails
 *   {@link resolveDomainModes} validation.
 */
export function assertOpenerMatchesDomain(
  domain: string,
  opener: BodyOpener,
  modes?: ApplicationDomainModes,
): void {
  const resolved = resolveDomainModes(modes);

  if (domain === _PLAINTEXT) return;

  const mode = resolved[domain];

  if (mode === undefined) {
    throw new Error(
      `Encryption domain '${domain}' has no mode in the framework catalog or ` +
        "the application's modes - the publisher refuses it, so no opener can " +
        "be checked against it. Pass the domain's mode in `modes`.",
    );
  }

  if (mode === "sealed") {
    if (
      !(opener instanceof CryptoBodyOpener) ||
      opener.domain !== domain ||
      opener.mode !== "sealed"
    ) {
      throw new Error(
        `Sealed domain '${domain}' requires a sealed CryptoBodyOpener for that ` +
          "domain, but none was wired - every delivery would DLQ. Wire the " +
          "managed sealed opener for this consumer.",
      );
    }

    return;
  }

  if (!(opener instanceof CryptoBodyOpener)) {
    throw new Error(
      `Symmetric domain '${domain}' requires a symmetric CryptoBodyOpener for ` +
        "that domain, but none was wired - every delivery would DLQ and a " +
        "cleartext body would be read as genuine. Wire the symmetric opener " +
        "for this domain.",
    );
  }

  if (opener.domain !== domain || opener.mode !== "symmetric") {
    throw new Error(
      `Symmetric domain '${domain}' is wired with a ${opener.mode} ` +
        `CryptoBodyOpener for domain '${opener.domain}' - every delivery would ` +
        "DLQ. Wire the symmetric opener for this domain.",
    );
  }
}
