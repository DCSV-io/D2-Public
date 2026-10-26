<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ProblemDetails.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/problem-details/`](../../../../contracts/problem-details/README.md)

Roslyn incremental source generator that emits the static class `DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys`, which
contains the RFC 7807 ProblemDetails wire-format catalog - `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*`
extension-key constants, `TITLE_*` per-HTTP-status title constants, and the `TitleFor` switch - into
[`DcsvIo.D2.ProblemDetails.Abstractions`](../abstractions/README.md) by reading
`contracts/problem-details/problem-details.spec.json` via `<AdditionalFiles>`. It has a single target: it emits only
when the consuming assembly is `DcsvIo.D2.ProblemDetails.Abstractions`.

The spec file is the single source of truth for the RFC 7807 wire shape emitted by every .NET ProblemDetails site:

- Path A: the host-supplied auth middleware, in its method that builds the ProblemDetails body
- Path B: `DcsvIo.D2.AspNetCore.Internal.D2ProblemDetailsCustomizer.Apply`, in the ASP.NET Core
  `IProblemDetailsService` pipeline

The same spec drives the TS-side `@dcsv-io/d2-problem-details-abstractions` catalog (sources committed), so the .NET and
TypeScript catalogs cannot differ in the URI prefix, content type, extension keys or per-status titles.

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                         |
| ---------- | -------- | --------------------------------------------------------------------------------------------------------------- |
| `D2PRB001` | Error    | Spec file is malformed JSON or violates the schema                                                              |
| `D2PRB002` | Error    | Two extension keys share the same `constName`                                                                   |
| `D2PRB003` | Error    | Two extension keys share the same wire `value`                                                                  |
| `D2PRB004` | Error    | Two titles share the same `constName`                                                                           |
| `D2PRB005` | Error    | Two titles share the same `httpStatus` (only one entry may map to each status; `null` is the singular fallback) |
| `D2PRB006` | Error    | `typeUriPrefix` does not end with a trailing slash (runtime appends the kebab-cased error code directly)        |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "typeUriPrefix": "https://problems.d2.dcsv.io/",
  "contentType": "application/problem+json",
  "extensionKeys": [
    {
      "constName": "ERROR_CODE",
      "value": "d2_error_code",
      "doc": "The extension key carrying the machine-readable error code."
    }
  ],
  "titles": [
    {
      "constName": "UNAUTHORIZED",
      "httpStatus": 401,
      "value": "Unauthorized",
      "doc": "The closed-enum coarse Title for 401 responses."
    },
    {
      "constName": "REQUEST_FAILED",
      "httpStatus": null,
      "value": "Request Failed",
      "doc": "The fallback Title used when no httpStatus-specific entry matches."
    }
  ]
}
```

### Field rules

- **`typeUriPrefix`** - base URI for the RFC 7807 `type` field. Runtime appends the kebab-cased error code directly, so
  the value must end with a trailing slash (codegen validates it via `D2PRB006`).
- **`contentType`** - MIME type per RFC 7807 section 6.1 for responses carrying a ProblemDetails body (e.g.
  `application/problem+json`). The host's auth middleware reads it when it writes a ProblemDetails response, so the .NET
  wire Content-Type is spec-driven (no hand-rolled literal).
- **`extensionKeys[].constName`** - UPPER_SNAKE_CASE. Unique. Becomes the public field name with the `EXTENSION_` prefix
  (e.g. `ERROR_CODE` -> `EXTENSION_ERROR_CODE`).
- **`extensionKeys[].value`** - wire-format extension-key string emitted on the JSON body. Unique. The literal is the
  wire format.
- **`titles[].constName`** - UPPER_SNAKE_CASE. Unique. Becomes the public field name with the `TITLE_` prefix
  (e.g. `UNAUTHORIZED` -> `TITLE_UNAUTHORIZED`).
- **`titles[].httpStatus`** - integer status (e.g. 401, 503) or `null` for the fallback entry. Unique across the catalog
  (one row per status; exactly one row may carry `null`).
- **`titles[].value`** - wire-format coarse Title (locale-neutral English from a closed enumeration; the client does
  locale-aware translation through the `d2_messages` extension).

---

## Emitted output

One `.g.cs` file emitted into the consuming assembly (`DcsvIo.D2.ProblemDetails.Abstractions`):

**`D2ProblemDetailsKeys.g.cs`** - `DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys` static class with:

- One `public const string TYPE_URI_PREFIX` declaration.
- One `public const string CONTENT_TYPE` declaration.
- One `public const string EXTENSION_*` per extension-key entry.
- One `public const string TITLE_*` per title entry.
- One `public static string TitleFor(HttpStatusCode statusCode)` switch helper.

The abstractions csproj is referenced by host auth middleware (path A) and public `DcsvIo.D2.AspNetCore` (path B
Customizer); both emit paths use the same emitted constant set.

---

## Reference

- [`contracts/problem-details/schema.json`](../../../../contracts/problem-details/schema.json) - JSON Schema for the
  spec
- [`contracts/problem-details/problem-details.spec.json`](../../../../contracts/problem-details/problem-details.spec.json) -
  the source-of-truth catalog
- [`DcsvIo.D2.ProblemDetails.Abstractions`](../abstractions/README.md) - the consuming csproj (single emit target)
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) - Problem Details for HTTP APIs

---

## Validation ledger

| Emitter                    | Validated against                                                                                                                                                                                        | Test double stands in for                                                                      | What retires the double              |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `ProblemDetailsEmitter`    | `ProblemDetailsEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `ProblemDetailsGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `ProblemDetailsSpecLoader` | `ProblemDetailsSpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                           | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics                | `ProblemDetailsDiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                         | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
