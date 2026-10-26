<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/headers/`

HTTP, AMQP and gRPC header registry - the closed set of D2-specific headers used across the HTTP, AMQP and gRPC
transports, with the transports each applies to. `headers.spec.json` lists them (for example `X-D2-Internal-Token` and
`x-d2-context`), together with the standard headers the platform reads (`traceparent`, `Authorization`).

## Consumed by

- **.NET** - [`packages/dotnet/headers/source-gen/`](../../packages/dotnet/headers/source-gen/README.md) (Roslyn source-gen -> per-transport header-name constants in `DcsvIo.D2.Headers.Common` / `.Http` / `.Grpc` / `.Amqp`)
- **TypeScript** - constants/types in `@dcsv-io/d2-headers-{common,http,amqp,grpc}` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
