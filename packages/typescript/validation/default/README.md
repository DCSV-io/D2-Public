<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-validation

> **Audience**: backend Node/TypeScript engineers and BFF code that need to validate and normalize emails, phone
> numbers and postal codes against the same rules the .NET services enforce.

Default implementations of the email, phone and postal-code validator contracts declared in
`@dcsv-io/d2-validation-abstractions`. Mirrors `DcsvIo.D2.Validation` (.NET) with the same normalization rules and the
same per-field `D2Result<string>` contract, so a value accepted on one runtime is accepted on the other.

## Install

```bash
pnpm add @dcsv-io/d2-validation
```

## Validators

| Class                        | Contract               | Backing                                                                      | Success payload                    | Failure key (`TK.common.validation.*`) |
| ---------------------------- | ---------------------- | ---------------------------------------------------------------------------- | ---------------------------------- | -------------------------------------- |
| `DefaultEmailValidator`      | `IEmailValidator`      | `EMAIL_PATTERN` regex (case-insensitive)                                     | trimmed and lowercased address     | `EMAIL_INVALID`                        |
| `DefaultPhoneValidator`      | `IPhoneValidator`      | `libphonenumber-js`                                                          | E.164-formatted number             | `PHONE_INVALID`                        |
| `DefaultPostalCodeValidator` | `IPostalCodeValidator` | shared `contracts/validation/postal-code-regexes.json` per-country regex map | trimmed and uppercased postal code | `POSTAL_CODE_INVALID`                  |

Each `validate(...)` returns `D2Result<string>`:

- Success: `ok(normalized)`, carrying the normalized form.
- Failure: `validationFailed` with a single per-field `InputError` (field `"email"`, `"phone"` or `"postalCode"`)
  keyed with the `*_INVALID` translation key.

`undefined`, empty and whitespace-only input all produce the same `*_INVALID` failure. There is no separate
"required" key, because a missing value is treated as an invalid value.

## Singletons

The default implementations are stateless. The package entry point exports ready-to-use shared instances, so callers
do not re-allocate the compiled email regex at each call site:

```ts
import {
  emailValidator,
  phoneValidator,
  postalCodeValidator,
} from "@dcsv-io/d2-validation";
import { CountryCode } from "@dcsv-io/d2-geo-abstractions";

const email = emailValidator.validate("  Ada@Example.COM  ");
// ok("ada@example.com")

const phone = phoneValidator.validate("(202) 555-0143", CountryCode.US);
// ok("+12025550143")

const postal = postalCodeValidator.validate("sw1a 1aa", CountryCode.GB);
// ok("SW1A 1AA")
```

## Cross-language parity

- Email: `EMAIL_PATTERN` is the shared source of truth. A parity test asserts that the exported constant is
  byte-identical to the .NET `DefaultEmailValidator.EMAIL_PATTERN` const, so a change to one side fails the test until
  both match. The pattern enforces a total length of 1-254, a local part of 1-64 and at least one dot in the domain,
  and its character classes reject any non-ASCII codepoint.
- Phone: both runtimes delegate to a libphonenumber port (libphonenumber-js here, libphonenumber-csharp on .NET) and
  normalize to E.164, so the accept or reject decision and the normalized output match.
- Postal code: both runtimes compile the SAME per-country regex map from the single shared dataset
  `contracts/validation/postal-code-regexes.json`. This package imports the JSON directly, and the .NET side embeds
  it. The check is country-aware and structural. An unknown or absent country fails closed on both runtimes: the
  validator returns `POSTAL_CODE_INVALID` and never throws, and there is no permissive country-agnostic fallback. Both
  runtimes normalize (trim and uppercase) before matching, and compile each pattern case-insensitively (TS `"i"`
  flag, .NET `RegexOptions.IgnoreCase`). The dataset is ported from `postcode-validator@3.10.9` with one deliberate
  correction: `[A-z]` becomes `[A-Za-z]` in the `UK` and `GB` patterns, which fixes an upstream bug.
- Country mapping: `CountryCode` from `@dcsv-io/d2-geo-abstractions` is a branded alpha-2 string. Its runtime value is
  the 2-letter region identifier that `libphonenumber-js` expects and that the dataset uses as the key of its
  per-country regexes, so the bridge is a compile-time cast only.

## Version pins

`libphonenumber-js` is pinned to an exact version (no `^` or `~`). Host form layers should pin the same version so
that client and library validation stay identical. Libraries that ship metadata shift their accept or reject boundary
between minor releases, and an exact pin keeps that boundary stable across consumers and across the .NET parity
fixtures.

Postal-code validation reads the shared `contracts/validation/postal-code-regexes.json` dataset directly and does not
depend on the `postcode-validator` npm package. The provenance and version stamp (ported from
`postcode-validator@3.10.9`, with the `[A-z]` -> `[A-Za-z]` UK/GB correction) is in that file's `$comment`.

## Dependencies

- `@dcsv-io/d2-validation-abstractions`: the `IEmailValidator` / `IPhoneValidator` / `IPostalCodeValidator` contracts
  this package implements.
- `@dcsv-io/d2-geo-abstractions`: the branded `CountryCode` type that the phone and postal validators accept.
- `@dcsv-io/d2-result`: the `D2Result` envelope and the `ok` / `validationFailed` / `inputError` / `tk` factories. Every
  validator returns a `D2Result`.
- `@dcsv-io/d2-i18n-keys`: the `TK` key catalog that supplies the `*_INVALID` translation keys.
- `@dcsv-io/d2-utilities`: the `falsey` boundary helper for null, empty and whitespace input guards.
- `libphonenumber-js`: phone-number parsing and E.164 normalization (exact pin).

Postal-code validation has no runtime npm dependency: it imports the shared
`contracts/validation/postal-code-regexes.json` dataset directly (`resolveJsonModule`).
