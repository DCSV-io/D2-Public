<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/grpc-trailers/`

gRPC trailer key catalog — the metadata trailer names written by the .NET gRPC handler (`d2_error_code`, `d2_messages`, `traceId`) and read by the TypeScript gRPC client to reconstruct a `D2Result` from a gRPC response.

## Consumed by

- **.NET** — host-supplied gRPC transport bindings may emit trailer-key constants from this catalog
- **TypeScript** — trailer-key constants for gRPC clients that reconstruct `D2Result` from trailers (generated from this spec when present; host-supplied clients may consume the same wire names)

## See also

- All contracts: [contracts catalog](../README.md)
