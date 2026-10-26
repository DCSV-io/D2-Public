<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# auth/

> Parent: [`packages/typescript/`](../README.md)

Auth-related vocabulary for TypeScript consumers: the codegen-emitted scope, error-code and claim catalogs, and the
domain-safe auth-context interface. Every catalog is emitted from the same `contracts/` specs that drive the .NET shared
libraries, so .NET and TypeScript cannot drift apart. A SvelteKit backend-for-frontend (BFF) server and other Node
services read these constants when they decode inbound JWTs and populate the request context.

## Packages

| Package                                                   | Description                                                                                                                          |
| --------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md)                 | Codegen-emitted `Scopes` / `AuthErrorCodes` / `AuthFailures` / `JwtClaimTypes` catalogs. Mirrors `DcsvIo.D2.Auth.Abstractions`.      |
| [`context-abstractions/`](context-abstractions/README.md) | The `IAuthContext` interface, codegen from `contracts/auth-context/IAuthContext.spec.json`. Mirrors `DcsvIo.D2.AuthContext.Abstractions`. |
