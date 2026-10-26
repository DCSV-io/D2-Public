<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.WireShapes.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contracts:** [`contracts/tk-message/`](../../../../contracts/tk-message/README.md) +
[`contracts/input-error/`](../../../../contracts/input-error/README.md)

A Roslyn `IIncrementalGenerator` (netstandard2.0) that emits a static class of JSON property names for each wire shape.
It reads `contracts/<wire-shape>/<wire-shape>.spec.json` files through `<AdditionalFiles>`. It dispatches to several
targets: one analyzer and one shared scaffolding serve several consuming catalog assemblies. It uses the per-transport
dispatch pattern of `DcsvIo.D2.Headers.SourceGen`.

The generator enforces the spec-driven wire-identifier contract, in which every cross-language wire-format identifier is
declared in a spec and covered by a parity test. It applies the contract to the `TKMessage` (`{key, params?}`) and
`InputError` (`{field, errors}`) wire shapes. Both shapes travel across the .NET <-> TypeScript boundary inside the
`D2Result` envelope. The property names come from a spec, so the .NET serializer and the TypeScript parser use the same
source for the JSON keys, and the property names cannot drift between .NET and TypeScript.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

---

## Dispatch table

| Consuming assembly            | Spec file                                     | Emitted source             | Emitted class                          |
| ----------------------------- | --------------------------------------------- | -------------------------- | -------------------------------------- |
| `DcsvIo.D2.I18n.Abstractions` | `contracts/tk-message/tk-message.spec.json`   | `TkMessageWireShape.g.cs`  | `DcsvIo.D2.I18n.TkMessageWireShape`    |
| `DcsvIo.D2.Result`            | `contracts/input-error/input-error.spec.json` | `InputErrorWireShape.g.cs` | `DcsvIo.D2.Result.InputErrorWireShape` |
| anything else                 | -                                             | - (no-op)                  | -                                      |

To add a wire-shape catalog:

1. Add a `DispatchEntry` to `sr_dispatch` in `WireShapesGenerator.cs`.
2. Author the spec and schema under `contracts/`.
3. Wire the consuming csproj's `<AdditionalFiles>` and `<ProjectReference OutputItemType="Analyzer">`.

---

## Diagnostic catalog

| ID        | Severity | Meaning                                                            |
| --------- | -------- | ------------------------------------------------------------------ |
| `D2WS001` | Error    | Spec file is malformed JSON or violates the schema.                |
| `D2WS002` | Error    | Two properties share the same `constName`.                         |
| `D2WS003` | Error    | Two properties share the same wire `value`.                        |
| `D2WS004` | Error    | Property `constName` does not match `^[A-Z][A-Z0-9_]*$`.           |
| `D2WS005` | Error    | No spec file passed to the analyzer for a target catalog assembly. |

---

## Cross-language parity

The TypeScript packages commit matching catalogs that they generate from the same
`contracts/tk-message/tk-message.spec.json` and `contracts/input-error/input-error.spec.json` files, so the
.NET-emitted constants and the TypeScript-emitted constants have byte-equal wire values for every property name.
