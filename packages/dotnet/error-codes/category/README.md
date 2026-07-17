<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ErrorCodes.Category

Foundational zero-dependency (BCL-only) leaf that exposes the closed `ErrorCategory` classification — the nine-value semantic/telemetry class every `D2Result` and every error code carries. Source-generated from the error-category contract spec. Lives here so result-core and the error-codes registry can reference `ErrorCategory` downward without pulling the merged-catalog generator.

The category is the producer's coarse signal — `infrastructure_unavailable`, `not_found`, `validation_failure` — so a consumer can do generic class-based handling (retry any `InfrastructureUnavailable`, surface any `NotFound` as a 404) without importing the producer's error-code catalog.

## Install

```bash
dotnet add package DcsvIo.D2.ErrorCodes.Category
```

## Public API

| Export                       | Purpose                                                                                                                                                              |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ErrorCategory`              | Closed enum — one member per category (e.g. `ErrorCategory.NotFound`). Carries `[JsonConverter(typeof(ErrorCategoryJsonConverter))]` so it serializes as its wire string. |
| `ErrorCategoryWire.ToWire`   | `this ErrorCategory → string` — the canonical snake_case wire string (e.g. `ErrorCategory.ValidationFailure → "validation_failure"`). Throws on an undefined member.   |
| `ErrorCategoryWire.TryFromWire` | `string → bool + out ErrorCategory` — parses a wire string back to the enum. Returns `false` for any unknown string (never throws).                                |
| `ErrorCategoryJsonConverter` | `JsonConverter<ErrorCategory>` — reads/writes the snake_case wire string; unknown wire strings throw `JsonException` (strict policy).                                  |

```csharp
ErrorCategory.NotFound.ToWire();                       // "not_found"
ErrorCategoryWire.TryFromWire("conflict", out var c);  // true, c == ErrorCategory.Conflict
JsonSerializer.Serialize(ErrorCategory.RateLimited);   // "rate_limited"
```

## Dependency edge

```
DcsvIo.D2.ErrorCodes.Category   (BCL-only — no project references)
                ▲
                ├── DcsvIo.D2.ErrorCodes.Registry   (references ErrorCategory downward)
                └── DcsvIo.D2.Result                (D2Result.Category is a typed ErrorCategory?)
```

`ErrorCategory.g.cs` is emitted by the sibling `DcsvIo.D2.ErrorCodes.Category.SourceGen` analyzer (referenced build-only, no runtime dll). The registry references this leaf instead of emitting the enum itself, and result-core references it so `D2Result.Category` is a typed `ErrorCategory?` — both arrows point downward into this zero-dep sink, so the references stay acyclic.

The TypeScript package `@dcsv-io/d2-error-category` is the zero-dep leaf whose `ErrorCategory` string-union is generated from the same error-category spec. A cross-runtime parity fixture asserts the .NET wire set ≡ the TS union ≡ the spec.

## Source of truth

The error-category contract spec declares the nine category wire strings. The closed set matches the `category` enum in the error-codes canonical schema byte-for-byte; the error-codes registry cross-checks every code's `category` against this set at build time.

Never hand-edit generated `ErrorCategory.g.cs` — change the spec and rebuild.
