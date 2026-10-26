<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/problem-details/`

RFC 7807 problem-details extension-key catalog - the `type` URI prefix and the extension property names written into
ASP.NET Core problem-details responses: the `d2_`-prefixed keys, `traceId` and `correlationId`.

## Consumed by

- **.NET** - [`packages/dotnet/problem-details/source-gen/`](../../packages/dotnet/problem-details/source-gen/README.md) (Roslyn source-gen -> `D2ProblemDetailsKeys` constants in `DcsvIo.D2.ProblemDetails.Abstractions`)
- **TypeScript** - constants/types in `@dcsv-io/d2-problem-details-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
