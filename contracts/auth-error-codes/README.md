<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-error-codes/`

Auth error-code catalog — the closed set of authentication and authorization failure codes (bearer missing, JWT expired, scope insufficient, session revoked, etc.) with their HTTP status, error category, and user-message key.

## Consumed by

- **.NET** — host-supplied auth error factories may consume this catalog; the open TS package ships matching constants (see below)
- **TypeScript** — constants/types in `@dcsv-io/d2-auth-abstractions` (generated from this spec; sources committed)
- **TypeSpec** — internal IDL tooling; not required for published packages

This catalog is also merged into the cross-service registry — see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- All contracts: [contracts catalog](../README.md)
