<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ErrorCodes.Registry.SourceGen

> Parent: [`packages/dotnet/error-codes/`](../README.md)

**Input contracts:** every `*-error-codes.spec.json` catalog supplied through `<AdditionalFiles>`, plus
`error-category.spec.json` for the closed category set.

Roslyn incremental source generator that merges every error-code catalog it is given into one
`ErrorCodeRegistry` lookup and the `ErrorCodeInfo` record that contains a code's full metadata. The
merge is where cross-catalog collisions and reserved-namespace violations are caught, so a code that
two catalogs both declare fails the build rather than resolving to whichever entry loaded last.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern - file layout, diagnostic id
convention, generator anatomy, `<AdditionalFiles>` wiring.

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                             |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `D2ERC004` | Error    | Two catalogs declare the same error `code` string - a cross-catalog collision. No registry is emitted.              |
| `D2ERC005` | Error    | Reserved-namespace violation: an unprefixed code in a per-domain spec, or a prefixed code in the generic spec.      |
| `D2ERC006` | Error    | Malformed registry spec, or an entry missing one of `category` / `userMessageKey` / `factoryName` / `factoryShape`. |
| `D2ERC007` | Error    | A code's `category` wire string is not a member of the closed set declared in `error-category.spec.json`.           |
| `D2ERC008` | Error    | An error-code catalog's filename is in no catalog identity map, so its owner, domain and prefix are undeclared.     |

---

## Emitted shape

```csharp
public readonly record struct ErrorCodeInfo(
    string Code,
    int HttpStatus,
    ErrorCategory Category,
    TKMessage UserMessageKey,
    string FactoryName,
    string FactoryShape,
    string Doc,
    string Domain,
    string Owner,
    bool IsDeprecated);

public static class ErrorCodeRegistry
{
    public static bool TryResolve(string code, out ErrorCodeInfo info);
    public static ErrorCodeInfo? Resolve(string code);
    public static IReadOnlyList<ErrorCodeInfo> All { get; }
}
```

The lookup is backed by a `FrozenDictionary`, ordinal and case-sensitive. An unknown code makes
`TryResolve` return `false` and `Resolve` return `null`, rather than throwing, so a consumer reading a code minted by a
newer deployment degrades instead of failing.

`RegistryEmitConfig` sets the namespace, the banner and the message-key class the emitter writes.

---

## Validation ledger

| Emitter                    | Validated against                                                                                                                                                                                                                                                        | Test double stands in for                                                                      | What retires the double              |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `RegistryEmitter`          | `RegistryEmitterTests` runs the real emitter over registry entries and asserts the emitted source; `RegistryGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `ErrorCodeRegistryTests` asserts the compiled `ErrorCodeRegistry` output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `RegistrySpecLoader`       | `RegistrySpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                                                                                                 | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |
| `RegistryCollisionChecker` | `RegistryCollisionCheckerTests` runs the real checker over colliding and non-colliding catalog sets; `RegistryOwnerPrefixDriftTests` runs it over the reserved-namespace arms.                                                                                           | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |
| Source fence               | `PublicRegistrySourceGenFenceTests` asserts that the emit configuration, the catalog identity map and the package source contain only the catalogs, package ids and message-key class this package declares.                                                             | Nothing. No collaborator is doubled.                                                           | Not applicable - there is no double. |

---

## References

- [`category-source-gen/`](../category-source-gen/README.md) - the closed `ErrorCategory` set this
  generator validates every entry against.
- [`registry/`](../registry/README.md) - the package that compiles the merged registry.
