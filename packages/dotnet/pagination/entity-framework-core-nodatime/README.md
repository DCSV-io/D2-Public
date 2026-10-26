<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime

NodaTime **Instant** sort modes for the multi-mode list page. Cursor primary keys
use the culture-safe `InstantPattern.ExtendedIso` for Format and Parse.

## Install

```bash
dotnet add package DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime
```

## Public API

| Member                                                         | Role                                                                                                                                      |
| -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `SortCatalogInstantExtensions`                                 | The static class holding this package's whole surface                                                                                     |
| `SortCatalog<TRow>.Instant(primary, tieBreak)`                 | Registers an Instant primary column with a Guid tie-break; the wire field name is the camelCase of the primary property                   |
| `SortCatalog<TRow>.Instant(fieldWireConst, primary, tieBreak)` | The alias overload, for the rare case where the wire field name differs from the property name; the const comes from generated sort names |
| `SortCatalogInstantExtensions.RoundTripExtendedIso(instant)`   | Formats an Instant to the cursor wire form and parses it back, so a caller can pin the round trip the cursor depends on                   |

## Usage

```csharp
using DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime;

var catalog = new SortCatalog<MyPageRow>()
    .Instant(r => r.JoinedAt, r => r.Id)
    .Text(r => r.Email, r => r.Id);
```

## Configuration

No options type and no configuration section. The pattern is fixed at
`InstantPattern.ExtendedIso`, because the cursor wire form has to be stable
across hosts and cultures.

## Telemetry

This package emits no span, metric or log. It registers expressions on a catalog
and formats a cursor key.

## Edge cases

- A cursor key that is absent, empty or not ExtendedIso returns
  `LIB_PAGINATION_CURSOR_KEY_UNDECODABLE` rather than throwing.
- ExtendedIso keeps sub-second precision, so two rows one tick apart page in the
  order the database returned them; the Guid tie-break orders rows with equal
  instants.
- The alias overload throws for a null, empty or whitespace field name.

## Dependencies

| PackageId                                  | Role                                 |
| ------------------------------------------ | ------------------------------------ |
| `DcsvIo.D2.Pagination.EntityFrameworkCore` | SortCatalog / ListPageEf             |
| `NodaTime`                                 | Instant + InstantPattern.ExtendedIso |

## Notes

- No application-domain types.
- ExtendedIso is mandatory for Instant cursor wire form.
