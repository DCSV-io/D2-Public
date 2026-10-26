// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { DlqFailureCauses } from "@dcsv-io/d2-messaging-abstractions";
import {
  EncryptionFrame,
  SealedFrame,
} from "@dcsv-io/d2-encryption-abstractions";

import { MessageBodyDecodeError } from "./message-body-decode-error.js";

/**
 * Decodes a raw AMQP delivery body into the object a handler consumes. The
 * opener is injected: the default handles plaintext domains, and an
 * encrypted-domain opener (WebCrypto decrypt) replaces it for those domains. An
 * opener MUST throw {@link MessageBodyDecodeError} for any body it cannot
 * faithfully decode, and never swallows one silently.
 */
export interface BodyOpener {
  /**
   * Decodes the raw body bytes into the handler's message value. The method
   * may return a promise, because a decrypting opener (WebCrypto) is async; the
   * delivery pipeline awaits the result. The default plaintext opener is
   * synchronous.
   *
   * @param body Raw AMQP body bytes (opaque `application/octet-stream`).
   * @throws MessageBodyDecodeError on an undecodable body.
   */
  open(body: Buffer): unknown | Promise<unknown>;
}

/**
 * The default {@link BodyOpener}, for plaintext domains only. It parses a raw
 * UTF-8 JSON body. It throws for any body whose first byte is a known
 * encryption-frame version (1 = symmetric {@link EncryptionFrame}, 2 =
 * sealed {@link SealedFrame}). An encrypted body reaching this opener means no
 * decrypting opener was registered for the domain, so the body goes to the DLQ
 * with `DECRYPT_FAILURE` and is not parsed as JSON, and it is never dropped
 * silently. A malformed plaintext body goes to the DLQ with
 * `DESERIALIZE_FAILURE`.
 */
export class PlaintextBodyOpener implements BodyOpener {
  open(body: Buffer): unknown {
    if (body.length > 0) {
      const firstByte = body[0];
      if (
        firstByte === EncryptionFrame.CURRENT_VERSION ||
        firstByte === SealedFrame.CURRENT_VERSION
      ) {
        throw new MessageBodyDecodeError(
          DlqFailureCauses.DECRYPT_FAILURE,
          "encrypted body received on a plaintext opener (no decrypting " +
            "opener registered for this domain)",
        );
      }
    }

    const text = body.toString("utf8");
    try {
      return JSON.parse(text);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DESERIALIZE_FAILURE,
        "message body is not valid UTF-8 JSON",
        err,
      );
    }
  }
}
