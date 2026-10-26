<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Location

> **Audience**: Backend .NET service engineers attaching location or postal-address data to domain entities.

> Geographic value objects whose content hash identifies duplicates, for handlers and service code that attach location
> data to entities. The package has `Coordinates`, `StreetAddress`, `AdminLocation`, the `ComposeLocationHash` free
> function, and the `IPostalCodeValidator` boundary contract with a global-range `DefaultPostalCodeValidator`. It
> produces deterministic identity hashes for civic locations. `StreetAddress` and `AdminLocation` hash the
> `NormalizeForHash` form of their text, so inputs that differ only in case, diacritics or punctuation get the same hash
> in any script. The package does not transliterate between scripts and does not match misspelled inputs.

## Install

```bash
dotnet add package DcsvIo.D2.Location
```

## Purpose

The package has the immutable, content-addressable value objects `Coordinates`, `StreetAddress` and `AdminLocation`,
the free hash composer `ComposeLocationHash` and the boundary validator `IPostalCodeValidator`. Every factory returns
`D2Result<T>` (smart-constructor pattern). The same content produces the same `HashId` (`"v1." + SHA-256 hex`), so
duplicate insertions across services and repeated submissions map to the same row. The package is a pure-domain layer.
It depends on `DcsvIo.D2.Geo.Abstractions` (typed code enums), `DcsvIo.D2.Result` (D2Result factories),
`DcsvIo.D2.Utilities` (Falsey/Truthy/CleanStr boundary helpers and the canonical `NormalizeForHash` extension), and
`DcsvIo.D2.Validation.Abstractions` (FieldConstraints length caps). It has no infrastructure dependencies, no NodaTime
and no observability surface.

## Public API surface

- `ValueObjects/Coordinates.cs` - `sealed record` with the representations lat/lon decimal degrees, geohash-10 and OLC
  plus-code-13, and optional accuracy metadata. Its factories:
  - `Coordinates.Create(latitude, longitude, accuracyMeters?)` - from decimal degrees.
  - `Coordinates.FromGeohash(geohash, accuracyMeters?)` - from a 1-12 char geohash (truncated / re-encoded to
    canonical 10).
  - `Coordinates.FromPlusCode(plusCode, accuracyMeters?)` - from a valid OLC plus-code.

  All factories reduce the input to the canonical geohash-10 cell center, so inputs in different forms that represent
  the same physical cell of about 1 m produce byte-identical `HashId` values. Accuracy is metadata and is not included
  in the hash.
- `ValueObjects/StreetAddress.cs` - `sealed record` with 5 free-text lines (`Line1` required, `Line2..Line5` optional,
  no gap rule). Normalization has two stages. The stored form keeps case and strips decorative punctuation. The hash
  form calls the `NormalizeForHash` extension of `DcsvIo.D2.Utilities`, which upper-cases the text, strips combining
  marks after NFD decomposition, and keeps only Letter, Decimal-digit and ASCII-space characters (Unicode category
  filter). Each line is capped at `FieldConstraints.STREET_LINE_MAX` (255 chars, measured on the post-clean stored
  value).
  - `StreetAddress.Create(line1, line2?, line3?, line4?, line5?)` returns `D2Result<StreetAddress>`.
  - Failure keys: `TK.Geo.Validation.ADDRESS_LINE1_REQUIRED` (line1 empty after clean), `TK.Geo.Validation.STREET_LINE_TOO_LONG` (any line exceeds `STREET_LINE_MAX` after clean).
- `ValueObjects/AdminLocation.cs` - `sealed record` with administrative hierarchy: country, subdivision, city, postal
  code (any subset). Cross-field coherence is enforced when both country and subdivision are supplied; when a caller
  supplies only a subdivision, the country is filled in from `SubdivisionCode.ParentCountry`. City is capped at
  `FieldConstraints.CITY_MAX` (255 chars) and postal code at `FieldConstraints.POSTAL_CODE_MAX` (16 chars), both
  measured on the post-clean value. The length cap always applies and runs before the optional
  `IPostalCodeValidator`.
  - `AdminLocation.Create(countryIso31661Alpha2Code?, subdivisionIso31662Code?, city?, postalCode?, postalCodeValidator?)` returns `D2Result<AdminLocation>`.
  - Failure keys: `TK.Geo.Validation.ADMIN_EMPTY_RECORD` (all fields null/empty), `TK.Geo.Validation.ADMIN_COUNTRY_SUBDIVISION_MISMATCH` (country/subdivision mismatch), `TK.Geo.Validation.CITY_TOO_LONG` (city exceeds `CITY_MAX`), `TK.Geo.Validation.POSTAL_CODE_TOO_LONG` (postal exceeds `POSTAL_CODE_MAX`), `TK.Geo.Validation.POSTAL_CODE_INVALID` (validator failure).
- `ComposeLocationHash.cs` - `static class` with `Compose(Coordinates?, StreetAddress?, AdminLocation?): string?` -
  joins the component `HashId`s into a single `"v1."`-prefixed hash. All-null input returns `null` (location
  absent; not an error).
- `IPostalCodeValidator.cs` - boundary contract `D2Result<string> Validate(string?, CountryCode?)`. Lives in
  `DcsvIo.D2.Location` (not Abstractions) so the DI seam stays out of pure-vocabulary projects.
- `DefaultPostalCodeValidator.cs` - `sealed class` implementing the global-range shape check (3-10 alphanumeric
  characters; internal spaces and hyphens allowed; alphanumeric at both ends). It does not check the country by
  design; a consumer that needs strict per-country validation overrides it.
- `DependencyInjection.cs` - `extension(IServiceCollection)` block-form `AddD2Location()` registers
  `IPostalCodeValidator -> DefaultPostalCodeValidator` (singleton, idempotent via `TryAddSingleton`).

## Dependencies

- `DcsvIo.D2.Geo.Abstractions` - typed `CountryCode` enum + `SubdivisionCode` wrapper struct (including
  `SubdivisionCode.ParentCountry` for `AdminLocation` coherence).
- `DcsvIo.D2.Result` - `D2Result<T>` semantic factories (`Ok`, `ValidationFailed`).
- `DcsvIo.D2.Utilities` - `Falsey()` / `Truthy()` / `CleanStr()` / `NormalizeForHash()` extension methods. The
  `NormalizeForHash` extension is the canonical implementation for hash canonicalization;
  `StreetAddress.NormalizeForHash` is a thin internal forwarder to it.
- `DcsvIo.D2.Validation.Abstractions` - `FieldConstraints` length cap constants (`STREET_LINE_MAX`, `CITY_MAX`,
  `POSTAL_CODE_MAX`) that the `Create` methods of the value objects check.
- `Microsoft.Extensions.DependencyInjection.Abstractions` (NuGet) - `IServiceCollection` receiver for the `AddD2Location` registration extension.

The package has no reference to `DcsvIo.D2.Geo.Default`, NodaTime, logging or observability. It is pure-domain by
design.

## Security / PII

The value objects in this library contain geographic and postal-address data that meets the GDPR definition of
personally identifiable information:

- `Coordinates` at geohash-10 / ~1m precision is precise geolocation data and PII per GDPR (geographic data: lat/long
  beyond country level).
- `StreetAddress` (every line) is postal-address PII and directly GDPR-sensitive.
- `AdminLocation.City` and `AdminLocation.PostalCode` are PII when combined with a name or other identifying fields
  (city and postal code beyond country level).

Consumer-side contract: handlers, repositories, audit-log emitters, and any DTO or entity that holds fields of these
types must apply `[RedactData]` on the property or field. Example:

```csharp
public sealed record Sighting
{
    [RedactData] public Coordinates? Coords { get; init; }
    [RedactData] public StreetAddress? Address { get; init; }
    [RedactData] public AdminLocation? Admin { get; init; }
}
```

This library does not put `[RedactData]` on the value-object definitions, because it has no `[LoggerMessage]`
declarations and no JSON-serialization surface. The annotation applies only in the consumer layer, where the PII
reaches a sink.

## Telemetry

Not applicable: this pure-domain value-object library has no telemetry surface, by design. Handlers in the consumer's
code emit the telemetry. Instrumentation in each value object would add cost to handler hot paths and no information.

## Configuration / Options

Not applicable: the package reads no environment variables and no appsettings, and it has no Options record.
`DefaultPostalCodeValidator` has hardcoded constants (the global-range regex and a 50ms `matchTimeout`; see the
source). Per-country validation is a DI override that the consumer registers with `services.Replace(...)`. The only
configurable default is the validator DI-override seam.

## Usage examples

```csharp
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location;
using DcsvIo.D2.Location.ValueObjects;

// 1. Construct any subset of the three value objects (each returns D2Result<T>).
var coordsResult = Coordinates.Create(40.7128, -74.0060);
if (!coordsResult.Success)
    return coordsResult.AsFailure<MyAggregate>(); // propagate ValidationFailed

var streetResult = StreetAddress.Create(line1: "350 Fifth Ave", line2: "Floor 86");
if (!streetResult.Success)
    return streetResult.AsFailure<MyAggregate>();

var adminResult = AdminLocation.Create(
    countryIso31661Alpha2Code: CountryCode.US,
    subdivisionIso31662Code: SubdivisionCode.US_NY,
    city: "New York",
    postalCode: "10118",
    postalCodeValidator: _validator); // injected IPostalCodeValidator
if (!adminResult.Success)
    return adminResult.AsFailure<MyAggregate>();

// 2. Each VO carries its own content-addressable HashId.
//    coordsResult.Data.HashId  == "v1.<64 hex>"
//    streetResult.Data.HashId  == "v1.<64 hex>"
//    adminResult.Data.HashId   == "v1.<64 hex>"

// 3. Compose into a single location identity (free function, returns string?).
string? locationHash = ComposeLocationHash.Compose(
    coordsResult.Data,
    streetResult.Data,
    adminResult.Data);
// Null only when all three inputs are null (location absent - not an error).
```

DI wire-up (composition root):

```csharp
services.AddD2Location();          // registers IPostalCodeValidator -> DefaultPostalCodeValidator
services.Replace(ServiceDescriptor // optional: override with country-specific validator
    .Singleton<IPostalCodeValidator, MyStrictPostalCodeValidator>());
```

## Important / usage notes

Locations are immutable. An update creates a new location, repoints the references to it and deletes the old one. The
same hash content gives the same `HashId`, which gives the same row in any consumer's local table, so the hash ID
deduplicates locations.

The fixture `contracts/location/parity-fixtures.json` pins the hash algorithm: `LocationHashDeterminismTests` in
`DcsvIo.D2.Tests` asserts every `HashId` and `expectedOutcome` row. A byte divergence means the hash algorithm changed,
and it would silently create duplicate records for content that was identical before.

`ComposeLocationHash.Compose` returns `string?`, not `D2Result<string>`, because the operation cannot fail: its inputs
are already-validated value objects or null. `null` means the location is absent, not an error. A plain value
rather than a `D2Result` fits because the method is a pure composition of already-validated inputs.

## References

- `DcsvIo.D2.Location.EntityFrameworkCore` - the sibling library that maps these value objects onto a host's EF Core
  model (`MapStreetAddress` / `MapAdminLocation` / `MapCoordinates` and per-field anonymize defaults).
- `DcsvIo.D2.Geo.Abstractions` - the typed `CountryCode` and `SubdivisionCode` surface this library uses.
- `contracts/location/parity-fixtures.json` - the hash-determinism fixture file.
