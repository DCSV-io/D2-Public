// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { SealedFrame } from "@dcsv-io/d2-encryption-abstractions";

import { importPublicP256 } from "./ecdh-p256.js";
import { validateKid, validateServiceId } from "./sealed-keyring-validation.js";

/**
 * Immutable keyring containing a recipient service's public sealing keys
 * (active and any retiring); the producer side of the sealed encryption mode,
 * and the twin of .NET `RecipientPublicKeyring`. Contains the recipient service
 * id that anchors the AEAD binding, so producers never pass an AAD by hand.
 *
 * Public keys are wire-public by design and are not sensitive to zeroization,
 * so this type is not disposable. Key material is still never rendered by
 * `toString` (the no-key-bytes-in-logs invariant is uniform across every
 * keyring type). Constructed through the async {@link create} factory, which
 * validates that every entry imports as a P-256 public key: a wrong-curve or
 * garbage key throws at the construction boundary, not at the first seal.
 */
export class RecipientPublicKeyring {
  readonly #publicKeys: ReadonlyMap<string, Uint8Array>;

  /** The recipient service id that the AEAD binding and key derivation anchor on. */
  readonly recipientServiceId: string;

  /** The recipient kid used for new seals. Always present in the keyring. */
  readonly activeKid: string;

  private constructor(
    recipientServiceId: string,
    activeKid: string,
    publicKeys: ReadonlyMap<string, Uint8Array>,
  ) {
    this.recipientServiceId = recipientServiceId;
    this.activeKid = activeKid;
    this.#publicKeys = publicKeys;
  }

  /**
   * Builds a validated recipient public keyring.
   *
   * @param recipientServiceId The recipient service id (lowercase `[a-z0-9-]`,
   *   at most 64 characters). Anchors the AEAD binding and key derivation.
   * @param activeKid The recipient kid used for new seals. Must be present in
   *   `publicKeysByKid`.
   * @param publicKeysByKid All recipient kids a producer may seal under. Each
   *   value must be a valid P-256 SubjectPublicKeyInfo, at most
   *   {@link SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH} bytes.
   * @returns The validated keyring.
   * @throws {RangeError} When an argument violates a stated invariant.
   */
  static async create(
    recipientServiceId: string,
    activeKid: string,
    publicKeysByKid: ReadonlyMap<string, Uint8Array>,
  ): Promise<RecipientPublicKeyring> {
    validateServiceId(recipientServiceId, "recipientServiceId");
    validateKid(activeKid, "activeKid");

    if (!publicKeysByKid.has(activeKid)) {
      throw new RangeError(
        `activeKid '${activeKid}' is not present in publicKeysByKid.`,
      );
    }

    const copies = new Map<string, Uint8Array>();

    for (const [kid, spki] of publicKeysByKid) {
      validateKid(kid, "publicKeysByKid");

      if (
        spki.length < 1 ||
        spki.length > SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH
      ) {
        throw new RangeError(
          `public key for kid '${kid}' must be in ` +
            `[1, ${SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH}] bytes (got ${spki.length}).`,
        );
      }

      // Throw at the boundary: the SPKI must import as a P-256 public key
      // (WebCrypto validates the DER structure and that the point is on the curve).
      try {
        await importPublicP256(spki);
      } catch (err) {
        throw new RangeError(
          `public key for kid '${kid}' is not a valid P-256 SubjectPublicKeyInfo.`,
          { cause: err },
        );
      }

      // Defensive copy: the caller may mutate the original.
      copies.set(kid, Uint8Array.from(spki));
    }

    return new RecipientPublicKeyring(recipientServiceId, activeKid, copies);
  }

  /**
   * Resolves a recipient kid to its public key bytes (SPKI DER).
   *
   * @param kid The kid to look up (a null or undefined kid returns undefined).
   * @returns The public key bytes when present; undefined otherwise.
   */
  tryGetPublicKey(kid: string | undefined): Uint8Array | undefined {
    return kid === undefined ? undefined : this.#publicKeys.get(kid);
  }

  /** Returns a redacted string that never includes key bytes. */
  toString(): string {
    return (
      `RecipientPublicKeyring(recipientServiceId=${this.recipientServiceId}, ` +
      `activeKid=${this.activeKid}, kids=${this.#publicKeys.size})`
    );
  }
}
