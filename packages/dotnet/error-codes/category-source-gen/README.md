<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ErrorCodes.Category.SourceGen

> Parent: [`packages/dotnet/error-codes/`](../README.md)

**Input contract:** `error-category.spec.json`, supplied through `<AdditionalFiles>`.

Roslyn incremental source generator that emits the closed `ErrorCategory` enum, its snake_case wire
mapping and the `ErrorCategoryJsonConverter` that serializes it. The enum and its wire strings come
from one spec file, so a category added on one side of the wire cannot drift from the other.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern - file layout, diagnostic id
convention, generator anatomy, `<AdditionalFiles>` wiring.

---

## Build-time diagnostics

| ID          | Severity | Trigger                                                    |
| ----------- | -------- | ---------------------------------------------------------- |
| `D2ECAT001` | Error    | The spec file could not be parsed, or violates the schema. |
| `D2ECAT002` | Error    | Two entries declare the same wire string.                  |
| `D2ECAT003` | Error    | A wire string is not snake_case.                           |
| `D2ECAT004` | Error    | An entry's documentation text is empty or whitespace-only. |

---

## Emitted shape

```csharp
public enum ErrorCategory { /* one member per spec entry */ }

public static class ErrorCategoryWire
{
    public static string ToWire(this ErrorCategory category);
    public static bool TryFromWire(string wire, out ErrorCategory category);
}

public sealed class ErrorCategoryJsonConverter : JsonConverter<ErrorCategory>;
```

Both directions are backed by a `FrozenDictionary`. The JSON converter is strict: an unknown wire
string throws `JsonException` rather than resolving to a default member, because a category silently
read as the wrong one changes how a failure is classified.

---

## Validation ledger

| Emitter                   | Validated against                                                                                                                                                                                                                                                                                                                                       | Test double stands in for                                                                      | What retires the double              |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `ErrorCategoryEmitter`    | `ErrorCategoryEmitterTests` runs the real emitter over spec entries and asserts the emitted source, including each diagnostic arm; `ErrorCategoryTests` asserts the compiled enum, both wire directions and the converter. There is no `GeneratorDriver` test for this generator; the emitter tests and the compiled-output tests provide the coverage. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `ErrorCategorySpecLoader` | Exercised through `ErrorCategoryEmitterTests` over the malformed and schema-violating shapes.                                                                                                                                                                                                                                                           | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |

---

## References

- [`category/`](../category/README.md) - the package that compiles the generated enum.
- [`registry-source-gen/`](../registry-source-gen/README.md) - checks every error code's category
  against this closed set.
