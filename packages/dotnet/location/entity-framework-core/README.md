<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Location.EntityFrameworkCore

> **Audience**: backend .NET service engineers mapping `DcsvIo.D2.Location` value objects into EF Core entity models via infra `IEntityTypeConfiguration<T>`.

Mapping helpers for the DcsvIo.D2.Location value objects (VOs): `StreetAddress`, `AdminLocation` and `Coordinates`.
Each value object has one helper that sets up its complex type and its value converters. The host's
`IEntityTypeConfiguration<T>` implementation calls the helpers. The domain aggregate has plain VO-typed properties and
no EF references.

Each helper, in one call:

- Registers member value converters where needed (`CountryCode`, `SubdivisionCode`)
- Applies `HasMaxLength` from `FieldConstraints.*` caps (plus the encoder-intrinsic
  geohash / plus-code caps)
- Writes the per-field anonymize defaults via the fluent `.Anonymize*` API
  (`DcsvIo.D2.DataGovernance.EntityFrameworkCore`)

The package ships no `DbContext`, no migrations and no DI engine. The host provides all of those.

---

## Install

```bash
dotnet add package DcsvIo.D2.Location.EntityFrameworkCore
```

## Domain purity

Host aggregates declare the VO-typed properties as plain CLR properties:

```csharp
// Host domain aggregate - ZERO EF references.
public sealed class Sighting
{
    public required StreetAddress Where { get; init; }
    public AdminLocation? Admin { get; init; }
    public Coordinates? Coords { get; init; }
}
```

All EF mapping (converters, lengths, anonymize annotations) is in the host's infra
`IEntityTypeConfiguration<T>` class, which calls the helpers of this package:

```csharp
// Host infra layer - SightingConfiguration.cs (NOT in the toolkit; illustrative).
internal sealed class SightingConfiguration : IEntityTypeConfiguration<Sighting>
{
    public void Configure(EntityTypeBuilder<Sighting> b)
    {
        b.ComplexProperty(s => s.Where, cp => cp.MapStreetAddress());
        b.ComplexProperty(s => s.Admin, cp => cp.MapAdminLocation());
        b.ComplexProperty(s => s.Coords, cp => cp.MapCoordinates());
    }
}
```

---

## Multi-field VO helpers (complex types)

Called from inside a `b.ComplexProperty(..., cp => ...)` callback. The helper takes no selector argument, because the
host already opened the `ComplexPropertyBuilder<TComplex>`; the helper configures the members of `cp`.

| Helper                  | VO                                                                        | Anonymize defaults                                                                                                                            |
| ----------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `cp.MapStreetAddress()` | `StreetAddress` (Line1-5/HashId)                                          | Line1 -> `"[deleted]"` (constant); Line2-5 -> SetNull; HashId -> cleared sentinel                                                             |
| `cp.MapAdminLocation()` | `AdminLocation` (City/PostalCode/Subdivision/Country/HashId)              | City/PostalCode/Subdivision -> SetNull; **Country KEPT** (coarse, no annotation); HashId -> cleared sentinel                                  |
| `cp.MapCoordinates()`   | `Coordinates` (Latitude/Longitude/Geohash/PlusCode/AccuracyMeters/HashId) | Latitude/Longitude -> `"0"` (constant, coerced to `0.0`); Geohash/PlusCode -> SetEmpty; AccuracyMeters -> SetNull; HashId -> cleared sentinel |

The helpers contain these value converters, so the host never wires them by hand:

- `AdminLocation.SubdivisionIso31662Code` -> `SubdivisionCode <-> .Value string` + `HasMaxLength(8)`
  (an empty stored value reads back as `null`, never `SubdivisionCode("")`)
- `AdminLocation.CountryIso31661Alpha2Code` -> `CountryCode enum <-> alpha-2 name string`

Required-numeric anonymize coercion: `Coordinates.Latitude` / `Longitude` are required
non-nullable `double` columns, so validation rule V7 of `AnonymizationModelValidator` forbids `SetNull`. They take a
constant `"0"` string; the anonymization engine coerces it to `0.0` through the column's
type mapping at erasure time. The required geohash / plus-code strings clear to empty
(`SetEmpty`); only the nullable `AccuracyMeters` clears to null.

The same VO type twice (e.g. home and work `AdminLocation`) works: the host calls
`MapAdminLocation()` twice through two distinct host-property selectors. EF Core 10 prefixes
columns with the owning-property path automatically (`HomeLocation_City` vs
`WorkLocation_City`). The helpers never call `HasColumnName`, so this default prefixing keeps the column names
unique.

---

## Per-VO anonymize-default table

| VO            | Field                                   | Default                             | Note                                    |
| ------------- | --------------------------------------- | ----------------------------------- | --------------------------------------- |
| StreetAddress | Line1                                   | `"[deleted]"`                       | Non-nullable - constant required        |
| StreetAddress | Line2-Line5                             | SetNull                             | Nullable                                |
| StreetAddress | HashId                                  | cleared sentinel (`"v1." + 64x'0'`) |                                         |
| AdminLocation | City/PostalCode/SubdivisionIso31662Code | SetNull                             | Nullable                                |
| AdminLocation | CountryIso31661Alpha2Code               | **KEPT**                            | Coarse-grained, not anonymized          |
| AdminLocation | HashId                                  | cleared sentinel                    |                                         |
| Coordinates   | Latitude/Longitude                      | `"0"` (constant)                    | Non-nullable numeric - coerced to `0.0` |
| Coordinates   | Geohash/PlusCode                        | SetEmpty                            | Non-nullable string                     |
| Coordinates   | AccuracyMeters                          | SetNull                             | Nullable                                |
| Coordinates   | HashId                                  | cleared sentinel                    |                                         |

Tombstone values are non-i18n literals: they are the same in every locale on purpose.

---

## Host responsibilities

1. Register the anonymization engine: `services.AddD2DataGovernance(...)`.
2. Apply anonymization conventions: call `ApplyAnonymizationConventions()` on
   `ModelConfigurationBuilder` in `ConfigureConventions`.
3. Implement `IUserOwned` + `IAnonymizationTrackable` on entities that carry anonymizable
   location data.

---

## EF Core 10 complex-member-index limitation

For the EF Core 10 limitation on indexing `ComplexProperty` member columns (e.g.
`AdminLocation.City`) and the `CreateD2Index` workaround, see
`DcsvIo.D2.EntityFrameworkCore`.

---

## Telemetry

No telemetry surface - mapping helpers are pure model-build-time calls with no runtime span or metric emission.

## Edge cases / gotchas

- `SetNull` is blocked on required numeric columns. `Coordinates.Latitude` / `Longitude` are non-nullable `double`
  columns. Rule V7 of `AnonymizationModelValidator` in `DcsvIo.D2.DataGovernance.EntityFrameworkCore` fails host
  startup when a `SetNull` rule targets a non-nullable column. Both fields take a constant `"0"` anonymization rule;
  the engine coerces `"0"` to `0.0` through the column's type mapping at erasure time.
- The country field is kept on purpose. `AdminLocation.CountryIso31661Alpha2Code` has no anonymization annotation.
  Country is coarse-grained, and the helper keeps it so that analytics still have it after erasure.
- The same VO type twice (e.g. home and work `AdminLocation`) works. EF Core 10 prefixes columns with the
  owning-property path automatically. The helpers never call `HasColumnName`.

## Configuration

No configuration: the helpers have no tunable behavior. All caps come from the shared, generated `FieldConstraints`
catalog.

## Dependencies

- `DcsvIo.D2.Location` (`location/core/`) - the `StreetAddress` / `AdminLocation` /
  `Coordinates` VO types being mapped
- `DcsvIo.D2.Validation.Abstractions` - `FieldConstraints.*` length caps
- `DcsvIo.D2.DataGovernance.EntityFrameworkCore` - the fluent `.Anonymize*` API
- `Microsoft.EntityFrameworkCore.Relational` - `ComplexPropertyBuilder<T>`,
  `ComplexTypePropertyBuilder<T>`, `HasMaxLength`, `HasConversion`
