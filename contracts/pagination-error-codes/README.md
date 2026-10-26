<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/pagination-error-codes/`

Pagination (owner **lib**) error-code catalog - wire prefix **`LIB_PAGINATION_*`**. Closed set of list-paging and
list-search failure codes (bad sort field, undecodable cursor, stale cursor, query too short, a search query presented
against a surface that declared no search policy, and the host-configuration defects behind them) with their HTTP
status, error category, and user-message key.

Every code has its own message key under `TK.Common.Pagination.*`, because a caller shown a paging failure needs to
know which request parameter to change.

## Status split

| Status | What the codes in it describe                                                                            |
| ------ | -------------------------------------------------------------------------------------------------------- |
| `400`  | Something the caller sent. The fix is a different request.                                               |
| `500`  | A host configuration or cursor-encode defect. The caller sent nothing wrong and can do nothing about it. |

## Consumed by

- **.NET** - `DcsvIo.D2.Pagination` emits `PaginationErrorCodes` + `PaginationFailures` from this catalog through `DcsvIo.D2.Pagination.ErrorCodes.SourceGen`
- **TypeScript** - resolvable through the merged registry in `@dcsv-io/d2-error-codes-registry`, with message keys in `@dcsv-io/d2-i18n-keys`

This catalog is also merged into the public cross-service registry - see
[`contracts/error-codes/`](../error-codes/README.md).

## See also

- All contracts: [contracts catalog](../README.md)
