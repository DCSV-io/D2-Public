<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/messaging-error-codes/`

Messaging-transport (owner **lib**) error-code catalog - wire prefix **`LIB_MESSAGING_*`**. Failures of the
transport itself, where the message could not be carried and the caller sent nothing wrong.

Every code is `503` / `infrastructure_unavailable`, and every code shares the generic
`TK.Common.Errors.SERVICE_UNAVAILABLE` message key. A person is told the service could not take the message;
the error code and the publisher's logs state the cause an operator acts on.

## Design rationale: a separate messaging transport code

`GEN_SERVICE_UNAVAILABLE` reports that a dependency was unreachable. `LIB_MESSAGING_PUBLISH_UNROUTABLE` reports that
the broker was reachable, took the message, and had no queue to route it to - the exchange exists and nothing is bound
to it for that routing key. Each condition needs a different response. An operator waits out an unreachable
dependency; an unroutable publish is a topology or deployment defect, and the operator fixes the missing binding or
exchange declaration.

## Codes

| Code                               | Status | Means                                                                                           |
| ---------------------------------- | ------ | ----------------------------------------------------------------------------------------------- |
| `LIB_MESSAGING_PUBLISH_UNROUTABLE` | `503`  | The publish matched no queue on every attempt, including after the publisher's backoff retries. |

## Consumed by

- **.NET** - `DcsvIo.D2.Messaging.RabbitMq` emits `MessagingErrorCodes` + `MessagingTransportFailures` from
  this catalog through `DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen`
- **TypeScript** - resolvable through the merged registry in `@dcsv-io/d2-error-codes-registry`, with message
  keys in `@dcsv-io/d2-i18n-keys`

This catalog is also merged into the public cross-service registry - see
[`contracts/error-codes/`](../error-codes/README.md).

## See also

- All contracts: [contracts catalog](../README.md)
