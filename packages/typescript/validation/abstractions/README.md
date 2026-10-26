<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-validation-abstractions

> **Audience**: backend Node/TypeScript service and BFF engineers who need the validator contract interfaces
> (email, phone and postal code) or the shared field-constraints catalog (field-length bounds and the name and sex
> taxonomy enums), without the default implementations in `@dcsv-io/d2-validation`.

Hand-written validator contract interfaces, plus the shared field-constraints catalog that codegen emits
(field-length and digit-count constants and closed-list taxonomy enums). Mirrors
`DcsvIo.D2.Validation.Abstractions` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-validation-abstractions
```

## Overview

The validation layer ships in these TS packages:

- `@dcsv-io/d2-validation-abstractions`: this package. It contains the validator contract interfaces
  (`IEmailValidator`, `IPhoneValidator`, `IPostalCodeValidator`) and the `FieldConstraints` bounds and
  `NamePrefix` / `NameSuffix` / `BiologicalSex` taxonomy enums that codegen emits, with their Zod schemas. The
  interfaces are pure types with near-zero runtime payload. The emitted catalog contains the const objects and
  Zod schemas, so the package has a small runtime dependency on `zod`.
- `@dcsv-io/d2-validation`: the default implementations, backed by the standard normalization rules. It depends on
  this package.

Domain code that depends on a validator imports the interface from `@dcsv-io/d2-validation-abstractions`. Only
composition-root code wires the concrete implementation from `@dcsv-io/d2-validation`. Code that needs the shared
field bounds or taxonomy enums imports `FieldConstraints`, `NamePrefix` and the other exports directly.

## Field-constraints catalog (codegen-emitted)

Codegen generates this catalog from `contracts/validation/field-constraints.spec.json` and emits it into
`src/generated/` (committed, `linguist-generated`). The same spec drives the .NET
`DcsvIo.D2.Validation.Abstractions` catalog, so cross-language drift between the .NET catalog and this one cannot occur
while both are generated from it.

- `FieldConstraints` (`field-constraints.g.ts`): a plain numeric `as const` object holding the field-length and
  digit-count bounds listed in that file, plus the derived `FieldConstraint` value type. It has the same shape as
  geo's numeric `GeopoliticalEntityType`: the values are integers, not a closed-set wire vocabulary that needs a
  brand. Use these bounds to gate Zod schemas and form validation against the same bounds the .NET `Create(...)`
  gates enforce.
- `NamePrefix` / `NameSuffix` / `BiologicalSex` (`taxonomy.g.ts`): each closed-list enum has a string-valued
  `as const` object (the member name is the wire form), a branded derived type, a `z.enum([...])` schema
  (`*Schema`) and an `ALL_*_SET` `ReadonlySet<string>` membership set. The schemas gate BFF and client input against
  the same closed vocabularies that the .NET enums encode.

The catalog contains no localized display strings, and its member names are adequate for display. Frontend labels use
i18n `TK.*` keys if a picker needs them.

## Public surface

| Export                                                                    | Source file                                 | Purpose                                                                                                         |
| ------------------------------------------------------------------------- | ------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `IEmailValidator` (interface)                                             | `src/interfaces/i-email-validator.ts`       | Validates an email; returns the normalized (trimmed and lowercased) address on success.                         |
| `IPhoneValidator` (interface)                                             | `src/interfaces/i-phone-validator.ts`       | Validates a phone number; returns the normalized E.164 form on success.                                         |
| `IPostalCodeValidator` (interface)                                        | `src/interfaces/i-postal-code-validator.ts` | Country-aware postal-code validation; returns the normalized (trimmed and uppercased) code on success.          |
| `FieldConstraints` + `FieldConstraint`                                    | `src/generated/field-constraints.g.ts`      | Codegen-emitted numeric `as const` field-length and digit-count bounds, plus the derived value type.            |
| `NamePrefix` / `NameSuffix` / `BiologicalSex` (+ `*Schema` + `ALL_*_SET`) | `src/generated/taxonomy.g.ts`               | Codegen-emitted closed-list taxonomy enums: const object, branded type, Zod `z.enum` schema and membership set. |

## Normalized return contract

Every validator exposes a single `validate(...)` method returning
`D2Result<string>`:

- Success: an `ok` `D2Result` whose data is the normalized value:
 - `IEmailValidator` -> trimmed and lowercased email.
 - `IPhoneValidator` -> E.164 representation.
 - `IPostalCodeValidator` -> trimmed and uppercased postal code.
- Failure: a `validationFailed` `D2Result` carrying a single per-field `InputError`. The field key is `"email"`,
  `"phone"` or `"postalCode"` respectively. Failure covers `undefined`, empty, whitespace-only and structurally
  invalid input.

Returning the normalized value (rather than a bare boolean) lets callers
persist the canonical form directly without a second normalization pass.

## Parity with .NET

Mirrors `DcsvIo.D2.Validation.Abstractions`:

- `IEmailValidator` <-> `DcsvIo.D2.Validation.Abstractions.IEmailValidator`.
- `IPhoneValidator` <-> `DcsvIo.D2.Validation.Abstractions.IPhoneValidator`.
- `IPostalCodeValidator` <->
 `DcsvIo.D2.Validation.Abstractions.IPostalCodeValidator`.

Each interface exposes the same single `validate(...)` method returning
`D2Result<string>` with the same normalization semantics and the same
per-field `InputError` field keys across both runtimes.

Optional parameters use `undefined` (not `null`) per workspace TS
convention. `null` arriving from the .NET wire normalizes to `undefined` at
the deserialization boundary.

## Dependencies

- `@dcsv-io/d2-result`: the `D2Result<string>` return type.
- `@dcsv-io/d2-geo-abstractions`: `CountryCode`, used for the phone default-region and postal-code country
  parameters.
- `zod`: the emitted taxonomy `*Schema` exports are `z.enum([...])` schemas. The version is pinned to the one
  `@dcsv-io/d2-geo-abstractions` uses. The validator interfaces themselves contain no runtime code; the dependency
  belongs to the catalog.

## Telemetry

No telemetry surface. The package emits no spans or metrics. Consumers instrument the validator call sites in their
own OTel setup.

## Configuration

No configuration. The contracts contain no tunable behavior.
