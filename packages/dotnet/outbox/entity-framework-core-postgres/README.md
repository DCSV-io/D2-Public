<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres

For a developer whose service stages outward announcements through the D2
transactional outbox on PostgreSQL. This package delivers them with a
hosted service that claims a batch of due messages, hands each to the dispatcher
whose kind matches, and records what happened.

It is the PostgreSQL leaf of the outbox. The relay takes a PostgreSQL advisory
lock so exactly one instance drains the table per tick, which is what binds it to
a provider. `DcsvIo.D2.Outbox.EntityFrameworkCore` contains the row, its mapping,
the store and the staging seam, and binds no provider - so a service that stages
messages and delivers them elsewhere never reaches a PostgreSQL driver.

## Install

```bash
dotnet add package DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres
```

## Public API

| Surface                                                 | Role                                                            |
| ------------------------------------------------------- | --------------------------------------------------------------- |
| `OutboxRelay<TDb>`                                      | The hosted service that claims, dispatches and records          |
| `OutboxRelay<TDb>.NO_DISPATCHER_MARKER`                 | Recorded when a staged kind has no dispatcher                   |
| `OutboxRelay<TDb>.DISPATCHER_THREW_MARKER`              | Recorded when a dispatcher threw rather than returning a result |
| `AddD2OutboxRelayPostgres<TDb>` on `IServiceCollection` | Registers the relay for one database                            |

## Dependencies

| PackageId                                               | Role                                                                                   |
| ------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Outbox`                                      | The message states, the dispatcher port and `OutboxOptions`                            |
| `DcsvIo.D2.Outbox.EntityFrameworkCore`                  | `IOutboxStore<TDb>`, which the relay claims and records through, and `OutboxTelemetry` |
| `DcsvIo.D2.Result`                                      | The `D2Result` a dispatcher returns                                                    |
| `DcsvIo.D2.Time`                                        | `IClock`, which the due predicate and every timestamp read from                        |
| `DcsvIo.D2.Utilities`                                   | `SanitizedExceptionRender`, which renders the relay's caught exceptions                |
| `DcsvIo.D2.EntityFrameworkCore.Postgres`                | `PgAdvisoryLock`, the relay's try-lock                                                 |
| `DcsvIo.D2.Context.Abstractions`                        | `ISystemWorkScopeFactory`, which opens each tick's scope                               |
| `NodaTime`                                              | `Instant` and `Duration` on the states and the backoff curve                           |
| `Microsoft.Extensions.Hosting.Abstractions`             | `BackgroundService`                                                                    |
| `Microsoft.Extensions.Logging.Abstractions`             | The relay's log delegates                                                              |
| `Microsoft.Extensions.Options`                          | `IOptionsMonitor<OutboxOptionsFor<TDb>>`                                               |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | The registration extension                                                             |

## Configuration

This package declares no options of its own. The tick, the batch size, the retry
limit and the backoff curve, plus the required advisory-lock key and connection
string, are `OutboxOptions` in `DcsvIo.D2.Outbox`, which documents each with its
default. They are bound onto `OutboxOptionsFor<TDb>` and set through
`AddD2Outbox<TDb>`, so a host running two outboxes configures each separately.

## Usage

Register the relay beside the store, both for the same database:

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

There is one relay per context interface. `TDb` is the database's application-layer
context interface, and the relay resolves its store, its dispatchers and its
options by it, so each relay drains its own table under its own lock. It is also
the registration's identity: the hosted service is deduplicated on its
implementation type, which is `OutboxRelay<TDb>`, one closed type per interface,
so a second call for one interface adds nothing and a call for another
interface adds its own relay. One interface is therefore paired with one context, and one
context with one interface, because
`AddD2OutboxEntityFrameworkCore<TDb, TDbContext>` records the pair in both
directions and refuses either collision; that call is in
`DcsvIo.D2.Outbox.EntityFrameworkCore` and its README explains it. A relay
cannot start without that interface's store, and that chain is why the
message table has no name column.

Mapping a context to a table is the host's responsibility. Registration receives only the CLR types it is given, so it
records one context type per interface and has no information showing that two context classes map the one physical
`outbox_messages` table. Composed that way they are two unrelated registrations as far as DI is concerned, and their two
relays would each hold their own advisory lock and claim rows the other has to dispatch. Keeping one context class over
one outbox table is the host's responsibility.

The store is checked at start. A relay registered for a database with no
`IOutboxStore<TDb>` stops the host from starting, and the error states that database and the call
it is missing. Without the check it would start cleanly and then claim nothing
for the life of the host: the store resolves inside each tick, and a tick that
throws is caught so one bad tick cannot crash the host.

## Concurrency and failure

One instance dispatches at a time. Each tick takes a PostgreSQL
try-advisory-lock on the consumer's own key, skip-if-held, so exactly one
instance dispatches per window and the others wait for their next tick. The
lock auto-releases on connection drop, so a crashed instance never blocks
delivery. The key comes from
`OutboxOptions.RelayAdvisoryLockKey`; this package defines no key of its own. The
connection string comes from `OutboxOptions.RelayConnectionString`, supplied by the consumer's composition
root: a session advisory lock needs its own physical connection, and the
connection string a live provider hands back has had its password removed once
the connection was opened, so a lock opened from it fails authentication on every
tick, silently, while the messages pile up staged.

Delivery is at-least-once. A relay that dies after a dispatcher confirmed and
before the state was recorded dispatches the message again, so consumers must be
idempotent.

At-least-once delivery is still bounded. The attempt is counted by the claim rather
than by the record, so a message whose outcome could not be written still
advances toward `OutboxOptions.RetryLimit` and still waits before it is attempted
again.

Unavailability behavior of the dependencies the relay reaches:

| Dependency                       | Behavior when unavailable                                                          |
| -------------------------------- | ---------------------------------------------------------------------------------- |
| PostgreSQL, at the advisory lock | Retry-with-backoff. The tick is abandoned and the next one retries                 |
| PostgreSQL, at claim             | Retry-with-backoff. The tick logs and returns; the next tick tries again           |
| PostgreSQL, at record            | Retry-with-backoff, by redelivery. The message is dispatched again on a later tick |
| A dispatcher's own transport     | Retry-with-backoff, bounded by `RetryLimit`, then abandon-and-alert                |

A staged kind with no registered dispatcher takes the ordinary failure path and
records `NO_DISPATCHER_MARKER` in `last_failure_code`. That means a deployment lacks
a component, and the message is not bad, and the retry window is
long enough that a rolling deploy which adds the dispatcher delivers the message
rather than losing it.

Two dispatchers registered for one kind make the relay throw when it starts. Grouping by kind and taking the
first would deliver that kind to a dispatcher the consumer did not
choose.

The same tick also deletes the settled rows past their retention window. A
published row is deleted once it is older than `OutboxOptions.PublishedRetention`, an
abandoned row once its last attempt is older than
`OutboxOptions.AbandonedRetention`, and a staged row is never deleted by age. The
sweep runs after the tick's deliveries, under the advisory lock the tick already
holds, so exactly one instance deletes, and the deliveries of a tick run before its sweep.
It deletes at most `BatchSize` rows per tick, so a backlog drains over as many
ticks as it takes. A sweep that fails logs and leaves the rows for the next tick;
it never fails a delivery.

## Metrics

The relay records into meter `DcsvIo.D2.Outbox`. The instruments are declared by
`OutboxTelemetry` in `DcsvIo.D2.Outbox.EntityFrameworkCore`, whose README
documents each with its unit and its tag keys; a host adds the meter with
`.WithMetrics(m => m.AddMeter(OutboxTelemetry.METER_NAME))`.

Every failing delivery the relay records has the `outcome` tag, whose value
set is `no_dispatcher`, `dispatcher_threw` and `dispatcher_refused` - a
deployment that lacks a component, a transport that faulted, and a dispatcher that
returned a failed result. Each calls for a different operator response.
