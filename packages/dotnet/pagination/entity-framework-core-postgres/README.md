<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres

For a developer adding text search to an EF Core list API on PostgreSQL. This
README states what to declare on the entity and what to call on the query, so
a search page is bounded, indexed, ranked and cursored the same way every list
page is.

It is the PostgreSQL leaf of the list-search capability: the trigram search
dialect, the generated search-blob column and its index.
`DcsvIo.D2.Pagination.EntityFrameworkCore` stays provider-neutral and defines the
order of operations; this package supplies the expressions.

## Install

```bash
dotnet add package DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres
```

The database needs the `pg_trgm` extension for the index and the similarity function.

## Public API

| Surface                                                 | Role                                                                    |
| ------------------------------------------------------- | ----------------------------------------------------------------------- |
| `TrigramSearchDialect<TRow>`                            | A containment pattern for the filter, weighted similarity for the score |
| `TrigramSearchDialect<TRow>.EscapePattern`              | Escapes the pattern metacharacters, so wildcards match text             |
| `TrigramSearchDialect<TRow>.ESCAPE_CHARACTER`           | The escape character the escape clause declares                         |
| `HasD2SearchBlob` on `EntityTypeBuilder<TEntity>`       | Declares the blob column, its index and its sources                     |
| `SearchBlobModelBuilderExtensions.WHITESPACE_CLASS`     | The whitespace the column's collapse covers                             |
| `SearchBlobModelBuilderExtensions.INDEX_METHOD`         | The index method the blob column is indexed with                        |
| `SearchBlobModelBuilderExtensions.INDEX_OPERATOR_CLASS` | The operator class the blob index is built with                         |
| `ToTrigramSearchPageAsync` on `IQueryable<TRow>`        | One trigram-backed search page, dialect supplied                        |

## Configuration

None. There are no options to bind. The query-length bounds live on the
operation's `SearchPolicy`, and the searchable columns and their weights live on
its `SearchIndexDescriptor`.

## Dependencies

| PackageId                                  | Role                                                     |
| ------------------------------------------ | -------------------------------------------------------- |
| `DcsvIo.D2.Pagination`                     | Page types, the normalizer and the validated query       |
| `DcsvIo.D2.Pagination.EntityFrameworkCore` | The search catalog, the reach type and the page pipeline |
| `DcsvIo.D2.Result`                         | `D2Result` control flow                                  |
| `DcsvIo.D2.Utilities`                      | Null, empty and whitespace checks                        |
| `Microsoft.EntityFrameworkCore`            | `IQueryable` composition                                 |
| `Npgsql.EntityFrameworkCore.PostgreSQL`    | The pattern operator and the trigram similarity function |

## Usage examples

Declare the blob column on the entity:

```csharp
builder.HasD2SearchBlob(ListThingsCatalog.Descriptor.SearchIndex);
```

Take one search page:

```csharp
var page = await query.ToTrigramSearchPageAsync(
    new PageRequest(input.Cursor, input.Limit, input.Sort, input.Order, input.Q),
    ListThingsCatalog.Descriptor,
    SearchReach<ListThingsPageRow>.Restrict(r => r.OwnerId == ownerId));
```

The reach argument is required and its unrestricted case is a named factory, so
an unbounded search is something a reviewer can search for rather than a
predicate that is always true.

## Telemetry

None. This package emits no log, span or metric; the failures it can produce
are returned as `D2Result` values with `LIB_PAGINATION_*` codes.

## Edge cases

- **A query of wildcard characters** clears the minimum length as literal
  characters. The dialect escapes them, so it matches rows whose text contains
  those characters, which is normally none, rather than every row.
- **The stored column is not case-folded.** Case-insensitivity is a property of
  the operator, because a provider's lower-casing function is
  collation-dependent outside the ASCII range and a .NET fold cannot be made to
  agree with it there.
- **A source column that is null contributes nothing** and its separator does
  not double, so a row missing an optional field is still found by text from the
  field it does have.
- **Whitespace runs inside a source collapse to one space**, over the character
  set `WHITESPACE_CLASS` defines, which is the set query normalization collapses.
  A stored value containing a tab, a double space or a non-breaking space is
  therefore found by the single-spaced text a caller types.
- **A missing index makes the query slower, not wrong.** Correctness does not
  depend on it, so a missing index is an operations problem rather than a data
  problem.
- **A property the entity does not map** fails the model build at start-up and
  identifies the property, rather than returning nothing at the first query.
