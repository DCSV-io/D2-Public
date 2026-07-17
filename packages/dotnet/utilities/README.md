<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities

Foundational helpers used at every boundary across D2. The "no value too small to centralize" library — preventing whole classes of bugs (empty-string-as-data, env-var collisions, JSON cycles) from ever entering domain code.

Runtime dependencies are kept minimal so this lib stays domain-safe:

- `dotenv.net` — only loaded when `D2Env.Load()` is called
- `JetBrains.Annotations` — compile-time markers
- `DcsvIo.D2.Result` + `DcsvIo.D2.I18n.Abstractions` — both zero-runtime-dep themselves; pulled in so `TryParseEmail` / `TryParsePhoneNumber` can return `D2Result<string>` with `TK.*`-keyed messages

## Install

```bash
dotnet add package DcsvIo.D2.Utilities
```

## Public API (by concern)

| Concern         | Covers                                                                                                                                                                                                                                                                                                           |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Attributes`    | `[RedactData]` marker attribute consumed by the Serilog destructuring policy.                                                                                                                                                                                                                                    |
| `Configuration` | `ConnectionStringHelper` (URI ↔ wire-format conversion for Redis / Postgres / RabbitMQ) + `D2Env` (host-side `.env*` file loader).                                                                                                                                                                               |
| `Diagnostics`   | `SanitizedExceptionRender` — PII-safe exception rendering for log + telemetry + DLQ-header surfaces.                                                                                                                                                                                                             |
| `Enums`         | `IsolationLevel` (SQL transaction isolation taxonomy) + `RedactReason` (closed enum used by `[RedactData]`).                                                                                                                                                                                                     |
| `Extensions`    | The most-used surface: `Truthy()` / `Falsey()` / `ToNullIfEmpty()` boundary checks; `ThrowIfFalsey()` required-argument guards (BCL-split null vs falsey); `TryParseTruthyNull` Guid + enum parsers; `CleanStr` / `CleanDisplayStr` display cleaners; `TryParseEmail` / `TryParsePhoneNumber` `D2Result`-returning validators; `GetNormalizedStrForHashing`; `NormalizeForHash` (single-value cross-script hash canonicalization); `Clean()` for enumerables. |
| `Serialization` | `SerializerOptions` — frozen `JsonSerializerOptions` presets (`SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`).                                                                                                                                                                                                  |

## Adversarial coverage notes

Public surfaces are covered adversarially across every boundary helper, including:

- All `Truthy`/`Falsey` overloads — null, empty, whitespace-only, multi-element, boundary values.
- `ToNullIfEmpty` — null/empty/whitespace/trim/identity paths.
- `CleanStr` / `CleanDisplayStr` — Unicode multi-script preservation, allowed-char retention, all-stripped → null.
- `TryParseEmail` / `TryParsePhoneNumber` — happy paths + documented failure conditions; asserts `D2Result.IsValidationFailed` and the carried `TK.Common.Validation.EMAIL_INVALID` / `PHONE_INVALID` key.
- `GuidExtensions.TryParseTruthyNull` / `EnumExtensions.TryParseTruthyNull<TEnum>` — happy path + adversarial coverage.
- `GetNormalizedStrForHashing` / `NormalizeForHash` — empty, multi-script, idempotency, determinism.
- `EnumerableExtensions.Clean` — empty-behavior × value-null-behavior matrix.
- `RedactDataAttribute` — defaults, init-only setters, reflective discovery.
- `SerializerOptions` — camelCase, string-enums, null preservation/omission, cycle tolerance.
- `ConnectionStringHelper` / `D2Env` — URI conversion, env-var resolution, discovery precedence.
- `SanitizedExceptionRender` — type-name fallback, first-frame format, anti-leak invariants (rendered strings never contain `Exception.Message`).
