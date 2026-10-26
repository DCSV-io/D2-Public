# Changelog - DcsvIo.D2.Result

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.8 - 2026-10-20
### Changed

- The generated XML remarks on `D2ResultEnvelopeFieldNames`, `InputErrorWireShape` and `ErrorCodes` say that the
  TypeScript catalog is emitted from the same spec through the TypeScript emitter, in place of a path to a tool folder.
  No code changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.7 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.6 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
## 0.5.5 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
## 0.5.4 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
## 0.5.3 - 2026-09-26
### Changed

- The README factory table closes the `Canceled` row. The `ErrorCodeEntry` sunset documentation and the `error-codes-source-gen` and `error-codes-emit` READMEs describe the current contract without counts or all-caps emphasis. No code changed.
## 0.5.2 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
## 0.5.1 - 2026-09-08
### Changed

- The `D2ResultEnvelopeFieldNames` documentation describes the envelope as the gateway response shape a host serializes to its clients rather than by a D2 topology name. The generated xmldoc changes with it; no field name, value or wire shape changed.
## 0.5.0 - 2026-09-06
### Added

- `ErrorCodes.GEN_FORGERY_REFUSED`, the `D2Result.ForgeryRefused` and `D2Result<TData>.ForgeryRefused` factories and the
  `D2Result.IsForgeryRefused` member, generated from the new `GEN_FORGERY_REFUSED` entry in
  `contracts/error-codes/error-codes.spec.json`. The code is 403 `policy_denied` and reuses
  `TK.Common.Errors.FORBIDDEN`.
## 0.4.0 - 2026-08-31

### Added

- `D2Result.NoContent()` and `D2Result<TData>.NoContent()` - success factories
  that set HTTP `204`. A caller that implements a protocol whose success response
  has no body now sets that status through a factory rather than building the
  result by hand.

## 0.3.0 - 2026-08-06

### Wire-breaking

- `GEN_PARTIAL_SUCCESS` maps to HTTP `503` instead of `207`. The status reaches
  the wire through `ErrorCodes.GetHttpStatus`, the error-code registry, and
  `problem.Status` in the problem-details customizer, so a consumer that
  branched on `207` no longer sees it, and one that treats `2xx` as done no
  longer treats a half-applied write that way. `GEN_SERVICE_UNAVAILABLE` shares
  the status; the error code is what tells the two apart.

### API-breaking

- `D2Result<TData>.PartialSuccess(...)` returns a result whose `Success` is
  `false`. It previously returned `true`.

  Why: `SomeFound` and `PartialSuccess` treated a partial outcome in opposite ways.
  `SomeFound`, the read-side partial, already returned `Success` as `false`;
  `PartialSuccess`, the write-side partial and the more dangerous direction,
  returned it as `true`. A caller writing the ordinary `if (result.Failed)`
  therefore treated a write that applied to some targets and not others as done.

  What a consumer must now do: nothing, if it wants the safe reading - the
  existing `Failed` check now catches a partial write and treats the operation
  as not done. A consumer that legitimately accepts a partial outcome must add
  an explicit `result.IsPartialSuccess` test at that call site and proceed on
  it; `result.Data` still lists which targets were reached. A consumer that
  relied on `Success` being `true` to continue will now stop, which is the
  intended direction of the break.

  Unchanged: the error code, the `partial_success` category, the default
  `TK.Common.Errors.PARTIAL_SUCCESS` message, and the `IsPartialSuccess`
  discriminator. `IsTransientRetryable` is keyed on the error code rather than
  the status, so the new `503` does not make a partial write automatically
  retry-eligible.

## 0.2.2 - 2026-08-04
### Changed

- Dependency update: `DcsvIo.D2.I18n.Keys` gained the `TK.Common.Pagination.*`
  keys.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
## 0.2.1 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Dependency update: DcsvIo.D2.ErrorCodes.Category bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
## 0.2.0 - 2026-07-22

### Changed

- Package release 0.2.0 after dependency updates and a baseline reseed; the public API surface did not change.
## 0.1.2 - 2026-07-21
### Wire-breaking

- Generic `ErrorCodes` constants and stamped wire codes use the `GEN_*` owner prefix (e.g. `GEN_NOT_FOUND`).
- The generated booleans drop the prefix and keep the rest of the code name (`IsNotFound` for `GEN_NOT_FOUND`, never
  `IsGenNotFound`).

### Changed

- Error-codes SourceGen always enforces DomainPrefix / Owner / DomainToken on the generic shell.

## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.ErrorCodes.Category bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
