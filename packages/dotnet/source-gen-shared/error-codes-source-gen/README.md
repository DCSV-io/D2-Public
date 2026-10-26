<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Result.ErrorCodes.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/error-codes/`](../../../../contracts/error-codes/README.md)

A `[Generator]` shell over the shared unified error-codes engine
([`error-codes-emit`](../error-codes-emit/README.md)). It reads `contracts/error-codes/error-codes.spec.json` through
`<AdditionalFiles>` and emits the `ErrorCodes` const-string catalog, the constructing semantic failure factories and the
per-code booleans into `DcsvIo.D2.Result`. It is single-target: it emits only when the consuming assembly is
`DcsvIo.D2.Result`. The shell defines only the generic catalog identity (assembly name and the `ErrorCodesGenerator`
type fully qualified name) and its `CatalogConfig`; all generation logic is in the shared engine.

The spec file is the single source of truth for the platform's generic error-code taxonomy. The spec generates:

- every `d2_error_code` constant that a `D2Result` failure can have;
- every constructing semantic failure factory on `D2Result` / `D2Result<TData>` (for example `NotFound`,
  `ValidationFailed`);
- every per-code boolean discriminator (for example `IsNotFound`, `IsConflict`).

The same spec drives the TypeScript `@dcsv-io/d2-result` `ErrorCodes` catalog and factories (sources committed), so the
.NET and TypeScript wire formats cannot drift apart.

The generic catalog uses the reserved `GEN_*` DomainPrefix (`GEN_NOT_FOUND`, `GEN_CONFLICT`, ...) and runs in the
engine's `FactoryHost.Base` mode. In that mode the factories are the base: they construct a `D2Result` directly and are
members of the `D2Result` / `D2Result<TData>` partial classes, not of a separate `<Domain>Failures` class. Per-domain
catalogs, such as the public auth gate `LIB_AUTH_*` taxonomy at `contracts/auth-error-codes/` that
a host-supplied generator drives, run in `FactoryHost.Domain` mode. Their factories delegate to the base
factory that the `httpStatus` selects and live in a separate `<Domain>Failures` class (and `<Domain>Failures<T>`). Every
code starts with its catalog's owner prefix (`GEN_` / `LIB_{DOMAIN}_` / `DOM_{DOMAIN}_`). One engine drives every
catalog through its `CatalogConfig`, which requires `DomainPrefix`, `Owner` and `DomainToken`.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`). See
[`error-codes-emit`](../error-codes-emit/README.md) for the shared engine and the steps to add a catalog.

---

## Build-time diagnostics

| ID        | Severity | Trigger                                                                                                                                                                                                       |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2EC001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                                                                            |
| `D2EC002` | Error    | Two entries share the same `code`                                                                                                                                                                             |
| `D2EC003` | Error    | Entry's `httpStatus` is not in the supported set (`200` / `206` / `207` / `400` / `401` / `403` / `404` / `409` / `413` / `429` / `500` / `503`); adding a status requires updating the codegen mapping       |
| `D2EC004` | Error    | Entry's `code` is empty or does not match `^[A-Z][A-Z0-9_]*$`                                                                                                                                                 |
| `D2EC005` | Error    | Entry's `doc` summary text is missing or whitespace-only                                                                                                                                                      |

The shared engine's `D2ERC001` enforces each catalog's full `DomainPrefix`; for this shell that is `GEN_`. `D2ERC002`
applies because the catalog has factories: every `userMessageKey` is inverse-transformed (for example
`TK.Common.Errors.NOT_FOUND` -> `common_errors_NOT_FOUND`) and checked against `contracts/messages/en-US.json`, which
the project passes through `<AdditionalFiles>`. `D2ERC003` (unsupported delegating `factoryShape`) never fires here,
because `FactoryHost.Base` implements the universal `standard` shape and `none`.

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "errorCodes": [
    {
      "code": "GEN_NOT_FOUND",
      "httpStatus": 404,
      "category": "not_found",
      "userMessageKey": "TK.Common.Errors.NOT_FOUND",
      "factoryName": "NotFound",
      "factoryShape": "standard",
      "doc": "Indicates that the requested resource was not found."
    },
    {
      "code": "GEN_SERVICE_UNAVAILABLE",
      "httpStatus": 503,
      "category": "infrastructure_unavailable",
      "userMessageKey": "TK.Common.Errors.SERVICE_UNAVAILABLE",
      "factoryName": "ServiceUnavailable",
      "factoryShape": "standard",
      "doc": "Indicates that the service is currently unavailable."
    }
  ]
}
```

### Field rules

- `code` - wire format `^[A-Z][A-Z0-9_]*$`. Unique. The emitted constant has this value, and the literal is the wire
  format.
- `httpStatus` - one of the values in the supported set (see `D2EC003`). The JSON Schema `enum` mirrors the codegen
  matrix, so changing the set means editing both.
- `category` - a closed semantic and telemetry classification, sent on the wire for class-based handling by consumers.
  It includes `partial_success`, which classifies a mixed per-target outcome: `GEN_SOME_FOUND` uses it on 206 and
  `GEN_PARTIAL_SUCCESS` uses it on 503, and both have `Success` false.
- `userMessageKey` - TK symbol-path reference (for example `TK.Common.Errors.NOT_FOUND`), used as the factory's default
  message. `D2ERC002` checks it against `en-US.json`. The `code` and the `userMessageKey` constant may differ (for
  example `UNHANDLED_EXCEPTION` -> `TK.Common.Errors.UNKNOWN`; `RATE_LIMITED` -> `TK.Common.Errors.TOO_MANY_REQUESTS`).
- `factoryName` - PascalCase factory method name (for example `NotFound`, `TooManyRequests`). It may differ from `code`.
- `factoryShape` - `standard`, the one universal error-factory shape
  (`messages?, inputErrors?, errorCode?, category?, traceId?`, all optional), or `none` (constant and boolean only, no
  factory). It determines the factory signature.
- `doc` - XML `<summary>` text on the emitted constant and factory, and JSDoc on the constant that the TypeScript side
  emits.

---

## Emitted output

The generator emits these `.g.cs` files into the consuming assembly (`DcsvIo.D2.Result`):

- `ErrorCodes.g.cs` - the `DcsvIo.D2.Result.ErrorCodes` static class, with one `public const string` per spec entry,
  `IReadOnlyList<string> AllCodes` and `int GetHttpStatus(string)`.
- `D2Result.Factories.g.cs` - the constructing non-generic semantic failure factories on `partial class D2Result`, one
  per `factoryShape != none` entry.
- `D2Result.Generic.Factories.g.cs` - the `<TData>` typed twins on `partial class D2Result<TData>`, which carry `new`
  and `default` data.
- `D2Result.Booleans.g.cs` - the per-error-code boolean discriminators on `partial class D2Result`, one per
  boolean-keyed code (the selection is in `BaseFactoriesEmitter.cs`). The serialization codes `COULD_NOT_BE_*` have no
  boolean.

---

## Reference

- [`error-codes-emit`](../error-codes-emit/README.md) - the shared unified engine this shell drives
- [`contracts/error-codes/schema.json`](../../../../contracts/error-codes/schema.json) - JSON Schema for the spec
- [`contracts/error-codes/error-codes.spec.json`](../../../../contracts/error-codes/error-codes.spec.json) - the
  source-of-truth catalog
- `@dcsv-io/d2-result` - TypeScript catalog that consumes the same spec
