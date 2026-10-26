<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/telemetry/`

Telemetry tag catalog - the closed set of OTel activity/span tag names, meter names, and instrument definitions used across D2 services for distributed tracing and metrics.

## Consumed by

- **.NET** - [`packages/dotnet/telemetry/tags-source-gen/`](../../packages/dotnet/telemetry/tags-source-gen/README.md)
  (Roslyn source-gen -> tag-name constants + meter/instrument descriptors; multi-target - each meter group emits into
  the `consumingAssembly` declared in its spec entry, e.g. `DcsvIo.D2.Handler`, `DcsvIo.D2.Messaging.RabbitMq`,
  `DcsvIo.D2.Caching.*`)

No TypeScript package consumes this catalog. The TypeScript messaging tags come from `otel-messaging-tags/`.

## See also

- All contracts: [contracts catalog](../README.md)
