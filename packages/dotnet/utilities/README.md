<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities

Base helpers used at every boundary across D2: `Truthy()` / `Falsey()` checks that treat empty strings and
collections as absent, string cleaning and parse extensions, connection-string conversion, the `[RedactData]`
attribute, sanitized exception rendering, and `JsonSerializerOptions` presets.

Other D2 packages depend on this one, so it has no runtime NuGet package at all:

- `JetBrains.Annotations` - compile-time markers
- `DcsvIo.D2.Result` + `DcsvIo.D2.I18n.Abstractions` - both zero-runtime-dep themselves; pulled in so `TryParseEmail` /
  `TryParsePhoneNumber` can return `D2Result<string>` with `TK.*`-keyed messages

The `.env*` file reader ships from `DcsvIo.D2.EnvFiles`, which declares the `dotenv.net` reference it needs.

## Install

```bash
dotnet add package DcsvIo.D2.Utilities
```

## Public API (by concern)

| Concern         | Covers                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Attributes`    | `[RedactData]` marker attribute consumed by the Serilog destructuring policy.                                                                                                                                                                                                                                                                                                                                                                                 |
| `Configuration` | `ConnectionStringHelper` (URI <-> wire-format conversion for Redis / Postgres / RabbitMQ) + `TimeSpanRangeBounds` (named `[Range]` bound strings for `TimeSpan` options properties).                                                                                                                                                                                                                                                                          |
| `Diagnostics`   | `SanitizedExceptionRender` - PII-safe exception rendering for log + telemetry + DLQ-header surfaces.                                                                                                                                                                                                                                                                                                                                                          |
| `Enums`         | `IsolationLevel` (SQL transaction isolation taxonomy) + `RedactReason` (closed enum used by `[RedactData]`).                                                                                                                                                                                                                                                                                                                                                  |
| `Extensions`    | The most-used surface: `Truthy()` / `Falsey()` / `ToNullIfEmpty()` boundary checks; `ThrowIfFalsey()` required-argument guards (BCL-split null vs falsey); `TryParseTruthyNull` Guid + enum parsers; `CleanStr` / `CleanDisplayStr` display cleaners; `TryParseEmail` / `TryParsePhoneNumber` `D2Result`-returning validators; `GetNormalizedStrForHashing`; `NormalizeForHash` (single-value cross-script hash canonicalization); `Clean()` for enumerables. |
| `Serialization` | `SerializerOptions` - frozen `JsonSerializerOptions` presets (`SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`).                                                                                                                                                                                                                                                                                                                                               |

## Adversarial coverage notes

The tests cover the public surfaces adversarially for every boundary helper, including:

- All `Truthy`/`Falsey` overloads - null, empty, whitespace-only, multi-element, boundary values.
- `ToNullIfEmpty` - null/empty/whitespace/trim/identity paths.
- `CleanStr` / `CleanDisplayStr` - Unicode multi-script preservation, allowed-char retention, all-stripped -> null.
- `TryParseEmail` / `TryParsePhoneNumber` - happy paths + documented failure conditions; asserts
  `D2Result.IsValidationFailed` and the `TK.Common.Validation.EMAIL_INVALID` / `PHONE_INVALID` key in the result.
- `GuidExtensions.TryParseTruthyNull` / `EnumExtensions.TryParseTruthyNull<TEnum>` - happy path + adversarial coverage.
- `GetNormalizedStrForHashing` / `NormalizeForHash` - empty, multi-script, idempotency, determinism.
- `EnumerableExtensions.Clean` - empty-behavior x value-null-behavior matrix.
- `RedactDataAttribute` - defaults, init-only setters, reflective discovery.
- `SerializerOptions` - camelCase, string-enums, null preservation/omission, cycle tolerance.
- `ConnectionStringHelper` - URI conversion and env-var resolution.
- `TimeSpanRangeBounds` - `NO_CEILING` parses to `TimeSpan.MaxValue`; a floor-only `[Range]` built from it admits
  `TimeSpan.MaxValue` and still rejects a value under its floor.
- `SanitizedExceptionRender` - type-name fallback, first-frame format, anti-leak invariants (rendered strings never
  contain `Exception.Message`).
