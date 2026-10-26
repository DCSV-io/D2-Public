<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# context/

> Parent: [`packages/dotnet/`](../README.md)

The per-request context primitives D2 services pass between hops - the spec-driven `IRequestContext` surface plus the
source generator that emits it. Domain code references the abstractions to read caller identity, scopes,
impersonation, and propagated operational state without referencing DI / AspNetCore / Configuration. The propagated
subset is sent in the `x-d2-context` header between services; full identity is rebuilt from the JWT at each hop.

## Packages

| Package                                   | Description                                                                                                                                                                                                                  |
| ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | Defines every spec-driven context primitive, and no other package defines one - codegen-emitted `IRequestContext` / `MutableRequestContext` / `PropagatedContext` plus hand-written RFC actor-chain and scope-claim parsers. |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator that reads `contracts/{auth,request}-context/*.spec.json` and emits the context interfaces / records into `abstractions/` and `auth/context-abstractions/`.                                                 |
