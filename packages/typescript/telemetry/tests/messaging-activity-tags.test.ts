// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { expect, it } from "vitest";
import {
  MessagingActivityTags,
  ALL_MESSAGING_ACTIVITY_TAGS,
} from "../src/index.js";

it("ExportsCanonicalWireNames_ForMessagingActivityTags", () => {
  expect(MessagingActivityTags).toEqual({
    MESSAGING_SYSTEM: "messaging.system",
    MESSAGING_DESTINATION_NAME: "messaging.destination.name",
    MESSAGING_RABBITMQ_ROUTING_KEY: "messaging.rabbitmq.routing_key",
    MESSAGING_RABBITMQ_DELIVERY_TAG: "messaging.rabbitmq.delivery_tag",
    MESSAGING_RABBITMQ_REDELIVERED: "messaging.rabbitmq.redelivered",
    MESSAGING_MESSAGE_ID: "messaging.message.id",
    MESSAGING_OPERATION_TYPE: "messaging.operation.type",
    D2_MESSAGE_TYPE: "d2.message_type",
    D2_ENCRYPTION_KID: "d2.encryption_kid",
  });
});

it("ExportsCompleteOrderedWireNames_ForMessagingActivityCatalog", () => {
  expect(ALL_MESSAGING_ACTIVITY_TAGS).toEqual([
    "messaging.system",
    "messaging.destination.name",
    "messaging.rabbitmq.routing_key",
    "messaging.rabbitmq.delivery_tag",
    "messaging.rabbitmq.redelivered",
    "messaging.message.id",
    "messaging.operation.type",
    "d2.message_type",
    "d2.encryption_kid",
  ]);
});
