<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/error-category/`

Error-category catalog — the closed set of semantic failure categories (`validation_failure`, `not_found`, `conflict`, `policy_denied`, `rate_limited`, `payload_too_large`, `infrastructure_unavailable`, `internal_error`, `partial_success`) shared by every error code across all domains.

## Consumed by

- **.NET** — [`packages/dotnet/error-codes/category-source-gen/`](../../packages/dotnet/error-codes/category-source-gen/) (Roslyn source-gen → `ErrorCategory` enum + `ErrorCategoryJsonConverter` in `DcsvIo.D2.ErrorCodes.Category`; no README). The shared error-code engine ([`source-gen-shared/error-codes-source-gen/`](../../packages/dotnet/source-gen-shared/error-codes-source-gen/README.md)) reads the same category wire strings when emitting per-domain factories.
- **TypeScript** — constants/types in `@dcsv-io/d2-error-category` (generated from this spec; sources committed)
- **TypeSpec** — internal IDL tooling; not required for published packages

## See also

- All contracts: [contracts catalog](../README.md)
