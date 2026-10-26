<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0017: EF-as-DDD persistence - retire the per-op repository layer; persist sum-type aggregates as a flat non-polymorphic Row + pure mapper (Shape B), not TPH

- **Status**: Accepted
- **Date**: 2026-06-06, 2026-06-09

## Context

A per-operation repository layer, with one interface and one implementation per CRUD verb, aims to isolate DB access
behind a stable interface and reduce coupling. In practice, it has these costs:

- **No ad-hoc LINQ**: every non-trivial query required a new repo handler file; EF's composable `IQueryable` pipeline
  was unused.
- **No aggregate `Include` loads**: loading a key + its audit entries across the repository boundary required multiple
  round-trips or hand-rolled join logic.
- **No in-DB projections**: result-set projections (selecting only the columns needed) required new repo variants or
  pulling full entities.
- **Multi-row transactions**: a multi-row write (state change + append audit entry in one `SaveChangesAsync`) required
  coordinating separate repo handlers, making atomicity fragile.
- **Boilerplate**: every new domain object produced 4+ repo interfaces + 4+ implementations before any real logic was
  written.

The cross-cutting machinery the repository layer is supposed to protect - telemetry, metrics, `D2Result`
DB-exception translation, cancellation, request scope - all lives in `BaseHandler` / `BaseRepoHandler`, not in the
repository layer. The per-op repository layer provides only a naming convention with no real isolation benefit.

Entity Framework Core, used correctly, already provides:
- A working Unit-of-Work pattern (`SaveChangesAsync`)
- A clean aggregate-root pattern via `DbSet<T>` and navigation properties
- OTel instrumentation at the SQL level (via `Microsoft.EntityFrameworkCore.Diagnostics`)
- DB-exception classification via `IDbExceptionClassifier` (already in `BaseRepoHandler`)

### The persistence-mechanism problem: immutable sum-type aggregates do not map to a polymorphic EF entity

The example domain, a long-lived secret lifecycle store, is an **immutable sum-type state machine**: a sealed base
record for the key + one sealed per-state type (pending, active, retiring, retired and compromised keys) + total
transitions that return a *new* instance of the next state. This is the right domain model - illegal transitions do not
compile, because a stateful aggregate is an abstract base with one sealed type per state and a transition is declared
only on the states from which it is legal. The question this ADR decides is how that domain is persisted.

Making the aggregate the EF entity and mapping it TPH (`TABLE PER HIERARCHY`) is unsound: that mapping uses one table, a
status discriminator column mapped to the CLR type, and "transitions = delete-old-state row + insert-new-state row at
the same PK in one `SaveChangesAsync`." It rests on the premise that "a discriminator mapped to a read-only `Status`
since EF Core 5 is not a tracked mutation, so morphing the entity's runtime type is fine." That premise is false. EF
Core 10.0.7 with Npgsql.EFC 10.0.1 on Postgres 17 has these behaviors, which show that EF Core **will not morph a
tracked entity's runtime CLR type** (Zoran Horvat's "Wall #1"):

1. A same-PK `Remove` + `Add` is silently **merged into a single `UPDATE`**, not a `DELETE` + `INSERT`
   ([efcore#16355](https://github.com/dotnet/efcore/issues/16355) /
   [#30705](https://github.com/dotnet/efcore/issues/30705) - Open/Backlog, unfixed in EF Core 10). The intended "swap
   the row's CLR type" never happens.
2. That merged `UPDATE` leaves the **old state's columns un-nulled** -> the discriminator and the data disagree (the
   row says `Active` but `RetiringAt` is still populated)
   ([efcore#36308](https://github.com/dotnet/efcore/issues/36308)).
3. A discriminator mapped to a get-only `Status` override fails model-build ("no backing field / no setter")
   ([efcore#4650](https://github.com/dotnet/efcore/issues/4650) - won't-fix).

The morph wall is structural and unfixed. (Horvat's "Wall #2" - polymorphic value types - does **not** apply here: the
hierarchy is the key *entity*, and the read-side materialization of immutable records is fine; the VOs are
flat.) C# 15's `closed`-hierarchy / discriminated-union proposal does not solve the problem for the TPH approach
either: the C# 15 union is a struct boxing an `object?` with no EF mapping, so discriminated unions stay domain-side
regardless. The conclusion: the aggregate must not be the polymorphic EF entity.

The persistence-separation literature supports this directly - Vladimir Khorikov, Jimmy Bogard, and Kamil Grzybek all
separate a rich immutable domain from a thin persistence shape via a mapper rather than forcing the ORM to model the
domain's polymorphism; Oskar Dudycz's guidance is that event-sourcing is justified only when the history *is* the
product, not as a default.

## Decision

**Retire the per-op repository layer. CQRS handlers access the database directly through the module `DbContext`
contract + aggregates + LINQ.** These persistence shapes apply, selected by whether the aggregate is a state machine:

### Shape A (simple aggregates) - aggregate-as-EF-entity, direct mapping

For a non-state-machine aggregate (a single CLR type with VO members, no sealed per-state hierarchy), the aggregate is
the EF entity, mapped directly via an `IEntityTypeConfiguration<T>` (value converters / complex types per the EF VO
mapping pattern). State changes, when any, are ordinary tracked-property `UPDATE`s.

### Shape B (state-machine / sum-type aggregates) - flat non-polymorphic Row + pure mapper

For an immutable sum-type state machine (sealed base + per-state types + total transitions returning new instances -
the key aggregate described in Context), **the aggregate is not the EF entity.** Instead:

- **The domain stays pure and EF-free.** The sealed sum-type + transitions are unchanged - no EF attributes, no EF
  references on Domain. (When C# 15 `closed` hierarchies ship, the base may be annotated; that is purely additive and
  changes nothing here.)
- **Persistence is one flat, non-polymorphic EF Row** (`public sealed class`, not a C# `record` type) per aggregate,
  whose CLR type never changes:
  - a settable `Status` **value** column (a string/enum value, not a TPH type discriminator),
  - the always-present core fields (PK + identity + the fields every state carries),
  - nullable per-state columns (or a JSON payload column for data-heavy states) for the fields only some states carry.
  Because the Row's CLR type is fixed, the EF morph wall is **structurally unreachable** - a transition is an ordinary
  `UPDATE` of the `Status` value + the relevant columns.
- **A pure static mapper maps domain <-> Row:**
  - `row.ToDomain()` - switch on the `Status` value and rehydrate the correct sealed state from the columns, via the
    states' `required init` properties and the VOs' `FromTrusted` factories (trusted store value, no re-validation).
  - `aggregate.ProjectOnto(row)` - set `Status`, then **set all per-state columns to NULL first and set only the new
    state's** afterward. The null-all-then-set step prevents stale columns (the corruption EF's merged `UPDATE` would
    have caused).
  The mapper is a static function, not a DI-registered `IRepository` interface. An interface here would recreate the
  per-op repository layer this ADR retires.
- **`IQueryable` query extensions** (`Pending()` / `Active()` / `Signing()` / ...) filter on the `Status` value column
  -> server-side SQL `WHERE` (indexed); `.ToDomainListAsync()` materializes the rows then maps to the sum-type shape.
- **Concurrency** is a Postgres `xmin` optimistic-concurrency token configured as `.HasColumnType("xid").IsRowVersion()`,
  giving an exactly-one-winner invariant without extra code. It complements (does not replace) the
  `pg_try_advisory_lock` rotation coordination of the example domain.
- **Audit** rows (the append-only audit entity) are written in the same `SaveChangesAsync` /
  transaction as the state change. EF orders principal-before-dependent writes; the audit FK uses
  `OnDelete(Restrict)`.

### Layering

- **App** references EF Core and owns: the flat `*Row`, the pure mapper, the `IQueryable` query extensions, and the
  `IXxxDbContext` contract (`DbSet<...>` + `SaveChangesAsync`).
- **Infra** owns: the concrete `DbContext` implementation and the `IEntityTypeConfiguration<*Row>`.
- **Domain** stays EF-free.
- **No repository.** Handlers use the `IXxxDbContext` contract + the mapper + LINQ directly.

### Command handlers

Command handlers (mutations - rotate, activate, compromise, generate) inherit `BaseRepoHandler` (which keeps the
DB-exception -> `D2Result` translation via `IDbExceptionClassifier`). They load the tracked Row, call the domain
transition on the rehydrated aggregate, `ProjectOnto` the tracked Row, append the audit entity, and `SaveChangesAsync`
once. For state-machine aggregates the write is an ordinary `UPDATE` (+ audit `INSERT`); the `xmin` token makes a
concurrent transition fail the second writer. A handler that updates or deletes a single row of an entity with no
concurrency token inspects the `rowsAffected` count (the `SaveChangesAsync()` return value), and returns
`D2Result.NotFound()` on zero rows and
`D2Result.Conflict()` on more than one.

### Query handlers

Query handlers (read-only - JWKS assembly, key status lookups) inherit `BaseHandler` and use `AsNoTracking()` LINQ via
the query extensions -> `ToDomainListAsync()` or lightweight projections. No mutations, no `SaveChangesAsync`.

### Boilerplate at scale -> source generation

The Row + mapper + EF config + query extensions for a state-machine aggregate are mechanical (~120-180 lines per
aggregate, derivable from the sealed-state shapes). **Write the first one by hand.** Once the shape is proven across
2-3 aggregates, extract a Roslyn source generator that emits them per aggregate from the sealed-state hierarchy. Do not
build the generator before the shape is proven.

### What is unchanged

- `BaseHandler` and `BaseRepoHandler` are unchanged - all cross-cutting concerns (telemetry, metrics, cancellation,
  `D2Result` translation) remain there.
- EF Core OTel instrumentation replaces per-op repo spans with SQL-level spans, which are more precise.
- The handler pipeline, DI registration pattern, and `D2Result` semantic factories are unchanged.
- The EF VO mapping pattern (complex types + value converters, ADR-0001) is unchanged - it applies to the Row's
  VO-typed columns.

## Consequences

**Positive:**
- The EF morph wall is structurally unreachable for state-machine aggregates: the Row's CLR type never changes, so the
  silent-merge / stale-columns / discriminator-build failures cannot occur. On EF Core 10.0.7 with Npgsql.EFC 10.0.1 on
  Postgres 17 this mapping has these behaviors (model builds; transition emits one `UPDATE` + one audit `INSERT` - no
  delete+insert, no morph; full lifecycle round-trips to the correct sealed type; server-side status filtering; `xmin`
  exactly-one concurrency; exact temporal boundaries).
- The domain stays pure: the sum-type + total transitions have no persistence concern, so the guarantee that illegal
  states cannot be represented is not weakened by the ORM.
- Full EF power for queries: LINQ, `Include`, projections, bulk ops, single-transaction multi-row writes.
- Less boilerplate: no per-op repo interface + implementation per CRUD verb; the Row/mapper/config/query-extensions can
  themselves be source-generated once the shape is proven.
- SQL-level OTel spans: more precise than per-op repo spans for debugging.
- Cross-cutting unchanged: all pipeline machinery stays at `BaseHandler` / `BaseRepoHandler`.

**Negative / trade-offs:**
- The same aggregate is represented as the sealed domain shape and as the flat Row, with the mapper converting between
  them. The mapper's `ProjectOnto` null-all-then-set step is required - a missed null leaves a stale column. Every
  per-state column must be nulled first, then only the new state's columns set. This is the cost of keeping the domain
  pure; the mapping is mechanical and may be source-generated later, but it is hand-written and must be tested
  per-state (round-trip every state + assert no stale columns survive a transition).
- A new representation to learn: teams familiar with aggregate-as-EF-entity must learn the Row + mapper split for
  state-machine aggregates (Shape B); simple aggregates (Shape A) are unchanged.
- The DB-exception -> `D2Result` translation must be explicitly invoked via `BaseRepoHandler` - a handler that forgets
  to inherit it loses the translation. `BaseHandler` API guidance mitigates this.

### Deviation (per-aggregate, not the default): event-sourcing via Marten

For an aggregate that is both **audit-defining** (the history is the product, not a side table) and
**workflow-complex** (Dudycz's good-fit criteria), event-sourcing via Marten is the justified persistence. Marten
coexists with EF on the same Postgres instance. This is a deliberate per-aggregate deviation, not the system-wide
default. The default for state-machine aggregates is Shape B (flat Row + mapper); event-sourcing is used only when an
aggregate meets both criteria.

## Alternatives considered

- **Keep per-op Repository handlers**: rejected. The costs listed in Context outweigh any isolation benefit; all
  cross-cutting concerns are already at `BaseHandler`.
- **Aggregate-as-TPH-entity with delete+insert transitions**: rejected as **unsound** on EF Core 10. The morph wall
  ([efcore#30705](https://github.com/dotnet/efcore/issues/30705) Open/Backlog,
  [#36308](https://github.com/dotnet/efcore/issues/36308), [#4650](https://github.com/dotnet/efcore/issues/4650)
  won't-fix) makes "morph a tracked entity's CLR type via a discriminator" silently merge into a stale-column UPDATE,
  and the get-only-`Status` discriminator fails model-build. The "read-only discriminator since EF Core 5 -> not a
  tracked mutation" premise is false.
- **Generic `IRepository<T>`**: rejected - a leaky abstraction over EF that loses EF's query power and, for
  state-machine aggregates, would re-introduce the per-op repository layer the pure static mapper deliberately avoids.
- **MediatR-style separate repository + Unit-of-Work**: rejected - `BaseHandler` already is the pipeline;
  `SaveChangesAsync` already is the Unit-of-Work. Adding a separate UoW layer adds code and a layer with no benefit.
- **C# 15 discriminated unions mapped by EF**: rejected - the C# 15 union is a struct boxing an `object?` with no EF
  mapping; DUs stay domain-side. The flat-Row + mapper approach is independent of the union proposal.
- **Event-sourcing as the system-wide default**: rejected - per Dudycz, event-sourcing is justified only when the
  history is the product and the workflow is complex. It is a per-aggregate deviation (see Consequences), not the
  default; most aggregates are well-served by the flat Row + mapper.
- **Dapper / raw SQL**: out of scope for this service. EF Core's ORM capabilities are well-matched to the key lifecycle
  domain; the flat Row maps cleanly with value converters + the `xmin` token.

## References

- Example application: long-lived secret lifecycle stores that use sum-type domain states + a concrete flat row schema
  this convention persists; `pg_try_advisory_lock` rotation coordination complements the `xmin` concurrency token.
- [ADR-0001](0001-contacts-folded-owned-component.md) - the EF VO mapping pattern (complex types + value converters)
  the Row's VO columns reuse.
- [ADR-0018](0018-spec-driven-error-codes.md) / [ADR-0019](0019-wrapped-result-wire-model.md) - the error-code +
  wrapped-result conventions CQRS handlers surface failures through.
