<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Pagination.EntityFrameworkCore

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.6.8 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.6.7 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.6.6 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.6.5 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.6.4 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.6.3 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.6.2 - 2026-09-26
### Changed

- The `ListPageMaterializer` and `SearchCatalog` remarks describe the contract directly. The 0.1.1 release note states the README change from its own public surface. No code changed.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.6.1 - 2026-09-26
### Changed

- README: the `IListPageDescriptor<TRow>` row lists `Policy`, `AllowedSortFields` and `SortCatalog`. No code changed.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.5.1. This package's own surface is unchanged.
## 0.6.0 - 2026-09-26
### API-breaking

- `Scored<TRow>.Row` is a `required` member rather than one initialized to `default!`, so a member initialization that omits `Row` no longer compiles. Relevance projection, ordering and the SQL it produces are unchanged.
### Changed

- `ListPageEf` and `SearchPageEf` test an empty sort allow-set or arm list with `Falsey()` from `DcsvIo.D2.Utilities` rather than a count comparison. Behavior is unchanged.
## 0.5.3 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Pagination` advances to 0.5.0. `SortDirectionWire` exposes `ASC` and `DESC`;
  the wire values remain `asc` and `desc`.
## 0.5.2 - 2026-09-11
### Changed

- `SearchCatalog` and `ISearchableListPageDescriptor` xmldoc rewritten subject-first. No surface changed.
## 0.5.1 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.4.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.0 - 2026-09-10
### API-breaking

- `SortCatalog<TRow>.String` is renamed to `SortCatalog<TRow>.Text`, on both
  overloads. The old name matched the `System.String` type name, which the
  CA1720 analyzer reports and which two suppressions had been silencing. A
  caller updates each registration call; the parameters, the return value and
  the registered wire field name are unchanged.
## 0.4.1 - 2026-09-08
### Changed

- The README states what the built search catalog exposes and what `Build` throws on, and contains its configuration,
  telemetry and edge-case sections.
- The `DeriveCamelCase` emptiness check calls `Falsey()`.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.4.0 - 2026-09-08
### Added

- `ToListPageOverArmsAsync` draws one keyset page from several queries that admit no row in common. The cursor predicate, the ordering and the limit are applied to each arm and again to their concatenation, and the total counts the arms unlimited. A read whose predicate is an OR over two columns can be expressed as two such arms, and a relational database then walks an ordered index per arm and stops at the page instead of reading every matching row and sorting it. The arms must admit no row in common: concatenation keeps duplicates, and nothing here can check it. Empty arms and a null arm throw before the request is read, so a caller's own mistake is not reported as a pagination failure about the request.

## 0.3.9 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.3.8 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Pagination` bumped to 0.3.8. This package's own surface is unchanged.
## 0.3.7 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Pagination bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.6 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Pagination bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Pagination bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.4 - 2026-08-05
### Changed

- Dependency update cascade. `DcsvIo.D2.Pagination` advanced, so this package's
  resolved-dependency fingerprint moves and the version moves with it. No source
  in this package changed.

## 0.3.3 - 2026-08-05
### Changed

- Dependency update cascade. `DcsvIo.D2.Pagination` advanced to `0.3.3`, so this
  package's resolved-dependency fingerprint moves and the version moves with it.
  No source in this package changed.

## 0.3.2 - 2026-08-05
### Changed

- Documentation only. The remarks on the cursor-value binding, and the 0.3.1
  note below, say what a string sort key holds - human-readable text
  identifying a person or a record - without referring to a kind of record this
  package knows nothing about. No public surface and no behavior changed.
## 0.3.1 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Pagination bumped.
### Fixed

- The decoded cursor primary key and its tie-break reach the keyset predicate as
  a bound value rather than an expression-tree constant, in both sort-entry
  factories. Entity Framework renders a hand-built constant inline in the
  command text, so on a cursor page of a string-sorted surface the boundary
  row's own sort value - human-readable text identifying a person or a record,
  an electronic mail address or a display name - was written into the statement
  the provider's command logger emits and its exception detail carries, with no
  sensitive-data logging switch involved. It also gave every boundary value its
  own statement, its own plan and its own compiled-query cache entry.
- A string sort registration decodes the empty primary key rather than refusing
  it. A boundary row whose sort column is empty is ordinary data, and refusing
  it turned a working listing into a failure.
## 0.3.0 - 2026-08-04
### API-breaking

- `Scored<TRow>` is a class with settable members rather than a positional
  record, and `SearchCatalog.Project` builds it through a member initialization.
  That is a provider constraint rather than a style: the relevance ordering and
  the cursor filter are applied to this projection, and a provider binds a
  member read back to a settable member and refuses to bind one back to a
  constructor argument. Built through a constructor, every relevance page
  reached the provider as an expression it refused - which no test could see,
  because they all ran on the in-memory provider, which translates nothing.
### Changed

- Dependency update: DcsvIo.D2.Pagination bumped.
## 0.2.0

### Wire-breaking

- `SORT_CATALOG_EMPTY` and `SORT_FIELD_NOT_REGISTERED` now surface as 500 rather
  than an anonymous 400. Both describe a host whose allow-set and sort catalog do
  not line up, which no request can succeed against.

### API-breaking

- `ToListPageAsync` takes a `SortFieldAllowSet` in place of
  `IReadOnlySet<string>`, and gains an optional cursor codec before its
  cancellation token.
- `ListPageEf.Codec` is replaced by the read-only `ListPageEf.DefaultCodec`. A
  process-wide settable codec is shared mutable state two suites interfere
  through by test order and a host can change underneath in-flight pages; a page
  extension takes its own optional codec instead.

### Added

- `IListPageDescriptor<TRow>` and `ISearchableListPageDescriptor<TRow>`:
  everything one list operation's paging is configured by, as one value. A list
  that does not search cannot be handed to a search extension at all.
- `SearchCatalogBuilder<TRow>` and `SearchCatalog<TRow>`: registration happens on
  the builder, and the built catalog exposes nothing that mutates it, so the
  process-wide instance generated code holds cannot be written to.
- `ISearchDialect<TRow>`: how one database provider expresses filtering and
  ranking. This package defines the order of operations; a provider leaf supplies
  the expressions.
- `SearchReach<TRow>`: the rows a search may reach. Constructing one bounds the
  search, and the unrestricted case is a named factory rather than a predicate
  that is always true.
- `SearchIndexDescriptor` and `Scored<TRow>`.
- `SearchPageEf.ToSearchPageAsync`: the reach predicate, then the dialect's
  filter, then the count, then the page. A request sorted by relevance pages a
  projection carrying the materialized score; every other applied field pages the
  query directly, which is also what a request carrying no query does.
- `SortFieldEntry<TRow>.CreateOrderedPrimary`: an entry for a primary the
  database orders with the relational operators directly, which is what a numeric
  score needs.

## 0.1.1 - 2026-07-26
### Changed

- Gallery README revised to describe the package from its own public surface. Public API and behavior are unchanged.
- Dependency update: DcsvIo.D2.Pagination bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.0

### Added

- Initial release: `SortCatalog<TRow>`, `ListPageEf.ToListPageAsync` multi-mode list page.
