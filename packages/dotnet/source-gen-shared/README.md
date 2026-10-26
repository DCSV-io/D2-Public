<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# source-gen-shared/

> Parent: [`packages/dotnet/`](../README.md)

The shared source-generator scaffolding and the cross-cutting generators that have no single owning cluster. Every
spec-driven generator in the .NET stack compiles in the same shared scaffolding `.cs` files, so the netstandard2.0
polyfills and cross-generator records are identical in every generator and no generator can lack a polyfill. The
generators in this folder emit catalogs consumed across multiple clusters rather than within one - the generic
`D2Result` error-code taxonomy and the per-wire-shape JSON property-name catalogs - so they belong to no single
cluster.

## Packages

| Package                                                                         | Description                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [`core/`](core/) <!-- shared .cs scaffolding only; no csproj and no README. --> | Shared source-gen scaffolding (not a project) - the netstandard2.0 polyfills plus the cross-generator records (`EmitDiagnostic`, `LoadResult<TSpec>`, `SpecFile`) and the output-target selection `ExtensionTargetSelector`. Wired into each generator via `<Compile Include>`.      |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)                   | Roslyn generator emitting the generic `D2Result` error-code taxonomy (`ErrorCodes`) into `result/core/` from `contracts/error-codes/error-codes.spec.json`.                                                                                                                          |
| [`wire-shapes-source-gen/`](wire-shapes-source-gen/README.md)                   | Roslyn generator with multi-target dispatch - emits per-wire-shape JSON property-name catalogs into `i18n/abstractions/` and `result/core/` from the matching `contracts/<wire-shape>/` specs.                                                                                       |
| [`error-codes-emit/`](error-codes-emit/README.md)                               | Error-codes generation engine, parameterized by `CatalogConfig`, behind every `*-error-codes` Roslyn catalog. It is a single engine (spec loader + constants/failures/generic-twin emitters + `CatalogConfig`) compiled into each consuming generator shell via `<Compile Include>`. |
