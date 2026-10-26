<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Telemetry.Tags.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/telemetry/`](../../../../contracts/telemetry/README.md)

A Roslyn incremental source generator that emits a typed-constants class `*TelemetryTags.g.cs` for each meter. It reads
`contracts/telemetry/telemetry.spec.json` through `<AdditionalFiles>`. Dispatch is single-target per meter: the
generator emits only when the consuming assembly matches the meter's `consumingAssembly` field.

The spec file is the single source of truth for the platform's OTel meter, instrument and tag enumeration. Every
closed-enum tag value that a runtime counter call site emits is anchored to a generated constant, so a call site that
uses the constant cannot differ from the spec. The spec is plain JSON with no .NET-specific format, so a generator in
another language can read it.

Untagged instruments and instruments with open-enum tags (for example handler-name) are listed in the spec to document
them but produce no generated code.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern, covering file layout, diagnostic ID convention,
generator anatomy and `<AdditionalFiles>` wiring.

---

## Cross-spec resolution (`valuesFromSpec`)

When a tag declares `"valuesFromSpec": "auth-error-codes"` instead of an inline `"values": [...]` array, the generator
resolves the value set from a sibling spec passed through `<AdditionalFiles>`. Only `auth-error-codes.spec.json` is
supported. A consuming csproj that uses a meter with a `valuesFromSpec` tag must add both `telemetry.spec.json` and the
referenced sibling spec to `<AdditionalFiles>`. A missing, malformed or unknown sibling spec makes the generator report
`D2TEL006`, and a missing `telemetry.spec.json` makes it emit nothing. This link makes a closed-enum tag take its values
from the catalog that owns them (for example, the `d2.auth.problem.emitted` tag lists exactly the codes in
`auth-error-codes.spec.json`).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                                              |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2TEL001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                                   |
| `D2TEL002` | Error    | Duplicate `meter` name across the spec                                                                                                                               |
| `D2TEL003` | Error    | Duplicate `instruments[].name` within a single meter                                                                                                                 |
| `D2TEL004` | Error    | Unknown `kind` value (must be `counter` / `histogram` / `gauge`)                                                                                                     |
| `D2TEL005` | Error    | Duplicate value within a single tag's `values` array                                                                                                                 |
| `D2TEL006` | Error    | Cross-spec reference (e.g. `valuesFromSpec=auth-error-codes`) cannot be resolved - sibling spec missing from `AdditionalFiles`, malformed, or unknown reference name |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "meters": [
    {
      "meter": "DcsvIo.D2.Auth",
      "consumingAssembly": "DcsvIo.D2.Auth",
      "tagsNamespace": "DcsvIo.D2.Auth.Telemetry",
      "tagsClassName": "AuthTelemetryTags",
      "instruments": [
        {
          "name": "d2.auth.jwt.validations",
          "constName": "JwtValidations",
          "kind": "counter",
          "description": "Total inbound JWT validations.",
          "tags": [
            { "name": "outcome", "values": ["success", "expired", "..."] }
          ]
        },
        {
          "name": "d2.auth.problem.emitted",
          "constName": "ProblemEmitted",
          "kind": "counter",
          "description": "Total auth-failure responses emitted.",
          "tags": [
            { "name": "d2_error_code", "valuesFromSpec": "auth-error-codes" }
          ]
        }
      ]
    }
  ]
}
```

### Field rules

- `meter` - OTel meter name. Unique across the spec.
- `consumingAssembly` - .NET assembly name. The generator emits only into the compilation with this assembly name.
- `tagsNamespace` / `tagsClassName` - optional overrides for the namespace and the name of the emitted class.
- `instruments[].kind` - closed enum `counter` / `histogram` / `gauge`.
- `instruments[].tags[]` - the generator emits nested typed-constants classes from these. An instrument with no tags
  gets no emitted class and is a documentation-only spec entry.
- `tags[].values` or `tags[].valuesFromSpec` - exactly one is required. `valuesFromSpec` resolves the values from
  another spec at codegen time (only `"auth-error-codes"` is supported).

---

## Emitted output

The generator emits one `*TelemetryTags.g.cs` file per meter into the consuming assembly. Example shape:

```csharp
public static class AuthTelemetryTags
{
    public static class JwtValidations
    {
        public const string TAG_OUTCOME = "outcome";

        public static class Outcome
        {
            public const string SUCCESS = "success";
            public const string BEARER_MISSING = "bearer_missing";
            // ...
        }
    }

    public static class ProblemEmitted
    {
        public const string TAG_D2_ERROR_CODE = "d2_error_code";
        // No nested class - d2_error_code uses valuesFromSpec=auth-error-codes;
        // consumers reference AuthErrorCodes.LIB_AUTH_* directly.
    }
}
```

---

## Reference

- [`contracts/telemetry/schema.json`](../../../../contracts/telemetry/schema.json) - JSON Schema for the spec
- [`contracts/telemetry/telemetry.spec.json`](../../../../contracts/telemetry/telemetry.spec.json) - the source-of-truth
  catalog
- [`DcsvIo.D2.Auth.Scopes.SourceGen`](../../auth/scopes-source-gen/README.md) - a sibling generator that uses the same
  incremental-generator and diagnostic-split pattern

---

## Validation ledger

| Emitter                | Validated against                                                                                                                                                                                      | Test double stands in for                                                                      | What retires the double              |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `TelemetryTagsEmitter` | `TelemetryTagsEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `TelemetryTagsGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `TelemetrySpecLoader`  | `TelemetrySpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                              | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `CrossSpecResolver`    | `CrossSpecResolverTests` drives the real resolver over the cross-spec references it resolves and the ones it rejects.                                                                                  | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics            | `TelemetryDiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                            | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
