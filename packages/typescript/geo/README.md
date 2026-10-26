<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# geo/

> Parent: [`packages/typescript/`](../README.md)

Spec-driven geographic reference data and lookup contracts for TypeScript consumers: the minimal hand-written API
surface and the codegen-emitted in-memory catalogs. Both are emitted from the same `contracts/geo/*.spec.json` files
(countries, currencies, geopolitical-entities, languages, locales, subdivisions, timezones) that drive the .NET side
(sources committed), so .NET and TypeScript cannot drift apart. Domain code can reference the abstractions without
importing catalog data.

## Packages

| Package                                   | Description                                                                                                                                                                                                   |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | The minimal hand-written geo API surface - `IGeoReference`, `IGeoNameResolver` + normalization helpers, `DeprecationInfo`. Spec-derived types are codegen-emitted here. Mirrors `DcsvIo.D2.Geo.Abstractions`. |
| [`default/`](default/README.md)           | The codegen-emitted in-memory geo catalogs - per-entity records, nested lookup objects, flat lookup maps, and a module-init coordinator. Mirrors `DcsvIo.D2.Geo.Default`.                                     |
