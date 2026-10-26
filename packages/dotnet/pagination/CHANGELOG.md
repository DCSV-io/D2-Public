<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Pagination

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.8 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.7 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.6 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.5 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.4 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.3 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.5.2 - 2026-09-26
### Changed

- The `PageRequestNormalizer.Normalize` parameter documentation and the `error-codes-source-gen` README describe the contract directly. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.1 - 2026-09-26
### Changed

- README: the `PageResult<T>` row names its `Items` property beside `NextCursor`, `AppliedSort` and `TotalCount`. No code changed.
## 0.5.0 - 2026-09-11

### API-breaking

- Rename `SortDirectionWire.Asc` to `ASC` and `SortDirectionWire.Desc` to `DESC`.
  The wire values remain `asc` and `desc`; parsing remains case-insensitive.

## 0.4.1 - 2026-09-10
### Changed

- `SearchQuery`'s and `PageRequest`'s documentation is rewritten subject-first, and two blocks are spaced. No behavior changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.4.0 - 2026-09-08
### Added

- `LIB_PAGINATION_SEARCH_NOT_SUPPORTED` (400, `validation_failure`) and its `PaginationFailures.SearchNotSupported()` factory, for a page request that carries a search query against a surface that declared no `SearchPolicy`.
### Changed

- `PageRequestNormalizer.Normalize` returns `LIB_PAGINATION_SEARCH_NOT_SUPPORTED` for a query against a surface with
  no `SearchPolicy`, where it threw `InvalidOperationException` before. A caller that sends a query to a list that
  does not search receives that failure instead of reaching the handler as an unhandled exception.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.0. This package's own surface is unchanged.
## 0.3.10 - 2026-09-08
### Changed

- The cursor is described as readable rather than opaque: base64url is an encoding, so whoever holds a cursor can
  read the boundary row's sort value. The README and the `KeysetCursor` remarks state what a host does about a
  personal sort column.
- The emptiness checks in `OpaqueKeysetCursorCodec` and `SearchQuery` call `Falsey()`.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.3.9 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.3.8 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.3.7 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.6 - 2026-08-09
### Changed

- Removed source comments citing a retired rule catalog from this package's argument guards. No behavior change.

## 0.3.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.ErrorCodes.Category bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.4 - 2026-08-05
### Fixed

- The release fingerprint no longer covers the nested provider packages that sit
  in subdirectories of this one. It had been hashing their source, including
  their version fields, even though this package neither compiles nor depends on
  them. Because they depend on this package, a bump here cascaded to them, and
  their new versions changed this package's fingerprint again - so the version
  could never settle and every commit was rejected for a package the change set
  had not touched. No shipped content changed.

## 0.3.3 - 2026-08-05
### Changed

- No shipped content changed. The release fingerprint moved for a reason that
  turned out to be the fingerprint's own scope rather than anything in this
  package or its dependencies; `0.3.4` records the correction.

## 0.3.2 - 2026-08-05
### Changed

- No shipped content changed. This package's release fingerprint covers every
  tracked source under its own folder tree, which includes the nested provider
  packages, so a documentation edit in
  `DcsvIo.D2.Pagination.EntityFrameworkCore` moves it and the version moves
  with it.
## 0.3.1 - 2026-08-04
### Changed

- `SearchQuery.Normalized` is case-folded, and `QueryToken` is the digest of
  that text with no transform of its own. 0.3.0 had it the other way round -
  the matched text unfolded and the fold confined to the token - and the token
  is the cursor's binding to its query, so it has to identify exactly the text
  a dialect matches and scores on. Folding the token alone let two spellings
  that fold together produce one token and two different patterns, so a cursor
  minted on one was accepted on the other and a relevance page applied a score
  boundary taken from a different scoring run. One fold over one string means a
  token can only be shared by queries that hand a dialect identical text. The
  fold is not what makes matching case-insensitive - that is a property of the
  dialect's operator - and its cost is stated at the type: a character whose
  invariant lowering the database's comparison does not reproduce stops finding
  rows carrying it.
- The whitespace a query collapse covers is `char.IsWhiteSpace`'s set, stated
  at `SearchQuery.Create`, because a provider leaf storing a pre-collapsed
  column has to collapse the same characters.
### Fixed

- A cursor whose primary key is the empty string encodes and decodes as
  ordinary data. A boundary row whose string sort column is empty is a value
  the server read out of its own table, and returning
  `LIB_PAGINATION_CURSOR_NOT_ENCODABLE` - declared an internal fault - alerted
  an operator about a listing that was working. The guard still rejects an
  absent primary key, which is a cursor the host built wrong or one whose JSON
  omitted the field.
## 0.3.0 - 2026-08-04
### API-breaking

- `SortFieldNames.Relevance` is renamed `SortFieldNames.RELEVANCE`, following
  the constant-naming convention every other library constant here uses.
### Changed

- `SearchQuery.Normalized` applies no case transform. It is matched by an
  operator that is already case-insensitive, against a column stored unfolded,
  so a .NET fold on this side could only subtract: for a character whose
  invariant lowering the database's own comparison does not reproduce, the
  folded text stops matching rows the caller's own text would have found. The
  fold now runs only over the input to `QueryToken`, which is what it was there
  for - a caller who retypes their query with different capitalization keeps
  their cursor.
- The minimum bound on a query is the longest run of letters and digits in it
  rather than its character count. A trigram index is reached through those
  runs, so a punctuation-only query cleared a character count and left the
  database scanning every reachable row and then scoring what survived. The
  upper bound is unchanged and still measures characters.
### Fixed

- A cursor whose sort field is `SortFieldNames.RELEVANCE`, presented with no
  query, is rejected with `LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY`, as a
  client-supplied relevance sort already was. The cursor codec is
  unauthenticated, so the sort field in a cursor is caller-supplied input, and
  relevance is never registered in a sort catalog - the request reached a
  catalog lookup that found no entry and returned a code the catalog declares
  an internal fault.
## 0.2.0

### Wire-breaking

- Paging failures that previously surfaced as an anonymous 400 now return their own
  `LIB_PAGINATION_*` code, and four of them are 500 rather than 400:
  `PAGE_POLICY_INVALID`, `SORT_CATALOG_EMPTY`, `SORT_FIELD_NOT_REGISTERED` and
  `CURSOR_NOT_ENCODABLE`. Those codes describe a host configuration or encode
  defect that the caller did not cause and cannot fix; returning 400 for them
  told a caller to fix a request that was not wrong.

### API-breaking

- `PageRequestNormalizer.Normalize` takes a `SortFieldAllowSet` in place of
  `IReadOnlySet<string>`, and gains an optional `SearchPolicy` before its
  trailing direction argument. A set of names cannot express a direction lock.

### Added

- `AllowedDirections`, `SortFieldSpec` and `SortFieldAllowSet`: an allow-set that
  records, per field, which directions it permits. A field locked to one
  direction resolves an omitted order to that direction and rejects the other one
  with `LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED` rather than silently correcting
  it. Constructing a specification permitting no direction throws, because it is a
  wiring defect in the host rather than caller traffic.
- `SortFieldNames.Relevance`: the wire sort field name the library defines, so an
  emitter and the runtime do not each hold a literal that can disagree.
- `SearchPolicy` and `SearchQuery`: query-length bounds and a validated,
  normalized query. `SearchQuery.Create` trims, case-folds under the invariant
  culture, collapses runs of inner whitespace, then bound-checks the result -
  the number of characters the caller typed. `Normalized` carries no pattern
  syntax; escaping belongs to a matching dialect.
- `SearchQuery.QueryToken`: SHA-256 over the UTF-8 bytes of the normalized query,
  rendered base64url without padding. It is a sameness check and nothing else.
- `KeysetCursor.QueryToken`: binds a cursor to the query that produced it, so a
  cursor presented against a different query is refused with
  `LIB_PAGINATION_CURSOR_QUERY_MISMATCH` rather than serving a page that is
  neither result. Absent on both sides is a match; present on one side only is
  not. The cursor format version does not move, because a cursor minted before
  the field existed decodes with it absent.
- `PageRequest.Query` and `NormalizedPageRequest.Search`: `PageRequest.Query` holds
  the query beside the cursor it must agree with, and `NormalizedPageRequest.Search`
  holds the validated query on the normalized result.
- `PaginationErrorCodes` and `PaginationFailures`, generated from
  `contracts/pagination-error-codes/pagination-error-codes.spec.json`. A caller can
  now branch separately on a bad sort field, a bad cursor, a bad limit and a stale
  cursor.

## 0.1.1 - 2026-07-26
### Changed

- Release-baseline reseed only. This package's sources, public API surface and behavior are unchanged; the committed `.release-fingerprint` was stale against a recompute and has been regenerated.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.0 - 2026-07-25

### Added

- Initial portable keyset pagination surface (`ListPagingPolicy`, `PageRequest`,
  `NormalizedPageRequest`, `KeysetCursor`, `PageResult<T>`, `PageRequestNormalizer`,
  `IKeysetCursorCodec`, `OpaqueKeysetCursorCodec`).
- **Multi-mode list page:** `SortSpec` + `SortDirection` + `SortDirectionWire`;
  `ListPagingPolicy.DefaultSort` is `SortSpec` (field + direction); `PageRequest`
  carries optional client `Sort` / `Order`; `PageResult<T>` surfaces `AppliedSort`
  + `TotalCount`; normalizer accepts caller allow-set (not a policy field).
- Platform defaults: DefaultLimit 50, MaxLimit 100, cursor Version 1.
- Over-max limit clamps; non-positive limit and invalid cursor Sort/Version fail closed.
