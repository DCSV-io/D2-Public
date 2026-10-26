<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.ProtocolAudiences.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** `contracts/auth-protocol-audiences/protocol-audiences.spec.json`, supplied through
`<AdditionalFiles>`.

Roslyn incremental source generator that emits the `WellKnownAudiences` static partial class from the
protocol-audience spec. These are the audiences the protocol itself fixes - the self-audience a host
accepts on tokens minted for itself, and the universal internal receive audience every hop validates
against - as distinct from the service catalog
[`audiences-source-gen`](../audiences-source-gen/README.md) emits.

Convention: the generator follows the spec-driven Roslyn `IIncrementalGenerator` pattern - file layout, diagnostic id
convention, generator anatomy, `<AdditionalFiles>` wiring.

---

## Build-time diagnostics

| ID          | Severity | Trigger                                                                                 |
| ----------- | -------- | --------------------------------------------------------------------------------------- |
| `D2PAUD001` | Error    | Two entries share the same name, which would emit a duplicate `const string`.           |
| `D2PAUD002` | Error    | An entry name violates the C# identifier convention.                                    |
| `D2PAUD003` | Error    | Two entries share the same value, which would silently alias one audience to the other. |
| `D2PAUD004` | Error    | An entry carries an empty value.                                                        |
| `D2PAUD009` | Error    | The spec file is malformed JSON or violates the schema.                                 |
| `D2PAUD010` | Error    | No `protocol-audiences.spec.json` was found in `AdditionalFiles`.                       |

The spec has no `$schema` field: schema validation happens at edit time in the editor, and the
loader deserializes the data fields while the emitter validates them.

---

## Emitted shape

```csharp
public static partial class WellKnownAudiences
{
    /// <summary>One constant per spec entry, documented from the entry's description.</summary>
    public const string ExampleAudience = "https://example.internal";
}
```

---

## Validation ledger

| Emitter                      | Validated against                                                                                                                                                                                                                                                                              | Test double stands in for                                                                      | What retires the double              |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `ProtocolAudiencesEmitter`   | `ProtocolAudiencesEmitterTests` drives the real emitter over spec entries and asserts the emitted source and every diagnostic arm; `ProtocolAudiencesGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `WellKnownAudiencesTests` asserts the compiled output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `ProtocolAudienceSpecLoader` | Exercised through `ProtocolAudiencesGeneratorTests` over the malformed and missing-file arms.                                                                                                                                                                                                  | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |
| Diagnostics                  | `DiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                                                                                                                             | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |

---

## References

- [`audiences-source-gen/`](../audiences-source-gen/README.md) - the service-audience catalog.
- [`abstractions/`](../abstractions/README.md) - the consuming lib the generated class is emitted into.
