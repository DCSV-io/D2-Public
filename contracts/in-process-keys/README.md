<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/in-process-keys/`

In-process key registry — the closed set of named keys used to stash per-request state inside a single service process (HTTP `HttpContext.Items` slots and gRPC user-state interceptor keys), kept identical across the HTTP and gRPC transports.

## Consumed by

- **.NET** — [`packages/dotnet/encryption/in-process-keys-source-gen/`](../../packages/dotnet/encryption/in-process-keys-source-gen/README.md) (Roslyn source-gen → `D2HttpContextItems` in `DcsvIo.D2.Auth.Abstractions` for `http`-bound entries; host-supplied gRPC bindings may emit matching UserState keys for `grpc`-bound entries)

No TypeScript package consumes this catalog — the keys are an in-process .NET concern with no wire representation.

## See also

- All contracts: [contracts catalog](../README.md)
