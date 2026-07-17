<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# contracts/

Shared specifications for D2 open libraries — JSON schemas, value catalogs, proto definitions, and i18n message files. These specs are the single source of truth for cross-language constant catalogs: .NET Roslyn source generators and TypeScript packages emit matching constants and types from the same files, so wire identifiers stay byte-equal across runtimes.

Generated outputs ship **committed** in each consuming package (`*.g.cs` / `*.g.ts`). You do not need the generators to build or consume the published packages.

## Layout

Each subdirectory holds one catalog or contract family (`*.spec.json` + `schema.json` + a short README). See the per-folder README for what the catalog describes and which packages consume it.

| Area | Examples |
| --- | --- |
| Auth vocabulary | `auth-scopes/`, `auth-audiences/`, `auth-protocol-audiences/`, `auth-context/`, `jwt-claims/`, `auth-error-codes/` |
| Results / errors | `error-codes/`, `error-category/`, `d2result-envelope/`, `input-error/`, `problem-details/`, `tk-message/` |
| Headers / messaging | `headers/`, `mq-messages/`, `mq-subscriptions/`, `dlq-failure-metadata/`, `otel-messaging-tags/`, `grpc-trailers/` |
| Encryption | `encryption-domains/`, `encryption-frame/`, `encryption-frame-sealed/`, `in-process-keys/` |
| Context / telemetry | `request-context/`, `telemetry/` |
| Reference data | `geo/`, `validation/`, `messages/`, `location/`, `enum/`, `temporal/`, `resilience/` |
| Protos | `protos/` |

## Consuming packages

| Runtime | Home |
| --- | --- |
| .NET | `packages/dotnet/**` (Roslyn generators + runtime libs) |
| TypeScript | `packages/typescript/**` (committed generated sources) |
