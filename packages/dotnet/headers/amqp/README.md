<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Amqp

D2 wire-protocol headers for the AMQP transport. Includes AMQP-only entries (`content-type`, `x-proto-type`, `message-id`, `timestamp`, `x-d2-encryption-kid`, `x-d2-failure-reason`) and cross-transport entries (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from the headers contract spec via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Contains("amqp")`). Mirrors the TypeScript package `@dcsv-io/d2-headers-amqp` at byte-equal wire values; parity is asserted by shared contract tests.

## Install

```bash
dotnet add package DcsvIo.D2.Headers.Amqp
```

## Public API

| Member                           | Type                                 | Purpose                                                           |
| -------------------------------- | ------------------------------------ | ----------------------------------------------------------------- |
| `AmqpHeaders.CONTENT_TYPE`       | `const string "content-type"`        | Always `application/octet-stream` for D2 messages                 |
| `AmqpHeaders.ENCRYPTION_KID`     | `const string "x-d2-encryption-kid"` | Encryption key id duplicated from the encrypted frame for DLQ ops |
| `AmqpHeaders.FAILURE_REASON`     | `const string "x-d2-failure-reason"` | DLQ failure metadata attached by the consumer on nack             |
| `AmqpHeaders.MESSAGE_ID`         | `const string "message-id"`          | UUIDv7 message identifier                                         |
| `AmqpHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`        | Base64url-of-JSON propagated context envelope (cross-transport)   |
| `AmqpHeaders.PROTO_TYPE`         | `const string "x-proto-type"`        | Fully-qualified proto type name                                   |
| `AmqpHeaders.TIMESTAMP`          | `const string "timestamp"`           | Producer-set ISO 8601 UTC timestamp                               |
| `AmqpHeaders.TRACEPARENT`        | `const string "traceparent"`         | W3C Trace Context (cross-transport)                               |
| `AmqpHeaders.TRACESTATE`         | `const string "tracestate"`          | W3C tracestate (cross-transport)                                  |
| `AmqpHeaders.AllAmqpHeaders`     | `IReadOnlyList<string>`              | All wire values in `constName` order                              |

## Header categories

- **Routing + observability**: `MESSAGE_ID`, `TIMESTAMP`, `CONTENT_TYPE`, `PROTO_TYPE`
- **Cross-hop tracing**: `TRACEPARENT`, `TRACESTATE`, `PROPAGATED_CONTEXT`
- **Encryption + DLQ ops**: `ENCRYPTION_KID`, `FAILURE_REASON`

Headers MUST NOT carry user identity, scopes, fingerprints, or any other sensitive context — the broker stores headers as plaintext at rest. Sensitive identity that a consumer needs goes in the typed message body (encrypted via the descriptor's `encryption` domain when the type carries PII).

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Amqp` from any AMQP-context consumer — RabbitMQ publishers, subscribers, DLQ inspection tools. One `using` covers AMQP-only entries and cross-transport entries.

## Spec contract

The headers contract spec is the single source of truth. Every entry whose `applicability` array contains `"amqp"` lives in this catalog.

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

Sister packages: `DcsvIo.D2.Headers.Common`, `DcsvIo.D2.Headers.Http`, `DcsvIo.D2.Headers.Grpc`.
