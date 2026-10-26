<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-error-codes/`

Auth (owner **lib**) error-code catalog - wire prefix **`LIB_AUTH_*`**. Closed set of authentication and authorization
failure codes (bearer missing, JWT expired, scope insufficient, session revoked, etc.) with their HTTP status, error
category, and user-message key.

## Consumed by

- **.NET** - host-supplied auth error factories may consume this catalog; the TypeScript package
  `@dcsv-io/d2-auth-abstractions` ships matching constants
- **TypeScript** - constants/types in `@dcsv-io/d2-auth-abstractions` (generated from this spec; sources committed)
- **TypeSpec** - internal IDL tooling; not required for published packages

This catalog is also merged into the public cross-service registry - see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- All contracts: [contracts catalog](../README.md)
