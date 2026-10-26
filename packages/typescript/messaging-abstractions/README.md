<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-messaging-abstractions

D2 messaging-protocol wire identifiers, including the DLQ failure-metadata wire shape (`DlqFailureMetadataFields` for
JSON property names + `DlqFailureCauses` for the closed-enum cause-string catalog). Mirrors .NET
`DcsvIo.D2.Messaging.DlqFailureMetadataFields` (in `DcsvIo.D2.Messaging.Abstractions`) and
`DcsvIo.D2.Messaging.RabbitMq.Subscribing.DlqFailureCauses` (in `DcsvIo.D2.Messaging.RabbitMq`).

## Install

```bash
pnpm add @dcsv-io/d2-messaging-abstractions
```

## Public API

| Export                            | Source                      | Mirror                                                      |
| --------------------------------- | --------------------------- | ----------------------------------------------------------- |
| `DlqFailureMetadataFields`        | `dlq-failure-metadata.g.ts` | `DcsvIo.D2.Messaging.DlqFailureMetadataFields`              |
| `DlqFailureMetadataField`         | `dlq-failure-metadata.g.ts` | n/a (TS-only union type)                                    |
| `ALL_DLQ_FAILURE_METADATA_FIELDS` | `dlq-failure-metadata.g.ts` | `DlqFailureMetadataFields.AllFields`                        |
| `DlqFailureCauses`                | `dlq-failure-metadata.g.ts` | `DcsvIo.D2.Messaging.RabbitMq.Subscribing.DlqFailureCauses` |
| `DlqFailureCause`                 | `dlq-failure-metadata.g.ts` | n/a (TS-only union type)                                    |
| `ALL_DLQ_FAILURE_CAUSES`          | `dlq-failure-metadata.g.ts` | `DlqFailureCauses.AllCauses`                                |

## Codegen workflow

`prebuild` regenerates catalogs when a generator is available. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

- DLQ operations tooling: a tool that reads the `x-d2-failure-reason` header from DLQ messages uses the
  field-name constants to deserialize the JSON shape and the cause-string constants to branch on failure category.
  `@dcsv-io/d2-messaging-rabbitmq` imports the same constants to build that header, so the tool and the publisher use
  one set of names.
- TS-side RabbitMQ subscribers: any handler that may end up generating a DLQ entry (via header forwarding)
  references the same cause constants.

## Spec contract

`contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json` is the single source of truth.
It has the sub-catalogs `fields[]` (property names) and `causes[]` (closed-enum cause strings). The .NET side emits the
fields half into `DcsvIo.D2.Messaging.Abstractions` and the causes half into `DcsvIo.D2.Messaging.RabbitMq`.

## Dependencies

None at runtime - pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.
