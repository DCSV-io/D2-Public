<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0041: Transactional outbox package

- **Status**: Accepted
- **Date**: 2026-10-03

## Context

A service that changes durable state and then announces the change on a message bus has these failure modes when
the announcement is a separate step:

- The change commits and the publish fails. Consumers never learn of the change, and nothing records that an
  announcement is still required.
- The publish succeeds and the change rolls back. Consumers act on a change that never happened.

A service that commits a durable transition, then publishes once, and logs and swallows a publish failure has the
same defect. Two consumers in two databases need the same guarantee, so building it inside one of them would
produce a second implementation with its own failure semantics to keep in agreement.

## Decision

**Publication is separated from commitment.** A caller stages the announcement in the same unit of work as the
change, before the save. One commit makes the change and the staged announcement durable together, or
neither. A relay delivers staged messages afterwards. Delivery is at-least-once, so consumers must be idempotent.

**The mechanism ships as a package cluster split along the database boundary.** The staged row has to be in the
same database as the change it is staged with, or the single-transaction property is lost. Every port is declared
in the database-agnostic package, and every implementation that touches a `DbContext` or a database provider is
in a storage package.

- `DcsvIo.D2.Outbox` contains `IMessageOutbox<TDb>`, `IOutboxDispatcher`, the staged, published and abandoned
  message states, `OutboxOptions` and the registration surface. It contains no relay or other running component.
- `DcsvIo.D2.Outbox.EntityFrameworkCore` contains `OutboxMessageRow` and its mapping,
  `EfMessageOutbox<TDb, TDbContext>` staging onto the consumer's own context, the store the relay reads through,
  and the meter. It references no database provider.
- `DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres` contains `OutboxRelay<TDb>`, which claims, dispatches and records
  under a PostgreSQL try-advisory-lock so one instance drains a table per tick.

**An outbox belongs to a database, not to a host.** `TDb` is the consumer's application-layer context interface. A
host composing two modules over two databases resolves two outboxes and two relays, each a distinct closed generic
type checked at compile time. Each consumer owns its table through its own migration and supplies its own
advisory-lock key and relay connection string.

**The message kind is generic at the call site and a string at rest.** `StageAsync<TKind, TPayload>` takes the
consumer's own closed enum, and the row stores the member name. A shared enum would put a consumer's vocabulary in
a portable package, and a raw string everywhere would lose the closed set. Each consumer keeps a test asserting
that every member of its enum has a dispatcher.

**A consumer's domain-shaped announcer port calls the outbox**, and consumers do not publish to the message bus through
the outbox: the outbox sits behind the announcer port, not in front of the bus. The dispatcher publishes through the
bus, so the message catalog, the encryption frame and publisher confirms are unchanged.

## Consequences

- A broker outage at the moment of a change delays delivery instead of losing it. A database failure at the moment
  of the change fails the caller's transaction, and nothing is announced.
- Staging below the save is a silent defect: the row joins the change tracker after the commit and is either
  committed by an unrelated later save or lost. `IMessageOutbox<TDb>` states the order as a precondition on the
  caller.
- Retry is bounded. The shipped defaults (`RetryLimit` 70, backoff from two seconds doubling to a one-minute
  ceiling) cover a broker outage of about 65 minutes. Past the limit a message is abandoned with its payload and
  last failure code kept, so an operator can see and re-stage it.
- A payload refers to its subjects by identifier and contains no personal data. The row is stored as plaintext and is
  exempt from anonymization, so `StageAsync` rejects a payload with a `[RedactData]` marker anywhere the
  serializer reaches. Rows that are published or abandoned are deleted after `PublishedRetention` (7 days) and `AbandonedRetention`
  (90 days); a staged row is never deleted by age.
- A consumer that stages without running a relay has no database driver dependency, because only the relay package
  references PostgreSQL.

## Alternatives considered

**A mechanism local to one consuming module.** Rejected because a second consumer exists: two implementations
mean two sets of failure semantics, and the second consumer would call the first module's internals or copy them.

**One package containing the ports, the row and the relay.** Rejected because the core would then reference a
data-access library and a database provider. The same split already exists in the pagination packages
(`DcsvIo.D2.Pagination`, its Entity Framework package and its PostgreSQL leaf).

**The relay and the staging implementation in the database-agnostic package.** Rejected because the relay takes a
PostgreSQL advisory lock and staging adds a row to the caller's `DbContext`;
neither can be built without the storage dependencies.

**A shared closed enum of message kinds.** Rejected because the enum members are consumer vocabulary, which a
portable package must not contain. The string-at-rest kind is a weaker type at that one seam, and the per-consumer
equality test restores the closed-set check.

**Publish after commit and log a failure.** Rejected because a lost
publish is recovered only by an unrelated later event or a process restart.

## References

- `packages/dotnet/outbox/README.md` - the cluster and its split.
- `packages/dotnet/outbox/core/IMessageOutbox.cs` - the staging port and its above-the-save precondition.
- `packages/dotnet/outbox/core/OutboxOptions.cs` - the retry, backoff and retention defaults.
- `packages/dotnet/outbox/entity-framework-core/EfMessageOutbox.cs` - staging onto the consumer's context.
- `packages/dotnet/outbox/entity-framework-core/OutboxPayloadPersonalDataCheck.cs` - the marked-payload refusal.
- `packages/dotnet/outbox/entity-framework-core-postgres/OutboxRelay.cs` - claim, dispatch, record and sweep.
- [ADR-0009](0009-async-messaging-encrypted-payloads.md) - the message bus the dispatchers publish through.
- [ADR-0017](0017-ef-as-ddd-persistence.md) - the flat row and mapper shape the storage package follows.
