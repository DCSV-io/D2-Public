<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Outbox

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.11 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.10 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.7 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.6 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.5.5 - 2026-09-26
### Changed

- `IMessageOutbox.StageAsync` documents `ValidationFailed` for a payload that carries a value marked personal, including a value held in a member declared as `object`, which the storage half now reads by its runtime type. The README says the same. No code changed in this package.
## 0.5.4 - 2026-09-26
### Changed

- `IMessageOutbox.StageAsync` documents `ValidationFailed` for a payload type that declares a personal value, which the
  storage half now refuses. The no-personal-data precondition remains the caller's responsibility for unmarked members.
  No code changed in this package.
## 0.5.3 - 2026-09-26
### Changed

- The `IMessageOutbox` summary describes what the outbox stages rather than who calls it. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.2 - 2026-09-11
### Changed

- `IMessageOutbox.StageAsync` states the no-personal-data precondition on the payload that this package does not check, and what it costs when a consumer breaks it: a personal value staged here is stored as plaintext on a row exempt from anonymization and outlives the subject's erasure for as long as the storage half's retention window keeps the row. The README says the same where a consumer reads it. The precondition was previously stated only in the storage half's internal remarks.
- `OutboxMessage`, `StagedOutboxMessage` and the README state their facts subject-first. No behavior changed.
## 0.5.1 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.0 - 2026-09-08
### Added

- `OutboxOptions.PublishedRetention` (seven days) and `OutboxOptions.AbandonedRetention` (ninety days) bound how long a settled row is kept. Neither accepts a value meaning never.
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.4.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.4.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.1. This package's own surface is unchanged.
## 0.4.0 - 2026-08-24

### API-breaking

- `IMessageOutbox<TDb>.StageAsync(kind, payload, ct)` no longer defaults `ct`.
  A call written as `StageAsync(kind, payload)` stops compiling and passes its
  token: `StageAsync(kind, payload, ct)`. The public-API analyzer allows the
  optional parameter only on the overload with the most parameters (RS0027), so
  the id-taking overload added below takes the default and this one gives it up.

### Added

- `IMessageOutbox<TDb>.StageAsync` overload taking a caller-minted
  `OutboxMessageId`, for a payload that has to carry the id of the row it is
  staged in. An acknowledgment can then state which message it responds to.

## 0.3.0 - 2026-08-22

### API-breaking

- An outbox is identified by its database's application-layer context interface
  rather than by a string name. Every registration and every consumer uses that
  interface as a type parameter, and the compiler checks it.
- `IMessageOutbox` is removed. `IMessageOutbox<TDb>` replaces it with the same
  single member. A caller that took `[FromKeyedServices("my-outbox")]
  IMessageOutbox` takes `IMessageOutbox<IMyDbContext>` with no service key.
- `AddD2Outbox(string outboxName, Action<OutboxOptions> configure)` is removed.
  `AddD2Outbox<TDb>(Action<OutboxOptions> configure)` replaces it. It configures
  `OutboxOptionsFor<TDb>` rather than options named by the string.
- `AddD2OutboxDispatcher<TDispatcher>(string outboxName)` is removed.
  `AddD2OutboxDispatcher<TDb, TDispatcher>()` replaces it. It registers
  `TDispatcher` as a singleton and an `IOutboxDispatcherBinding<TDb>` over it,
  where the keyed `IOutboxDispatcher` registration used to be. `IOutboxDispatcher`
  itself is unchanged, so no dispatcher class changes.
- `OutboxOptions` is no longer `sealed`, so `OutboxOptionsFor<TDb>` can derive
  from it. Its members, defaults, annotations and `SECTION` are unchanged.

### Added

- `IOutboxDispatcherBinding<TDb>` - one `IOutboxDispatcher`, bound to the database
  whose relay may hand it messages. The relay for a database resolves the bindings
  carrying its own `TDb` and reaches no other module's transports.
- `OutboxOptionsFor<TDb>` - one database's `OutboxOptions`. It adds no member; the
  type parameter replaces an options name as the key by which the framework's options pattern separates two
  databases' settings. A host binds the
  `LIB_OUTBOX` section onto it and attaches its own
  `ValidateDataAnnotations().ValidateOnStart()`.

## 0.2.3 - 2026-08-22

### Changed

- The README places the relay in `DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres`
  rather than in `DcsvIo.D2.Outbox.EntityFrameworkCore`; the relay moved to the `.Postgres` package,
  and its registration example includes the relay's own call. No member was added,
  removed or renamed, and no behavior changed.
- Dependency update: DcsvIo.D2.Time bumped.

## 0.2.2 - 2026-08-19
### Changed

- The floor-only `[Range]` annotations on `OutboxOptions` (`RelayTick`, `RetryBackoffBase`, `RetryBackoffCeiling`) use
  `TimeSpanRangeBounds.NO_CEILING` as their upper bound instead of repeating `TimeSpan.MaxValue` written out. Same
  floors, same accepted values; the annotation now says why the maximum is there.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.1 - 2026-08-19

### Changed

- The `outboxName` guards on `AddD2Outbox` and `AddD2OutboxDispatcher<T>` are written
  `outboxName.ThrowIfFalsey()` rather than `ArgumentException.ThrowIfNullOrWhiteSpace(outboxName)`.
  Same exception types for the same inputs - `ArgumentNullException` for null, `ArgumentException`
  for empty or whitespace - so no caller sees a difference; it is the project's single required-
  argument guard for a string, which keeps one form to recognize rather than two.
- `DcsvIo.D2.Utilities` is now a declared `<ProjectReference>` rather than one reached through
  `DcsvIo.D2.Time`. The package already compiled against it - `OutboxOptions` carries
  `[RedactData(Reason = RedactReason.SecretInformation)]` from it - so the dependency existed and
  was not declared, and the resolved graph is unchanged because it was already transitive.

## 0.2.0 - 2026-08-18

### Added

- Data-annotation bounds on every `OutboxOptions` member, so a consumer's
  `ValidateDataAnnotations().ValidateOnStart()` refuses a misconfigured relay at boot instead of
  letting it fail silently at run time. Each one prevents a failure that is otherwise invisible: a
  `BatchSize` or `RetryLimit` below one makes the claim refuse or abandon on every tick while the
  messages sit staged, a non-positive `RelayTick` or backoff makes the timer throw, and
  `RelayAdvisoryLockKey` / `RelayConnectionString` are `required`, which is a compile-time
  construct the configuration binder does not apply - a host that binds the section and never
  calls `AddD2Outbox` reaches the relay with a zero lock key and a null connection string.

## 0.1.0 - 2026-08-17
### Added

- First release. The database-agnostic half of the transactional outbox:
  `IMessageOutbox` and `IOutboxDispatcher`; the `OutboxMessage` state family
  (`StagedOutboxMessage`, `PublishedOutboxMessage`, `AbandonedOutboxMessage`)
  with `OutboxMessageStatus` and `OutboxMessageId`; `OutboxOptions` carrying the
  relay lock key, relay connection string, tick, batch size, retry limit and
  backoff curve; and `AddD2Outbox` / `AddD2OutboxDispatcher<T>`.
- Every registration takes the outbox's name. An outbox belongs to a database
  rather than to a host, so a host composing two modules over two databases
  registers two of everything and each states which one it means.
- `StagedOutboxMessage.LastFailureCode`, so a message that is still retrying
  records why on every attempt rather than only on the one that abandons it.
- `OutboxOptions.RelayConnectionString` is marked
  `[RedactData(Reason = RedactReason.SecretInformation)]`; it carries a password.
