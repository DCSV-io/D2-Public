<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Pagination

Portable **multi-mode list page** helpers for list APIs. Normalize wire
`cursor` / `limit` / `sort` / `order`, encode versioned keyset cursors,
and return pages with `nextCursor`, `appliedSort`, and `totalCount`. Use this
package whenever a list endpoint needs stable continuation under concurrent
inserts without offset-as-primary paging.

## Install

```bash
dotnet add package DcsvIo.D2.Pagination
```

## Types

| Type                                          | Role                                                                                       |
| --------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `ListPagingPolicy`                            | Per-list `DefaultSort` (`SortSpec`) + DefaultLimit / MaxLimit                              |
| `ListPagingPolicy` defaults                   | DefaultLimit=50, MaxLimit=100 (constants)                                                  |
| `SortSpec`                                    | Applied sort field + `SortDirection` (Asc/Desc) + `DirectionWire`                          |
| `SortDirectionWire`                           | Wire order tokens (`asc` / `desc`) + TryParse                                              |
| `PageRequest`                                 | Wire request (`Cursor`, `Limit`, optional `Sort`, `Order`, `Query`)                        |
| `NormalizedPageRequest`                       | After policy apply (`Limit`, optional decoded cursor, applied sort, validated query)       |
| `KeysetCursor`                                | Versioned payload (`Version`, `Sort`, `Order`, `PrimaryKey`, `TieBreakerId`, `QueryToken`) |
| `PageResult<T>`                               | Page `Items` + `NextCursor` + `AppliedSort` + `TotalCount`                                 |
| `PageRequestNormalizer`                       | Pure `Normalize(request, policy, allowedSortFields, codec, searchPolicy)`                  |
| `IKeysetCursorCodec`                          | Encode / Decode seam                                                                       |
| `OpaqueKeysetCursorCodec`                     | v1 Base64Url JSON codec (no MAC, and the payload is readable by whoever holds the cursor)  |
| `AllowedDirections`                           | Flags: `None`, `Ascending`, `Descending`, `Both`                                           |
| `SortFieldSpec`                               | One allow-listed field and the directions it permits, plus `SoleDirection`                 |
| `SortFieldAllowSet`                           | `From`, `BothDirections`, `Fields`, `TryGet`, `IntersectWith`, `With`                      |
| `SortFieldNames`                              | Wire sort field names the library defines (`Relevance`)                                    |
| `SearchPolicy`                                | Query-length bounds, defaults 3 and 128 (constants)                                        |
| `SearchQuery`                                 | Validated normalized query + `QueryToken`, built by `Create`                               |
| `PaginationErrorCodes`                        | Generated `LIB_PAGINATION_*` constants, `AllCodes`, `GetHttpStatus`                        |
| `PaginationFailures`, `PaginationFailures<T>` | Generated failure factories, one per code                                                  |

## Usage

Use `SortDirectionWire.ASC` and `SortDirectionWire.DESC` for the `asc` and `desc`
wire values. `SortDirection.Asc` and `SortDirection.Desc` are the enum members.

```csharp
var policy = new ListPagingPolicy(
    DefaultSort: new SortSpec("joinedAt", SortDirection.Desc),
    DefaultLimit: 50,
    MaxLimit: 100);

// The allow-set lives with the caller or the generated catalog, not on
// ListPagingPolicy. Each field carries the directions it permits.
var allowed = SortFieldAllowSet.From(
    new SortFieldSpec("joinedAt", AllowedDirections.Both),
    new SortFieldSpec("email", AllowedDirections.Ascending));

var codec = new OpaqueKeysetCursorCodec();
var pageIn = new PageRequest(
    Cursor: wireCursor,
    Limit: wireLimit,
    Sort: wireSort,
    Order: wireOrder);
var norm = PageRequestNormalizer.Normalize(pageIn, policy, allowed, codec);

if (norm.Failed || norm.Data is null)
    return D2Result<MyListOutput?>.BubbleFail(norm);

// Compose LINQ with applied sort from NormalizedPageRequest / PageResult.AppliedSort.
// Peek limit+1 rows; when more exist Encode next KeysetCursor and BubbleFail on fail.
// Surface AppliedSort.Field + AppliedSort.DirectionWire + TotalCount on the list output.
```

## Policy rules

| Input                                            | Outcome                                                         |
| ------------------------------------------------ | --------------------------------------------------------------- |
| `limit` omitted                                  | `DefaultLimit`                                                  |
| `limit` in `1...MaxLimit`                        | as requested                                                    |
| `limit` > `MaxLimit`                             | **clamped** to `MaxLimit` (non-error)                           |
| `limit` <= 0                                     | `LIB_PAGINATION_LIMIT_INVALID`                                  |
| empty/null cursor                                | first page; client Sort/Order may apply when in allow-set       |
| malformed cursor                                 | `LIB_PAGINATION_CURSOR_INVALID`                                 |
| cursor `Version` != 1                            | `LIB_PAGINATION_CURSOR_VERSION_UNSUPPORTED`                     |
| cursor sort outside the allow-set                | `LIB_PAGINATION_CURSOR_SORT_NOT_ALLOWED`                        |
| unknown client sort field                        | `LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED` (allow-set fail closed) |
| order not permitted on a locked field            | `LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED`                     |
| policy yields no usable limit                    | `LIB_PAGINATION_PAGE_POLICY_INVALID` (500)                      |
| a query against a surface with no `SearchPolicy` | `LIB_PAGINATION_SEARCH_NOT_SUPPORTED`                           |

Default sort is a **`SortSpec`** (field + direction), not a bare string.
Sort field allow-sets are supplied by the caller (often a generated catalog
companion) - they are **not** a field on `ListPagingPolicy`.

## The direction lock

A field may permit one direction rather than both. With a lock in place:

| Request                                             | Outcome                                              |
| --------------------------------------------------- | ---------------------------------------------------- |
| sort field, order omitted                           | the sole permitted direction                         |
| sort field, order matching the lock                 | that direction                                       |
| sort field, order the lock refuses                  | `LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED`          |
| a cursor whose direction the lock no longer permits | the same code; the caller restarts at the first page |

A refused direction is a failure rather than a silent correction: a caller who
asked for ascending and was given descending without being told cannot act.

Constructing a `SortFieldSpec` permitting no direction throws. A field that
cannot be sorted on is a wiring defect in the host, and it fails at composition
rather than at the first request that sorts on it.

## Search

`SearchQuery.Create` trims, case-folds under the invariant culture, collapses
runs of inner whitespace, then bound-checks the result against the surface's
`SearchPolicy`. `Normalized` is the caller's literal text and is not interpreted as pattern
syntax, so the bounds mean the number of characters the caller typed and
escaping belongs to whatever matching dialect a host uses.

`QueryToken` is SHA-256 over the UTF-8 bytes of the normalized query, rendered
base64url without padding. The cursor stores it, so a cursor from one query is
rejected against another with `LIB_PAGINATION_CURSOR_QUERY_MISMATCH` rather than
serving a page that is neither result. It is a sameness check and nothing else:
a forged token only gives the forger a page of their own query.

Sorting by `SortFieldNames.RELEVANCE` resolves only against a query;
asking for it without one returns `LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY`.

A surface that declared no `SearchPolicy` has no bounds to check a query
against, so a request carrying one is rejected with
`LIB_PAGINATION_SEARCH_NOT_SUPPORTED` rather than serving a page of the whole
list as though it had searched.

## Cursor integrity

The codec is versioned Base64Url JSON **without** a MAC. The package does
not ship a shared-secret Options seam. Callers re-check authorization on every
page; a forged-looking cursor can only reposition within data the caller is
already authorized to list. The cursor is **not** a capability token. Normalize
rejects Version != 1 and Sort outside the caller allow-set.

## The cursor payload is readable

**Base64url is an encoding, not a seal: anyone holding a cursor string can decode
it and read `KeysetCursor.PrimaryKey`, which is the boundary row's value in the
column the page sorted by.** A surface that sorts on a personal column - the
allow-set example above registers `email` - therefore puts one such value per
page into its URLs, where it reaches access logs, proxies, referrer headers and
browser history, none of which see the response body.

A host chooses one of these per surface:

- Sort on a column that carries no personal value - an identifier, a timestamp,
  a rank - and keep the personal column out of the sort catalog.
- Encrypt the payload in its own `IKeysetCursorCodec`. The codec is an interface
  the host supplies, and `OpaqueKeysetCursorCodec` is only the default.

Primary key strings for temporal columns should use NodaTime
`InstantPattern.ExtendedIso` at the host (culture-safe Instant wire form).
Optional package `DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime` exposes
`SortCatalogInstantExtensions` for ExtendedIso Format/Parse.

## Dependencies

| PackageId                       | Role                                                   |
| ------------------------------- | ------------------------------------------------------ |
| `DcsvIo.D2.Result`              | `D2Result` control flow                                |
| `DcsvIo.D2.Utilities`           | `Falsey` / `Truthy` helpers                            |
| `DcsvIo.D2.I18n.Keys`           | The message keys the generated failure factories use   |
| `DcsvIo.D2.ErrorCodes.Category` | The semantic category the generated factories set      |

## Optional EF companions

| PackageId                                           | Role                                                             |
| --------------------------------------------------- | ---------------------------------------------------------------- |
| `DcsvIo.D2.Pagination.EntityFrameworkCore`          | `ToListPageAsync` for EF `IQueryable`                            |
| `DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime` | Instant ExtendedIso sort helpers                                 |
| `DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres` | Trigram list search - the dialect, the blob column and its index |

## Telemetry

This package does **not** emit meters or spans - pure normalize/codec helpers.
Hosts own observability around list call sites.

## Notes

- No Entity Framework in this package; hosts compose keyset `Where` / `OrderBy`
  (or use the EF companion package).
- Encode failures return Failed `D2Result` - list handlers must BubbleFail, never
  ignore `.Data`.
- Constants: `KeysetCursor.CURRENT_VERSION = 1`,
  `ListPagingPolicy.DEFAULT_LIMIT = 50`, `ListPagingPolicy.MAX_LIMIT = 100`.
