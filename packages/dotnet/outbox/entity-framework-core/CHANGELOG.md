<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Outbox.EntityFrameworkCore

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.8.9 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.8.8 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.8.7 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.8.6 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.8.5 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.8.4 - 2026-09-28
### Fixed

- `EfOutboxStore.ClaimDueAsync` returns `ValidationFailed` for a lease shorter than one millisecond. A PostgreSQL
  timestamp keeps only microseconds, so a positive lease below that was stored as `now` and the claimed message was
  claimable again at once, which the 0.8.3 zero-lease refusal did not catch. `IOutboxStore.ClaimDueAsync` documents the
  one-millisecond floor. `OutboxRelay` passes `RetryBackoffCeiling`, which its options already hold to at least one
  millisecond, so the relay is unaffected.
## 0.8.3 - 2026-09-28
### Fixed

- `EfOutboxStore.ClaimDueAsync` returns `ValidationFailed` for a zero lease, as it already did for a negative one. A
  zero lease stamped the next attempt at `now`, which the claim predicate matches, so a message still being dispatched
  was claimable again at once. `IOutboxStore.ClaimDueAsync` documents the lease as positive. `OutboxRelay` passes
  `RetryBackoffCeiling`, which its options already hold to at least one millisecond, so the relay is unaffected.
## 0.8.2 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.8.1 - 2026-09-26
### Fixed

- `EfMessageOutbox.StageAsync` refuses a `[RedactData]` marker on every member the payload serializer writes. The check read public properties only, so a marker on a `[JsonInclude]` field or non-public property, on a derived type a `[JsonDerivedType]` attribute names, or on a value held in a member declared as `object` passed as `Ok`. It now reads the serializer's own metadata for the payload's members, and walks the values of `object`-typed members by their runtime types; such a value whose type has its own converter and itself holds an `object` member is refused. The check runs after the serializer accepts the payload. `OutboxMessageRow.Payload` and the README describe the check.
## 0.8.0 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.4. This package's own surface is unchanged.
### Fixed

- `EfMessageOutbox.StageAsync` returns `ValidationFailed` and adds no row when the payload type declares a
  `[RedactData]` marker on the type, a public property or a public constructor parameter, or on a type reached through a
  property, an array element or a generic argument. The row is exempt from anonymization, so such a value outlived the
  subject's erasure for the retention window with nothing detecting it. `OutboxMessageRow.Payload` and the README
  describe the check.
- `EfOutboxStore.ClaimDueAsync` returns `Conflict` when another claimant wrote a row of the batch between this call's
  read and its claim, and clears the change tracker. The `DbUpdateConcurrencyException` fell into the general failure
  branch and returned `ServiceUnavailable`, which `IOutboxStore.ClaimDueAsync` documents as the database not responding.
  The interface's `<returns>` text states the new result.
## 0.7.4 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.7.3 - 2026-09-26
### Changed

- `EfOutboxStore` tests an empty claim or purge batch with `Falsey()` from `DcsvIo.D2.Utilities` rather than a count comparison. Behavior is unchanged.
## 0.7.2 - 2026-09-11
### Changed

- `OutboxMessageRow`, `EfOutboxStore`, `IOutboxStore` and the README state their facts subject-first. No behavior changed.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.2. This package's own surface is unchanged.
## 0.7.1 - 2026-09-10
### Added

- `OutboxMessageRowConfiguration` maps two partial indexes for the retention sweep: `ix_outbox_messages_published_at` filtered to `status = 'Published'` and `ix_outbox_messages_abandoned_at` filtered to `status = 'Abandoned'`. A consumer applying this configuration owns a migration that creates them.
### Changed

- `OutboxMessageRow.Payload` states in its remarks that nothing enforces the no-personal-data precondition its
  `[KeepOnAnonymization]` reason rests on, and states where the consumer checks it: the exemption is what keeps the row
  out of the anonymization validator's reach, so no build gate reports a payload carrying a personal value.
- The README lists the partial indexes `OutboxMessageRowConfiguration` maps and what each one serves.
- `EfOutboxStore.PruneSettledAsync` returns `D2Result<int>.Ok()` for the nothing-due arm rather than passing the parameter's own default. No behavior changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.7.0 - 2026-09-08
### Added

- `IOutboxStore<TDb>.PruneSettledAsync` deletes published and abandoned rows past their retention window, bounded per call. A staged row is never deleted by age.
- Counter `d2.outbox.pruned` records the rows the retention sweep deleted.
### Changed

- `OutboxMessageRow` declares its erasure disposition: the row is `IExemptFromAnonymization` and `Payload` carries a
  `[KeepOnAnonymization]` reason. The payload refers to subjects by identifier and contains no personal data, no secret,
  no code and no hash, and the README says so.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.6.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.4.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.2. This package's own surface is unchanged.
## 0.6.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Outbox` bumped to 0.4.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.1. This package's own surface is unchanged.
## 0.6.0 - 2026-08-24

### API-breaking

- `EfMessageOutbox.StageAsync(kind, payload, ct)` no longer defaults `ct`, which
  follows the same change on `IMessageOutbox<TDb>` in `DcsvIo.D2.Outbox` 0.4.0.
  A call written as `StageAsync(kind, payload)` stops compiling and passes its
  token: `StageAsync(kind, payload, ct)`.

### Added

- `EfMessageOutbox` implements the id-taking `StageAsync` overload. The minting
  overload delegates to it, so both take one path. A default `OutboxMessageId`
  is refused with `ValidationFailed`.

## 0.5.0 - 2026-08-22

### API-breaking

- An outbox is identified by its database's application-layer context interface
  rather than by a string name, and this package is where that identity is bound
  to a concrete context.
- `IOutboxStore` is removed. `IOutboxStore<TDb>` replaces it with the same
  members.
- `EfOutboxStore<TDbContext>` is removed. `EfOutboxStore<TDb, TDbContext>`
  replaces it, where `TDbContext : DbContext, TDb`. The body is unchanged.
- `EfMessageOutbox<TDbContext>` is removed. `EfMessageOutbox<TDb, TDbContext>`
  replaces it under the same constraint. The body is unchanged.
- `AddD2OutboxEntityFrameworkCore<TDbContext>(string outboxName)` is removed.
  `AddD2OutboxEntityFrameworkCore<TDb, TDbContext>()` replaces it. Both seams are
  registered as plain scoped services with `TryAdd` rather than keyed by a name.
  The `TDbContext : DbContext, TDb` constraint is what the name never had: a
  context bound under an interface it does not implement no longer compiles, so a
  handler cannot be handed another module's unit of work.

### Added

- `AddD2OutboxEntityFrameworkCore<TDb, TDbContext>` records the interface-context
  pair in both directions and refuses either collision with an
  `InvalidOperationException` whose message states the interface and the context on both sides.
  A `TDbContext` already registered under a different `TDb` is refused because one
  concrete context may implement two application-layer interfaces, which the
  `TDbContext : DbContext, TDb` constraint admits, and accepted that pair would
  put two stores and two relays over one table, each relay holding its own
  advisory lock and claiming rows the other has to dispatch. A second `TDbContext`
  under a `TDb` already registered is refused because both seams are registered
  under the interface alone, so the second call's `TryAdd` would be dropped and
  every handler taking `IMessageOutbox<TDb>` would stage onto the first context's
  change tracker while the host passed the second to the call. The same pair twice is still
  idempotent, a refused call records neither half, and two contexts under two
  interfaces still register. The refusals enforce one context type per
  interface and one interface per context type; two context classes mapping the
  one physical `outbox_messages` table are types DI cannot tell apart, and avoiding
  that stays the host's responsibility.

### Changed

- The `DcsvIo.D2.Utilities` project reference is dropped. It carried
  `ThrowIfFalsey` for the outbox-name guard, and no type here references it any more.

## 0.4.0 - 2026-08-22

### API-breaking

- `OutboxRelay<TDbContext>`, with its `NO_DISPATCHER_MARKER` and
  `DISPATCHER_THREW_MARKER`, moved to
  `DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres` and is no longer part of this
  package's surface. The relay drains the table under a PostgreSQL advisory
  lock, so this package carried a reference to
  `DcsvIo.D2.EntityFrameworkCore.Postgres` and every consumer of the row
  inherited a PostgreSQL driver it had no use for. A consumer that runs a relay
  adds the new package and calls the relay type by its new namespace; a consumer
  that only stages messages changes nothing.
- `AddD2OutboxEntityFrameworkCore<TDbContext>` no longer registers the relay as
  a hosted service. It registers the store and the staging seam, both keyed by
  the outbox name as before. A host that delivers its staged messages calls
  `AddD2OutboxRelayPostgres<TDbContext>` from the new package under the same
  name; without that call, messages are staged and never dispatched.

### Changed

- The package no longer references `DcsvIo.D2.EntityFrameworkCore.Postgres`,
  `DcsvIo.D2.Context.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions` or `Microsoft.Extensions.Options`.
  Each was there for the relay alone.
- `Microsoft.EntityFrameworkCore.Relational` is now a declared dependency.
  `OutboxMessageRowConfiguration` calls `ToTable`, `HasColumnName` and
  `HasDatabaseName`, which are relational APIs; the package had been receiving
  them transitively through a provider it no longer references.
- Dependency update: DcsvIo.D2.Time bumped.

## 0.3.3 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.EntityFrameworkCore.Postgres bumped.
- Dependency update: DcsvIo.D2.Outbox bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.2 - 2026-08-19

### Changed

- `OutboxMessageRowMapper`'s type summary now states why its write path is `ProjectOnto` rather
  than the conventional `ToRow`. Every write here applies to a row the relay already loaded and the
  change tracker already watches, and the concurrency token is that tracked instance, so returning
  a fresh row and assigning it would either detach the token or add a second entity for the same
  key. A reader who went looking for the conventional `ToRow` / `ToNewRow` pair previously found
  one half and had to read the file to learn the other name. Documentation only - no member was
  added, removed or renamed, and no behavior changed.

## 0.3.1 - 2026-08-19

### Changed

- The `outboxName` guards on `AddD2OutboxEntityFrameworkCore<TDbContext>` and the
  `OutboxRelay<TDbContext>` constructor are written `outboxName.ThrowIfFalsey()` rather than
  `ArgumentException.ThrowIfNullOrWhiteSpace(outboxName)`. Same exception types for the same
  inputs - `ArgumentNullException` for null, `ArgumentException` for empty or whitespace - so no
  caller sees a difference; it is the project's single required-argument guard for a string, which
  keeps one form to recognize rather than two. `DcsvIo.D2.Utilities` was already a declared
  dependency of this package.

## 0.3.0 - 2026-08-18

### Added

- `OutboxTelemetry.SR_DispatchDurationSeconds` - a `d2.outbox.dispatch_duration` histogram in
  seconds, tagged with `kind`, recorded around every dispatcher call including the one that
  threw. The counters could say the published rate fell and nothing about why: one tick claims up
  to `BatchSize` messages and awaits a dispatcher per message, and the tick is a fixed cadence
  rather than a queue, so a dispatcher that slows from ten milliseconds to eight hundred grows a
  backlog silently until the abandonment alert fires an hour later. A staged kind with no
  dispatcher records nothing - nothing was called, and a zero-second sample would lower every
  percentile. It carries `kind` alone: the failure class is already recorded on the error
  counters, and splitting latency by outcome would leave each bucket with too few samples to read.

### Fixed

- The README's metric table listed `kind` as the only tag and asserted it in the line above the
  table, so the `outcome` dimension the abandonment alert is built on was absent from the one
  document an operator building that alert reads.

## 0.2.0 - 2026-08-18

### Added

- `OutboxTelemetry.TAG_OUTCOME` and the closed `OutboxTelemetry.Outcome` value set
  (`no_dispatcher`, `dispatcher_threw`, `dispatcher_refused`), carried on both error counters.
  `d2.outbox.abandoned` is the signal an operator's alert is built on, and it could say that a
  message was abandoned but not why: a rolling deploy short a dispatcher was indistinguishable in
  metrics from a broker outage, and those are different responses. The dispatcher's own error code
  is not a tag value - that set is open, and a metric dimension must not be - so a refusal reports
  `dispatcher_refused` and the code stays on the message.

### Changed

- `d2.outbox.published` still carries the kind alone. There is one way to succeed.

## 0.1.0 - 2026-08-17
### Added

- First release. The storage half of the transactional outbox:
  `OutboxMessageRow` with its `OutboxMessageRowConfiguration` and
  `OutboxMessageRowMapper`; `OutboxPayloadSerializer`, whose options are a
  persisted contract; `IOutboxStore` and `EfOutboxStore<TDbContext>`;
  `EfMessageOutbox<TDbContext>`, which stages onto the caller's unit of work;
  `OutboxRelay<TDbContext>`, which claims under a PostgreSQL try-advisory-lock
  and dispatches; `OutboxTelemetry`; and
  `AddD2OutboxEntityFrameworkCore<TDbContext>`.
- The store and the staging seam are registered under the outbox's name, and the
  relay resolves its store, its dispatchers and its options by that name - so two
  modules composed into one host each stage onto their own context and each relay
  locks its own database.
- `ClaimDueAsync` claims rather than reads: it counts the attempt and moves the
  next attempt later by the lease it is given before any message is dispatched, so a
  message whose outcome could not be recorded still advances toward the retry
  limit and is not immediately re-eligible.
- The relay's two internal conditions are recorded as markers rather than error
  codes - `outbox.no-dispatcher` and `outbox.dispatcher-threw` - so an operator
  reading `last_failure_code` can tell them from a dispatcher's catalog code.
