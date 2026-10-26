<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Outbox.EntityFrameworkCore

The storage half of the D2 transactional outbox. It contains the row, its EF
mapping and mapper, the staging implementation that joins the caller's own unit
of work, and the store that the relay reads and writes through.

Everything that touches a `DbContext` is in this package and not in
`DcsvIo.D2.Outbox`, because the staged row has to be in the same database as
the change it is staged with. Otherwise the outbox loses the single-transaction
property it exists to provide.

This package binds no database provider. The relay does, because it drains the
table under a PostgreSQL advisory lock, so it ships in
`DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres` and a consumer that stages
messages without running a relay takes no driver with this package.

## Install

```bash
dotnet add package DcsvIo.D2.Outbox.EntityFrameworkCore
```

## Dependencies

- `DcsvIo.D2.Outbox`
- `DcsvIo.D2.Result`
- `DcsvIo.D2.Time`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
- `NodaTime`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## Types

| Type                                                   | Role                                                                                                                                                                                              |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OutboxMessageRow`                                     | The flat row, with the union of the per-state columns and a status discriminator                                                                                                                  |
| `OutboxMessageRowConfiguration`                        | The EF mapping to table `outbox_messages`, with the relay's claim index and the retention sweep's two                                                                                             |
| `OutboxMessageRowMapper`                               | `ToDomain`, `ProjectOnto`, `ToNewRow`                                                                                                                                                             |
| `OutboxPayloadSerializer`                              | The only place that produces a staged payload's text. Its options are a persisted contract                                                                                                        |
| `IOutboxStore<TDb>`                                    | `ClaimDueAsync`, `RecordAsync`, `PruneSettledAsync` - the seam the relay reads and writes through                                                                                                 |
| `EfOutboxStore<TDb, TDbContext>`                       | That store over the consumer's context                                                                                                                                                            |
| `EfMessageOutbox<TDb, TDbContext>`                     | `StageAsync` over the consumer's context; adds a row and commits nothing. Both overloads take one path: the minting one delegates to the id-taking one, which refuses a default `OutboxMessageId` |
| `OutboxTelemetry`                                      | The meter and its counters, which the relay records into                                                                                                                                          |
| `OutboxEntityFrameworkCoreServiceCollectionExtensions` | `AddD2OutboxEntityFrameworkCore<TDb, TDbContext>`                                                                                                                                                 |

## Setup

The consumer owns the table, because the table lives in the consumer's database.
Apply the configuration from `OnModelCreating` and create the table in the
consumer's own migration:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new OutboxMessageRowConfiguration());
}
```

Then register the pieces, all for the same database:

```csharp
services.AddD2Outbox<IMyDbContext>(o =>
{
    o.RelayAdvisoryLockKey = AdvisoryLocks.MyDb.OUTBOX_RELAY;
    o.RelayConnectionString = connectionString;
});
services.AddD2OutboxDispatcher<IMyDbContext, ThingHappenedDispatcher>();
services.AddD2OutboxEntityFrameworkCore<IMyDbContext, MyDbContext>();
services.AddD2OutboxRelayPostgres<IMyDbContext>();
```

The last call is the relay, and it comes from
`DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres`. Without it the messages are
staged and nothing delivers them, which is the right composition for a host that
only writes to the table.

The type parameters enforce the pairing between a database and its context. An outbox belongs to a database rather than
to a host, so a host composing two modules over two databases registers two of everything, and each module's handlers
must stage onto their own context. `TDb` is the database's application-layer context interface, and both seams are
registered under it; `TDbContext` is the concrete context and is constrained to implement `TDb`. With this constraint, a
context bound under an interface it does not implement is a compile error, so the composition where one module's
handlers stage onto the other module's unit of work cannot be written.

The call records the pairing in both directions. The constraint admits one context under
every interface it implements, and one interface for every context that
implements it, and a consumer is free to declare either pair twice. So this call
records the pair in both directions and refuses either collision: a second `TDb`
over a context already bound, and a second `TDbContext` under an interface
already bound. The same pair twice is idempotent, and a refused call records
neither half. Each refusal message states the interface and the context on both sides.
The first collision matters because two stores over one table would put two
relays on it, each holding its own advisory lock and claiming rows the other has
to dispatch. The second matters because both seams are registered under the
interface alone, so the second call's `TryAdd` is dropped and every handler
taking `IMessageOutbox<TDb>` stages onto the first context's change tracker
while the host passed the second to the call.

Registration receives only CLR types, so it has no information about which table a context maps. Both refusals compare
the CLR types the call was given, so they enforce one context type per interface and one interface per context type. Two
context classes that map the one physical `outbox_messages` table are two unrelated registrations as far as DI is
concerned, and keeping one context class over one outbox table is the host's responsibility.

A consumer takes its own staging seam by using the same interface as the type argument, with no
service key:

```csharp
public sealed class ThingHandler(IMessageOutbox<IMyDbContext> outbox);
```

The relay's meter is added to the host's OpenTelemetry builder the usual way:

```csharp
.WithMetrics(m => m.AddMeter(OutboxTelemetry.METER_NAME))
```

## Metrics

Meter `DcsvIo.D2.Outbox`. Every instrument has the `kind` tag, and the two
error counters also have `outcome`.

| Instrument                    | Unit        | Tag keys          | What it records                          |
| ----------------------------- | ----------- | ----------------- | ---------------------------------------- |
| `d2.outbox.published`         | `{message}` | `kind`            | Messages a dispatcher confirmed          |
| `d2.outbox.dispatch_failures` | `{failure}` | `kind`, `outcome` | Failed attempts that re-staged a message |
| `d2.outbox.abandoned`         | `{message}` | `kind`, `outcome` | Messages that ran out of attempts        |
| `d2.outbox.dispatch_duration` | `s`         | `kind`            | How long one dispatcher call took        |
| `d2.outbox.pruned`            | `{row}`     | none              | Settled rows the retention sweep deleted |

`outcome` takes one of a closed set of values - `no_dispatcher`, `dispatcher_threw`,
`dispatcher_refused` - and each calls for a different operator response:
a deployment that lacks a component, a transport that faulted, and a dispatcher
that returned a failed result. The published counter has `kind` alone, because
there is one way to succeed. The duration histogram does too: the failure class
is already recorded on the error counters, and splitting latency by outcome would leave
each bucket with too few samples to read.

A dispatcher that slows down is visible on `d2.outbox.dispatch_duration`
before it is visible anywhere else. One tick claims up to `BatchSize` messages
and awaits a dispatcher per message, and the tick is a fixed cadence rather than
a queue - so a dispatcher that goes from ten milliseconds to eight hundred grows
a backlog silently until the abandonment alert fires an hour later.

An abandonment alert is built on `d2.outbox.abandoned`. The alerting rule
is deployment configuration, and this package does not ship it; the package ships
the counter. The matching log line is at `Error` and includes the
message id, the kind and the last failure code.

## Configuration

This package declares no options of its own. The tick, batch size, retry limit
and backoff curve, plus the required relay advisory-lock key and connection
string, are `OutboxOptions` in `DcsvIo.D2.Outbox`, bound onto
`OutboxOptionsFor<TDb>` and set through `AddD2Outbox<TDb>`, so a host running two
outboxes configures each separately.

## What the row may carry, and how long it lives

A payload refers to its subjects by identifier and contains no personal data, no
secret, no code and no hash. The row stores it as it was staged: this package
does not encrypt it, and no erasure sweep rewrites it - `OutboxMessageRow` is
`IExemptFromAnonymization`, and `Payload` has a `[KeepOnAnonymization]`
reason that states why. A consumer whose announcement needs a personal value stages
the identifier and lets the dispatcher read the value at delivery, or seals the
value before staging it.

`StageAsync` refuses a payload that contains a marked value. The exemption
keeps the row out of `AnonymizationModelValidator`'s reach, and such a value
would survive in this column for up to `AbandonedRetention` past the subject's
erasure, so `EfMessageOutbox.StageAsync` returns `ValidationFailed` and adds no
row when a `[RedactData]` marker is on the payload type, on a member the
serializer writes - a public property, or a field or non-public property
carrying `[JsonInclude]` - or a public constructor parameter, or on a type any of
them reaches through a member, a collection element, a generic argument or a
derived type that a `[JsonDerivedType]` attribute lists. A member declared as `object`,
or a collection of them, is written by the runtime type of its value, so the
check reads the values those members hold, by their runtime types. Such a value
whose type has its own converter and itself holds an `object` member is refused,
because nothing describes what the converter writes. The check reads markers,
not values: a personal value on an unmarked member still passes, so the
consumer remains responsible for the precondition at its own staging call.

The relay's retention sweep bounds the row's lifetime. A published row
is deleted once it is older than `PublishedRetention` (default seven days), and
an abandoned row once its last attempt is older than `AbandonedRetention`
(default ninety days). A staged row is never deleted by age, because it has not been delivered yet; the
retry limit and not the window decides when the relay stops attempting it.
`IOutboxStore<TDb>.PruneSettledAsync` performs the deletion, the
relay calls it after each tick's deliveries under the same advisory lock, and
`d2.outbox.pruned` counts what it removed.

`OutboxMessageRowConfiguration` maps the partial indexes those reads walk,
and a consumer applying the configuration owns the migration that creates them:

| Index                                       | Filter                 | Serves                                                 |
| ------------------------------------------- | ---------------------- | ------------------------------------------------------ |
| `ix_outbox_messages_status_next_attempt_at` | `status = 'Staged'`    | the relay's claim of the messages due for delivery     |
| `ix_outbox_messages_published_at`           | `status = 'Published'` | the retention sweep's delete past `PublishedRetention` |
| `ix_outbox_messages_abandoned_at`           | `status = 'Abandoned'` | the retention sweep's delete past `AbandonedRetention` |

## Concurrency and failure

The relay is not in this package. It ships in
`DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres`, whose README documents the
advisory lock it takes each tick and how it behaves during an outage. The rest of this section
describes what the store guarantees to the relay.

Delivery is at-least-once. A relay that dies after a dispatcher confirmed
and before the state was recorded dispatches the message again, so consumers
must be idempotent.

At-least-once delivery is still bounded. `ClaimDueAsync` counts the attempt and
moves the next attempt later before the message is handed to a dispatcher, so a
message whose outcome could not be written - a row-version conflict, or a
database serving reads while refusing writes - still advances toward
`RetryLimit` and still waits before it is attempted again. Counting the attempt
on the record instead would leave it eligible on the very next tick with its
count unchanged, and the relay would republish it forever.

A claim that another claimant wrote first returns `Conflict`. The claim writes each row
against the `xmin` it read, so when a second claimant writes a row of the same
batch first, this claim is refused, `ClaimDueAsync` returns `Conflict`, and
nothing is dispatched. `ServiceUnavailable` remains the result for a database
that did not respond or did not accept the write.

Unavailability behavior of the database this package reaches:

| Call            | Behavior when the database is unavailable                                                                                                                                                       |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ClaimDueAsync` | Returns a failed `D2Result`. The relay logs it and returns; the next tick tries again                                                                                                           |
| `RecordAsync`   | Returns a failed `D2Result`. The message is dispatched again on a later tick                                                                                                                    |
| `StageAsync`    | Nothing is written, because it commits nothing of its own - the caller's `SaveChangesAsync` fails and the change and the message are lost together, which is the property the outbox exists for |

The `last_failure_code` column stores two kinds of value, which differ in form.
A dispatcher's own `D2Result.ErrorCode` comes from a generated
catalog and is upper-case and underscored. Anything lower-case and dotted -
`outbox.no-dispatcher`, `outbox.dispatcher-threw` - was written by the relay
about itself, describes no caller-visible failure, and has no HTTP status,
category or user message because none of those exist for it. The code is
recorded on every failed attempt rather than only on the one that abandons, so
an operator querying during an outage can tell the two apart while the message
is still retrying.
