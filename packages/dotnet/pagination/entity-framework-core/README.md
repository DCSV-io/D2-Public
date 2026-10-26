<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Pagination.EntityFrameworkCore

EF Core **multi-mode list page** helpers for D2 list APIs. Provides
`SortCatalog<TRow>` (expression catalog) and `ListPageEf.ToListPageAsync`
(normalize + totalCount + cursor Where + OrderBy + take+1 + next cursor).

Use with the page types of `DcsvIo.D2.Pagination` (`ListPagingPolicy`,
`PageRequest`, `PageResult`, `SortSpec`, `SortDirectionWire`). Instant
primary columns use the companion package
`DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime`.

## Install

```bash
dotnet add package DcsvIo.D2.Pagination.EntityFrameworkCore
```

## Types

| Type                                  | Role                                                                                                                        |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `SortCatalog<TRow>`                   | Registers string (and companion Instant) sort fields with Guid tie-break                                                    |
| `SortFieldEntry<TRow>`                | One catalog field: OrderBy + cursor Where + encode                                                                          |
| `ListPageEf`                          | `ToListPageAsync` fluent on `IQueryable<TRow>`, `ToListPageOverArmsAsync` on a list of them, plus the shared `DefaultCodec` |
| `IListPageDescriptor<TRow>`           | Everything one list operation's paging is configured by: `Policy`, `AllowedSortFields`, `SortCatalog`                       |
| `ISearchableListPageDescriptor<TRow>` | A list operation that also searches                                                                                         |
| `SearchCatalogBuilder<TRow>`          | Registers the blob, the rank fields and the tie-break                                                                       |
| `SearchCatalog<TRow>`                 | The built, immutable search shape; `Project`, `BuildRelevanceEntry`                                                         |
| `SearchRankField<TRow>`               | One rank field and its weight                                                                                               |
| `Scored<TRow>`                        | A row and the relevance score one query gave it                                                                             |
| `ISearchDialect<TRow>`                | How one database provider expresses filtering and ranking                                                                   |
| `SearchReach<TRow>`                   | The rows a search may reach: `Restrict`, `Unrestricted`, `Apply`                                                            |
| `SearchIndexDescriptor`               | The properties the generated search column concatenates                                                                     |
| `SearchPageEf`                        | `ToSearchPageAsync` fluent on `IQueryable<TRow>`                                                                            |

## Usage

```csharp
var catalog = new SortCatalog<MyPageRow>()
    .Text(r => r.Email, r => r.Id);

var page = await query.ToListPageAsync(
    new PageRequest(cursor, limit, sort, order),
    policy,           // typically generated List{Op}Page.Policy
    catalog,          // typically generated List{Op}Catalog.PageRow
    allowedFields,    // typically generated List{Op}Page.AllowedFields
    ct: ct);

if (page.Failed || page.Data is null)
    return D2Result<MyOutput?>.BubbleFail(page);

// page.Data.AppliedSort.DirectionWire - never bare "asc"/"desc"
// page.Data.TotalCount - base filter pre-cursor
```

One search page, with the dialect a provider leaf supplies:

```csharp
var page = await query.ToSearchPageAsync(
    new PageRequest(cursor, limit, sort, order, q),
    ListThingsCatalog.Descriptor,
    SearchReach<MyPageRow>.Restrict(r => r.OwnerId == ownerId),
    dialect,
    ct: ct);
```

## Rules

- Wire field names default to camelCase of the primary property.
- Alias overload accepts a generated const only (rare).
- Effective allow-set = generated allow-set intersect catalog wire fields.
- A sort field outside the allow-set is `LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED`;
  a direction a locked field rejects is `LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED`.
- Encode failures BubbleFail - never ignore `.Data`.
- `ToListPageOverArmsAsync` draws one page from queries that admit no row in
  common. It applies the cursor predicate, the ordering and the limit to each arm
  and again to their concatenation, and counts the arms unlimited for
  `TotalCount`. A predicate that admits a row on either of several columns is one
  such set of arms: a database walks an ordered index per arm and stops at the
  page, where an OR over the columns makes it read every matching row and sort
  them. The arms must admit no row in common - concatenation keeps duplicates,
  and nothing in the package can check it.
- The reach predicate is applied before anything else and on every page, cursor
  pages included. Its unrestricted case is a named factory, so a search of the
  whole set can be found by searching the source for that factory name.
- `TotalCount` on a searching page counts the rows the query matched, taken after
  the reach predicate and the filter and before the cursor.
- A request sorted by relevance pages a projection carrying the materialized
  score. Every other applied field pages the query directly, which is also what a
  request carrying no query does.
- Registration happens on `SearchCatalogBuilder<TRow>`. The built `SearchCatalog<TRow>`
  exposes no member that changes it, so the process-wide instance generated code
  holds is read-only once built. `Build` throws when no blob or no tie-break was
  registered.

## Dependencies

| PackageId                       | Role                                            |
| ------------------------------- | ----------------------------------------------- |
| `DcsvIo.D2.Pagination`          | Pure page types + normalizer                    |
| `DcsvIo.D2.Result`              | `D2Result` control flow                         |
| `Microsoft.EntityFrameworkCore` | `IQueryable` / `ToListAsync` / `LongCountAsync` |

## Configuration

No options type and no configuration section. Every knob is a value the caller
passes per call: the policy, the catalog, the allow-set, the reach predicate and
the dialect.

## Telemetry

This package emits no span, metric or log. The query runs on the caller's
`DbContext`, so the EF Core and database instrumentation the host already
registers reports it.

## Edge cases

- A cursor minted under one query token is rejected against another, so a search
  page cannot continue into a different query's ordering.
- Sorting by relevance with no query is rejected; relevance is never registered in
  a sort catalog.
- `ToListPageOverArmsAsync` keeps duplicates: the arms must admit no row in
  common, and nothing in the package can check that.
- A page of exactly the limit still returns a next cursor only when the take+1
  probe found another row.
- `Build` on a search catalog with no blob or no tie-break throws at
  registration time, which is start-up rather than request time.

## Notes

- No application-domain or host types in this package.
- Instant sort modes: add `DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime`.
- Trigram search on PostgreSQL: add
  `DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres`, which supplies the dialect
  this package declares and never implements.
- Hosts own authorization and base filters; this package owns only the paging steps of `ListPageEf.ToListPageAsync`
  (normalize, count, cursor filter, ordering, take+1 and next cursor) and the search order of operations in
  [Rules](#rules).
