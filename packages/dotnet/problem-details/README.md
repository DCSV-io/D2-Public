<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# problem-details/

> Parent: [`packages/dotnet/`](../README.md)

The RFC 7807 ProblemDetails wire shape for every service that emits a structured error body. The cluster contains the
codegen-emitted key catalog and the source generator that emits it from
`contracts/problem-details/problem-details.spec.json`. Both .NET emit paths - the auth-middleware path and the ASP.NET
`IProblemDetailsService` customizer path - use the same constant set, so they produce byte-identical bodies. The
same spec drives the TS-side ProblemDetails catalog.

## Packages

| Package                                   | Description                                                                                                                                                                                     |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | The only package that contains the codegen-emitted `D2ProblemDetailsKeys` static class - type-URI prefix, content type, extension keys, per-status titles, `TitleFor`. No runtime dependencies. |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator emitting `D2ProblemDetailsKeys.g.cs` into `abstractions/` from `contracts/problem-details/problem-details.spec.json`.                                                          |
