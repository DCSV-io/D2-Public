<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Auth.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.16 - 2026-10-21
### Changed

- A remark on `JwksKeySetSnapshot` says that its equality compares `Keys` by reference. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.15 - 2026-10-20
### Changed

- The generated XML docs of `Audiences`, `WellKnownAudiences` and `Scopes` no longer name a specific host service, and
  the `Audiences` examples use the audience `Orders` in place of the earlier sample audience. No code changed.
- The README example scope is now `Scopes.Anon.Public.Health` in place of `Scopes.Auth.Password.Change`, and the
  `IAmbientRequestScopeAccessor` row no longer names the HTTP and gRPC adapter classes. No code changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.14 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.13 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.12 - 2026-10-17
### Changed

- Punctuation in the `InvalidOperationException` message of `ForwardedJwt.RevealForForwarding`: a dash is now a hyphen.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.11 - 2026-10-03
### Fixed

- `ImpersonationKind`'s documentation no longer refers to scopes and organization groups that no scope catalog
  declares (`auth.user.impersonate`, "Staff + Admin orgs", "mgmt + dev"). It now says what each flavor records and
  that the deploying product's scope catalog decides who may start each. `ActorEntry`'s `ImpersonationKind`
  parameter and the README say the same. Documentation only.
- `ActorEntry.ToString()` prints `SessionId` as `[REDACTED: SecretInformation]`, in a nested `Act` too. The
  compiler-written record printer printed the impersonation session id verbatim, so a plain `{x}` log capture, an
  interpolation or a printed `IAuthContext.ActorChain` element wrote the raw id while only `{@x}` destructuring masked
  it. Every other member prints as before, and reading `SessionId` still returns the value. No member, type or wire
  format changed.
- The README no longer says `SessionId` is never printed. It lists the paths that mask it - `{@x}` destructuring,
  `ToString`, a plain `{x}` capture and an interpolation - and says that `System.Text.Json` writes the raw value
  when it serializes an `ActorEntry` or the actor chain. Documentation only.
- The README's forwarded-JWT example says the holder is registered request-scoped by the host's HTTP and gRPC
  transport registrations, rather than listing registration methods this package does not ship.
- The README's Tests section lists only suites under `packages/dotnet/tests`, which `dotnet test packages/dotnet/tests`
  runs. The forwarded-JWT suite list included suites that run elsewhere, and its log-capture line said the raw bytes
  surface under any capture form - the opposite of what that suite asserts.
## 0.5.10 - 2026-09-27
### Changed

- The README's and the design notes' prose lines over 120 characters are re-wrapped at that width; no wording changed.
  No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.5.9 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.8 - 2026-09-11
### Changed

- README and `ScopesEmitter` prose rewritten subject-first. No surface, constant or diagnostic changed.
## 0.5.7 - 2026-09-10
### Changed

- The README lists `JwtClaimTypes.ROOT_ORG_ID` and `JwtClaimTypes.ACCESSIBLE_ORG_IDS` with their wire values, and links out of the packed README nowhere.
- `RoleAuthority` drops a redundant `using System;`, and `SessionLivenessCacheKey` spaces a control-flow block. No behavior changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.6 - 2026-09-09
### Changed

- `SessionLivenessCacheKey` and `ActorEntry.SessionId` state the true reason a session id is
  kept out of logs and out of a reader's reach. Both previously called the id a live bearer
  credential replayable as a session cookie, which the session boundary does not do: the session cookie
  contains an encrypted frame with a fresh nonce per seal, sealed under its own encryption
  domain, and no request path accepts a bare session id as an authenticator. The id is a
  lookup handle - a reader of the store, the invalidation channel or the logs learns which
  sessions exist and when each ends, can correlate one session across records, and if it can
  also write to the store can end one named person's session. The key still contains the id
  because the readers need it back. No type, member, constant value or wire format changed.
## 0.5.5 - 2026-09-08
### Changed

- `SessionLivenessCacheKey`'s remarks state the present fact - composing and parsing the key both live here, so a change
  to either half cannot be made on one side only - and drop the account of how the components that used to compose it
  disagreed. That account belongs in this changelog, not in xmldoc a consumer hovers.
- The scopes, audiences, JWT-claims and protocol-audiences generator READMEs contain a validation ledger that lists what
  each emitter is validated against, and the protocol-audiences generator has a README at all. No emitted output
  changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.5.4 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.5.3 - 2026-09-04
### Changed

- `OrgType.Support` documents what it represents: a root organization holding the platform operator's
  customer-support staff, who work from it rather than from a customer organization.
  Documentation only - the member and its ordinal are unchanged.
## 0.5.2 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.5.1 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.5.0 - 2026-08-18

### API-breaking

- `Sessions.ISessionLivenessTracker` is removed. No inbound request path called it: the check it
  served was taken out of the HTTP middleware and the gRPC interceptor, because a per-hop
  shared-store read on every request of every service is a product decision the auth libraries
  should not make, and because only the service that owns the session store can answer
  the question properly. A registered seam that nothing reads invites someone to wire the per-hop read
  back in, so this release removes it rather than deprecating it. The boundary owning the session store now composes
  that step into its own pipeline. Callers: none in this repository.

### Added

- `IForwardedJwtAccessor.Clear()`, with a default-free implementation on
  `MutableForwardedJwtAccessor`. A token is captured for outbound forwarding at the end of the
  validating step, because validation is decidable from the token alone and runs at every hop; a
  gate composed after that step - a boundary's revocation check - can still refuse, and calls this
  so the credential it refused is not forwardable. The capture cannot simply move later: a
  downstream hop composes no such gate and still needs the token forwarded.

### Changed

- The README's source map documents `Sessions/SessionLivenessCacheKey.cs` in place of the removed
  read-side port, and states what the key is actually for: the invalidation broadcast a revocation
  produces, which the token-exchange cache filters on to purge the exchanged tokens it holds for
  that session.
## 0.4.1 - 2026-08-17

### Changed

- `ISessionLivenessTracker` documentation now states that the inbound runtime makes no per-hop
  liveness check. Validating a token and deciding whether the session behind it was revoked are
  different jobs: validation is decidable from the token plus cached signing keys and runs at every
  hop, while revocation needs an external lookup and is asked once, at the boundary that owns the
  session store, by a step that boundary composes. The contract itself is unchanged - this corrects
  a statement that had stopped being true.

## 0.4.0 - 2026-08-12
### API-breaking

- `Scopes.GetActionSensitivity(string)` is removed, along with the
  `actionSensitivity` field it read from `contracts/auth-scopes/scopes.spec.json`.
  How sensitive an action is belongs to the operation rather than to a scope -
  one scope can be granted to several operations - so it is declared on the
  operation and reaches the endpoint as route metadata, and a scope entry no
  longer carries a second declaration of the same concept. Callers: none in this
  repository.

### Changed

- `ActionSensitivity` documentation no longer calls the enum a per-scope
  discriminator that lives on every scope. It states what the type is after the
  removal above: how dangerous an action is if it succeeds, declared on the
  operation and carried as endpoint metadata. The README source map says the
  same. Documentation only - no member, name or value changed.

## 0.3.3 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.2 - 2026-08-08

### Changed

- `ImpersonationKind.Force` documentation no longer states that the audit log is
  visible to the impersonated user in their account activity. A forced
  impersonation is not surfaced to the subject; a durable record is kept for
  internal audit. Documentation only - no member, name or value changed.

## 0.3.1 - 2026-08-06

### Changed

- `Role` remarks and the README no longer name specific product services when
  illustrating the authority order. Comment and documentation text only: no
  exported symbol, type, value or wire format changed.

## 0.3.0 - 2026-08-05

### API-breaking

- The documented position of `Role` is reversed. The enumeration previously stated that
  roles do NOT form a hierarchy. It now states two separate facts: capability - what a
  role may do - is still unordered and is declared per (role, org type) by each scope's
  `grantedTo`; authority over another member - who may act on whom - is a total order. A
  consumer that read the old remark as covering both questions took one answer to apply
  to both.

### Added

- `RoleAuthority` - the total order over `Role`, declared once. `RankOf(role)` returns 0
  for `Auditor`, 1 for `Agent`, 2 for `Officer` and 3 for `Owner`, and throws
  `ArgumentOutOfRangeException` for a value outside the enumeration rather than inventing
  a rank. `TryRankOf(role, out rank)` is the non-throwing form for a caller that may be
  handed a value from outside the enumeration - a stored role, say, which
  `Enum.TryParse` will happily produce from a numeric string. `Outranks(actor, target)`
  is true when the actor's rank is strictly greater, so the relation is irreflexive and
  every declared pair is comparable in exactly one direction; a value outside the
  enumeration has no authority, so `Outranks` returns false on either side, because this is an
  authority decision and an authority decision denies rather than throws.
- `JwtClaimTypes.ROOT_ORG_ID` (`d2_root_org_id`) and `JwtClaimTypes.ACCESSIBLE_ORG_IDS`
  (`d2_accessible_org_ids`) - the claims carrying a caller's root organization and the
  set of organizations they may reach.

## 0.2.1 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.0 - 2026-07-28
### Added

- `Sessions.SessionLivenessCacheKey` - a static type that is now the single declaration of the session-liveness cache
  key shared across assemblies. Exposes `DEFAULT_PREFIX` (`"session:"`), `Compose(prefix, sessionId)`,
  `HasPrefix(key, prefix)` and `TryParse(key, prefix, out sessionId)`. The key shape is `{prefix}{sessionId:N}` - the
  session id rendered as 32 lowercase hex chars with no hyphens. Composing and parsing live together so composing and
  parsing cannot drift apart; `TryParse` matches the `N` rendering exactly and rejects a hyphenated or brace-wrapped id
  rather than accepting it silently.

### Fixed

- Security: `ActorEntry.SessionId` now has `[RedactData(RedactReason.SecretInformation)]`, so structural
  destructuring masks it instead of printing a live session credential. The SECRET regime is deliberately distinct from
  the PII regime. No type, member, constant value or wire format changed.

## 0.1.5 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs
  replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced
  by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-23
### Changed

- Public health scope description repair (`anon.public.health` catalog text) for mojibake-free
  consumer-facing scope catalog prose; fingerprint reseeded with scopes catalog content.

## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-20

### Changed

- Package release 0.1.2 (dependency cascade / baseline reseed; no public API surface change in this slot).
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
