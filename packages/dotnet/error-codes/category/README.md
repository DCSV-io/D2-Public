<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ErrorCodes.Category

Foundational zero-dependency (BCL-only) leaf that exposes the closed `ErrorCategory` classification - the
semantic/telemetry class that every `D2Result` and every error code has (the wire strings are listed under
[Source of truth](#source-of-truth)). Source-generated from the error-category contract spec. It is a separate package
so that result-core and the error-codes registry can reference `ErrorCategory` downward without depending on the
merged-catalog generator.

The category is the producer's coarse signal - `infrastructure_unavailable`, `not_found`, `validation_failure` - so a
consumer can do generic class-based handling (retry any `InfrastructureUnavailable`, surface any `NotFound` as a 404)
without importing the producer's error-code catalog.

## Install

```bash
dotnet add package DcsvIo.D2.ErrorCodes.Category
```

## Public API

| Export                          | Purpose                                                                                                                                                                |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ErrorCategory`                 | Closed enum - one member per category (e.g. `ErrorCategory.NotFound`). Has `[JsonConverter(typeof(ErrorCategoryJsonConverter))]`, so it serializes as its wire string. |
| `ErrorCategoryWire.ToWire`      | `this ErrorCategory -> string` - the canonical snake_case wire string (e.g. `ErrorCategory.ValidationFailure -> "validation_failure"`). Throws on an undefined member. |
| `ErrorCategoryWire.TryFromWire` | `string -> bool + out ErrorCategory` - parses a wire string back to the enum. Returns `false` for any unknown string (never throws).                                   |
| `ErrorCategoryJsonConverter`    | `JsonConverter<ErrorCategory>` - reads/writes the snake_case wire string; unknown wire strings throw `JsonException` (strict policy).                                  |

```csharp
ErrorCategory.NotFound.ToWire();                       // "not_found"
ErrorCategoryWire.TryFromWire("conflict", out var c);  // true, c == ErrorCategory.Conflict
JsonSerializer.Serialize(ErrorCategory.RateLimited);   // "rate_limited"
```

## Dependency edge

```
DcsvIo.D2.ErrorCodes.Category   (BCL-only - no project references)
                ▲
                ├── DcsvIo.D2.ErrorCodes.Registry   (references ErrorCategory downward)
                └── DcsvIo.D2.Result                (D2Result.Category is a typed ErrorCategory?)
```

`ErrorCategory.g.cs` is emitted by the sibling `DcsvIo.D2.ErrorCodes.Category.SourceGen` analyzer (referenced
build-only, no runtime dll). The registry references this leaf instead of emitting the enum itself, and result-core
references it so `D2Result.Category` is a typed `ErrorCategory?` - both reference this package and it references
neither, so the references stay acyclic.

The TypeScript package `@dcsv-io/d2-error-category` is the zero-dep leaf whose `ErrorCategory` string-union is
generated from the same error-category spec. A cross-runtime parity fixture asserts that the .NET wire set, the TS
union and the spec are equal.

## Source of truth

The error-category contract spec declares the category wire strings `validation_failure`, `not_found`, `conflict`,
`policy_denied`, `rate_limited`, `payload_too_large`, `infrastructure_unavailable`, `internal_error` and
`partial_success`. The closed set matches the `category` enum in the error-codes canonical schema byte-for-byte; the
error-codes registry cross-checks every code's `category` against this set at build time.

Never hand-edit generated `ErrorCategory.g.cs` - change the spec and rebuild.
