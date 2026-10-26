<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# contracts/

Shared specifications for D2 open libraries - JSON schemas, value catalogs, proto definitions, and i18n message files.
These specs are the single source of truth for cross-language constant catalogs: .NET Roslyn source generators and
TypeScript packages emit matching constants and types from the same files, so wire identifiers stay byte-equal across
runtimes.

Generated outputs ship **committed** in each consuming package (`*.g.cs` / `*.g.ts`). You do not need the generators to
build or consume the published packages.

## Layout

Each subdirectory contains one catalog or contract family (`*.spec.json` + `schema.json` + a short README). See the
per-folder README for what the catalog describes and which packages consume it.

| Area                | Contracts                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Auth vocabulary     | [`auth-scopes/`](auth-scopes/README.md), [`auth-audiences/`](auth-audiences/README.md), [`auth-protocol-audiences/`](auth-protocol-audiences/README.md), [`auth-context/`](auth-context/README.md), [`jwt-claims/`](jwt-claims/README.md), [`auth-error-codes/`](auth-error-codes/README.md)                                                                                                                                                                                             |
| Results / errors    | [`error-codes/`](error-codes/README.md), [`pagination-error-codes/`](pagination-error-codes/README.md), [`error-category/`](error-category/README.md), [`d2result-envelope/`](d2result-envelope/README.md), [`input-error/`](input-error/README.md), [`problem-details/`](problem-details/README.md), [`tk-message/`](tk-message/README.md)                                                                                                                                              |
| Headers / messaging | [`headers/`](headers/README.md), [`mq-messages/`](mq-messages/README.md), [`mq-subscriptions/`](mq-subscriptions/README.md) (both contain the entry shape and no entries - an application declares its message types and subscriptions in its own catalog), [`dlq-failure-metadata/`](dlq-failure-metadata/README.md), [`messaging-error-codes/`](messaging-error-codes/README.md), [`otel-messaging-tags/`](otel-messaging-tags/README.md), [`grpc-trailers/`](grpc-trailers/README.md) |
| Encryption          | [`encryption-domains/`](encryption-domains/README.md), [`encryption-frame/`](encryption-frame/README.md), [`encryption-frame-sealed/`](encryption-frame-sealed/README.md), [`in-process-keys/`](in-process-keys/README.md)                                                                                                                                                                                                                                                               |
| Context / telemetry | [`request-context/`](request-context/README.md), [`telemetry/`](telemetry/README.md)                                                                                                                                                                                                                                                                                                                                                                                                     |
| Reference data      | [`geo/`](geo/README.md), [`validation/`](validation/README.md), [`messages/`](messages/README.md), [`location/`](location/README.md), [`enum/`](enum/README.md), [`temporal/`](temporal/README.md), [`resilience/`](resilience/README.md)                                                                                                                                                                                                                                                |
| Protos              | [`protos/`](protos/README.md)                                                                                                                                                                                                                                                                                                                                                                                                                                                            |

## Consuming packages

| Runtime    | Home                                                    |
| ---------- | ------------------------------------------------------- |
| .NET       | `packages/dotnet/**` (Roslyn generators + runtime libs) |
| TypeScript | `packages/typescript/**` (committed generated sources)  |
