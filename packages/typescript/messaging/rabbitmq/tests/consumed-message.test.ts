// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { readHeaderString } from "../src/subscribing/consumed-message.js";
import { toConsumedMessage } from "../src/subscribing/subscriber.js";
import type { AsyncMessage } from "rabbitmq-client";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";

describe("Original transport metadata", () => {
  it("ToConsumedMessage_LegacyDecodedBody_UsesPrivateOriginalBytes", () => {
    const rawBody = Buffer.from(' { "key": 1, "key": 2 } ');
    const expected = Buffer.from(rawBody);
    const normalized = toConsumedMessage({
      body: { key: 2 },
      rawBody,
      deliveryTag: 1,
      consumerTag: "fixture",
      redelivered: false,
      exchange: "fixture",
      routingKey: "",
    });
    rawBody.fill(0);
    expect(normalized.body).toEqual(expected);
  });

  it.each([undefined, "decoded", new Uint8Array([1])])(
    "ToConsumedMessage_InvalidRawBody_%s_RejectsWithoutReconstruction",
    (rawBody) => {
      expect(() =>
        toConsumedMessage({
          body: Buffer.from([1]),
          rawBody,
          deliveryTag: 1,
          consumerTag: "fixture",
          redelivered: false,
          exchange: "fixture",
          routingKey: "",
        } as AsyncMessage),
      ).toThrow("RabbitMQ message rawBody must be a Buffer.");
    },
  );

  it.each([undefined, "", "rejected:id"])(
    "ToConsumedMessage_OriginalId_%s_PreservesWireProperty",
    (messageId) => {
      const normalized = toConsumedMessage({
        body: Buffer.from([1]),
        rawBody: Buffer.from([1]),
        messageId,
        headers: { [AmqpHeaders.MESSAGE_ID]: "processing-id" },
        deliveryTag: 1,
        consumerTag: "fixture",
        redelivered: false,
        exchange: "fixture",
        routingKey: "",
        contentType: "application/octet-stream",
        contentEncoding: "binary",
        correlationId: "correlation",
      } as AsyncMessage);
      expect(normalized.originalMessageId).toBe(messageId);
      expect(normalized.contentEncoding).toBe("binary");
      expect(normalized.messageId).toBe(
        messageId === "rejected:id" ? undefined : "processing-id",
      );
    },
  );
});

describe("readHeaderString", () => {
  it("returns a string header verbatim", () => {
    expect(readHeaderString({ traceparent: "00-abc" }, "traceparent")).toBe(
      "00-abc",
    );
  });

  it("decodes a byte-typed header (Uint8Array / Buffer) as UTF-8", () => {
    const bytes = new Uint8Array(Buffer.from("x-d2-context-value", "utf8"));
    expect(readHeaderString({ "x-d2-context": bytes }, "x-d2-context")).toBe(
      "x-d2-context-value",
    );
  });

  it("returns undefined for an absent or non-string/non-bytes header", () => {
    expect(readHeaderString({}, "traceparent")).toBeUndefined();
    expect(readHeaderString({ n: 42 }, "n")).toBeUndefined();
  });
});
