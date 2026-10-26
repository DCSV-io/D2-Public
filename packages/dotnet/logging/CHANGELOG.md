<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Logging

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.36 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.21. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.1.35 - 2026-10-20
### Changed

- The csproj comment on the `InternalsVisibleTo` grants is removed. The set of grants is unchanged.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.1.34 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.33 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.32 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.1.31 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.16. This package's own surface is unchanged.
## 0.1.30 - 2026-09-27
### Changed

- The README's and the field policy document's prose lines over 120 characters are re-wrapped at that width; no wording
  changed. No code changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.1.29 - 2026-09-26
### Changed

- The field-policy document describes the connection-remote IP policy in terms of a gateway or reverse proxy and the
  ingress boundary, and drops the field and cluster counts. The `D2RequestContextEnricher` remarks drop the field
  counts. No code changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.1.28 - 2026-09-13
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.27 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.13. This package's own surface is unchanged.
### Fixed

- The `D2RequestContextEnricher` remarks said each destructured `ActorChain` entry's session id is masked by
  `[RedactData(SecretInformation)]` on `ActorEntry.SessionId`. The enricher passes the diagnostic context a
  `LogSafeActorEntry` projection whose only session member is `SessionIdHash`, and `LogSafeIdentifiers` states that the
  attribute alone is not sufficient for a sequence element. The remarks now describe the projection and keep the
  attribute as a second protection for other capture sites.
- Row 16 of the field-policy document in this package's documentation folder described the emitted `ActorChain` as an
  array of `ActorEntry` objects carrying a redacted `SessionId`. It now describes the shipped shape: an array of the
  log-safe projection, every member verbatim except the session id, which appears as `SessionIdHash`, plus the
  `ActTruncated` marker.
## 0.1.26 - 2026-09-10
### Changed

- The packed README has no outbound links and refers to the field-policy document by name instead: the registry
  renderer cannot resolve a relative repository path.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.1.25 - 2026-09-09
### Changed

- `LogSafeIdentifiers`, `LogSafeActorEntry`, `D2RequestContextEnricher` and the field-policy
  document state the reason a session id never reaches a sink. Each previously called
  the id the session cookie's value and a replayable bearer secret, which is not the case:
  the session cookie contains an encrypted frame and no request path accepts a bare session id as
  an authenticator. The id is the handle every record about one session shares, so a log line
  that contains it joins that session across every log, ties it to one person, and identifies that person's
  session to anything able to end it. The digesting is unchanged - same domain prefix, same
  truncation, same output - and no type, member, constant value or wire format changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.11. This package's own surface is unchanged.
## 0.1.24 - 2026-09-08
### Changed

- The comment on the actor-chain depth cap states the fact plainly instead of closing on a metaphor, and the
  `LogSafeIdentifiers` remarks stop using capitals for emphasis. No behavior changed.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.4.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.1.23 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.4.0. This package's own surface is unchanged.
## 0.1.22 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.3.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.1.21 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.20 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.2.1. This package's own surface is unchanged.
## 0.1.19 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.2.0. That release changes `AddD2MutualTls` validation: an empty
  `AllowedWorkloads` list now admits no peer instead of failing the host build, and an entry that is not lowercase with
  no whitespace is refused at build. This package's own surface is unchanged.
## 0.1.18 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.AspNetCore` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.17 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.16 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.15 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.14 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.13 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.12 - 2026-08-06
### Changed

- Dependency update cascade: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.11 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.10 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.9 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
### Fixed

- Security: the request-logging enricher no longer writes session identifiers to a sink. A session id is the value of
  the session cookie, so a log line containing it was a replayable credential. `SessionId` and `ImpersonationSessionId`
  now leave as stable non-reversible correlation digests under the names `SessionIdHash` and
  `ImpersonationSessionIdHash`, and each `ActorChain` entry's session id is projected to a digest before the chain is
  passed on. Correlation across log lines and services is unchanged; the raw value is gone. Operators with saved
  queries on the old `SessionId` / `ImpersonationSessionId` fields must repoint them to the `*Hash` fields.
## 0.1.8 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs
  replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-07-24

### Changed

- Package release 0.1.7 (dependency cascade / baseline reseed; no public API surface change in this release).
## 0.1.6 - 2026-07-24

### Changed

- Package release 0.1.6 (dependency cascade / baseline reseed; no public API surface change in this release).
## 0.1.5 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.AspNetCore bumped.
