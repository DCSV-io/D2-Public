<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Outbox

A transactional outbox for outward announcements. Staging a message joins
the caller's own unit of work, so the change and the staged message are
committed together or not at all. A broker that is down delays delivery
instead of losing the message, and a rolled-back change announces nothing.

This package contains the database-agnostic half: the staged message and its
states, the ports a consumer calls, the retry and abandon policy inputs, and the
dispatcher registry. The storage half - the row, its mapping and the staging
implementation - ships as `DcsvIo.D2.Outbox.EntityFrameworkCore`, because the
staged row has to live in the same database as the change it is staged with. The
relay that delivers the staged messages ships as
`DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres`, because the advisory lock that
limits draining the table to one instance is PostgreSQL-specific, while the row and
its mapping are provider-neutral.

## Install

```bash
dotnet add package DcsvIo.D2.Outbox
```

## Dependencies

- `DcsvIo.D2.Result`
- `DcsvIo.D2.Time`
- `DcsvIo.D2.Utilities`
- `NodaTime`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Options`

## Types

| Type                                | Role                                                                                                                                                                                  |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IMessageOutbox<TDb>`               | `StageAsync<TKind, TPayload>` - joins the caller's unit of work and commits nothing; an overload takes a caller-minted `OutboxMessageId` so a payload can carry the id of its own row |
| `IOutboxDispatcher`                 | `Kind` plus `DispatchAsync` - one per kind; its implementation sends the message over the transport                                                                                   |
| `IOutboxDispatcherBinding<TDb>`     | One dispatcher, bound to the database whose relay may hand it messages                                                                                                                |
| `OutboxMessage`                     | The abstract base: `Id`, `Kind`, `Payload`, `StagedAt`, `Attempts`, `Status`                                                                                                          |
| `StagedOutboxMessage`               | Durable and undelivered. Has `NextAttemptAt`, `LastFailureCode`, `Publish` and `Fail`                                                                                                 |
| `PublishedOutboxMessage`            | Confirmed. Has `PublishedAt`                                                                                                                                                          |
| `AbandonedOutboxMessage`            | Out of attempts. Has `AbandonedAt` and `LastFailureCode`                                                                                                                              |
| `OutboxMessageStatus`               | The persistence discriminator: `Staged`, `Published`, `Abandoned`                                                                                                                     |
| `OutboxMessageId`                   | The message identity, with `New()`                                                                                                                                                    |
| `OutboxOptions`                     | Relay lock key, relay connection string, tick, batch size, retry limit, backoff curve, and the retention window per settled status                                                    |
| `OutboxOptionsFor<TDb>`             | One database's `OutboxOptions`. It adds no member: the type parameter is the key                                                                                                      |
| `OutboxServiceCollectionExtensions` | `AddD2Outbox<TDb>`, `AddD2OutboxDispatcher<TDb, TDispatcher>`                                                                                                                         |

## Options

Bound from the `LIB_OUTBOX` section onto `OutboxOptionsFor<TDb>`. A host running
two outboxes over two databases binds the section onto each one's own closed
type and sets that one's lock key and connection string separately.

```csharp
services.AddOptions<OutboxOptionsFor<IMyDbContext>>()
    .Bind(configuration.GetSection(OutboxOptions.SECTION))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

| Property                | Default              | What it decides                                                                            |
| ----------------------- | -------------------- | ------------------------------------------------------------------------------------------ |
| `RelayAdvisoryLockKey`  | required, no default | The consumer's own advisory-lock key for its relay                                         |
| `RelayConnectionString` | required, no default | The connection the relay takes its advisory lock on - the same database the table lives in |
| `RelayTick`             | `00:00:05`           | How often the relay looks for due messages                                                 |
| `BatchSize`             | `100`                | How many messages one tick claims                                                          |
| `RetryLimit`            | `70`                 | Total attempts a message is allowed                                                        |
| `RetryBackoffBase`      | `00:00:02`           | The first wait, doubling per attempt already made                                          |
| `RetryBackoffCeiling`   | `00:01:00`           | The longest wait the curve reaches                                                         |
| `PublishedRetention`    | `7.00:00:00`         | How long a published row is kept after the dispatcher confirmed it                         |
| `AbandonedRetention`    | `90.00:00:00`        | How long an abandoned row is kept after the last attempt failed                            |

`RelayConnectionString` is an option and is not derived,
because a session advisory lock needs its own physical connection, and
the connection string a live Npgsql provider hands back has had its password
removed once the connection was opened - so a lock opened from it fails
authentication on every tick, silently, while the messages pile up staged.

`RetryLimit`, `RetryBackoffBase` and `RetryBackoffCeiling` together decide how
long a broker outage can last before messages are abandoned. A bounded retry has to abandon a poisoned message, and an
outage has to delay delivery and not end it; these requirements conflict. With the defaults, an outage can last
about sixty-five minutes: the early waits double from two seconds until they
reach the ceiling, and every later wait equals the ceiling. If you lower the limit or the ceiling, an outage
of an hour abandons messages.

## Usage

The kind is generic at the call site and a string at rest. A raw string
everywhere would lose the closed set of kinds; a shared enum would put a consumer's
own vocabulary into a portable package. Each consumer defines its own enum and its
own test asserting that every member has a dispatcher.

```csharp
public enum ThingOutboxKind { ThingHappened }

public sealed class ThingHappenedDispatcher(IMessageBus bus) : IOutboxDispatcher
{
    public string Kind => nameof(ThingOutboxKind.ThingHappened);

    public async ValueTask<D2Result> DispatchAsync(
        OutboxMessage message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Deserialize<ThingHappenedEvent>(message.Payload);
        return await bus.PublishAsync(payload!, ct);
    }
}
```

Call `StageAsync` before `SaveChangesAsync`, never after it. A row staged after the save is
added to the change tracker after the commit, and an unrelated later
save either commits it or it is lost silently; nothing fails.

```csharp
// 1. project the change onto its rows - nothing is committed yet
thing.ProjectOnto(row);

// 2. stage the announcement onto the same unit of work
var staged = await outbox.StageAsync(ThingOutboxKind.ThingHappened, wireEvent, ct);
if (!staged.IsOk) return staged;

// 3. one commit makes the change and the obligation durable together
await db.SaveChangesAsync(ct);
```

## Which outbox is which

An outbox belongs to a database, not to a host. A host composing two modules over
two databases has two of everything, and the staged row has to join the unit of
work that commits the change it was staged with. If a module is given the other
module's staging seam, the row commits with a transaction that is unrelated to it.

The application-layer context interface identifies the database, and the
compiler checks that type. `TDb` is unconstrained here because this
package binds no data-access library, so it cannot refer to the base type a
consumer's context derives from; the storage package constrains the concrete
context to implement `TDb`, so a mis-paired registration is a compile error
rather than a runtime stall.

Registration pairs with the storage half:

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

A caller takes its own staging seam by using the same interface as the type argument:

```csharp
public sealed class ThingRecorder(IMessageOutbox<IMyDbContext> outbox);
```

## Telemetry

This package emits none: it contains the message states, the ports and the options,
and no relay or store. The meter and its instruments are declared in
`DcsvIo.D2.Outbox.EntityFrameworkCore`, and the relay in
`DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres` records into them.

## Failure semantics

| Dependency unavailable                     | What happens                                                                                                                                          |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| The transport, at the moment of the change | Nothing. The change commits, the message is staged, and the relay delivers when the transport returns                                                 |
| The database, at the moment of the change  | The caller's transaction fails and the message fails with it. Nothing happened, so nothing is announced                                               |
| The transport, at dispatch                 | The message stays staged, its attempt count advances, and the next attempt moves out by the backoff                                                   |
| The relay process, mid-dispatch            | If it died after the confirm and before recording it, the message is dispatched again. **Delivery is at-least-once and consumers must be idempotent** |

At the retry limit the message becomes abandoned. It keeps its payload and its
last failure code, and an operator is alerted, so what was not delivered can be
seen and re-staged. Abandonment is an alert to the operator and does not lose the message. The retry limit exists so
that a permanently poisoned message stops consuming relay ticks.

## What a payload may carry, and how long the row lives

A payload refers to its subjects by identifier and contains no personal data, no
secret, no code and no hash. The staged row stores it as text in the consumer's
own database: nothing encrypts it and no erasure sweep rewrites it. A consumer
whose announcement needs a personal value stages the identifier and lets the
dispatcher read the value at delivery, or seals the value before staging it.

The shipped `StageAsync` refuses a marked payload; keeping other personal data out of the payload is the
consumer's responsibility. The storage half marks the row exempt from anonymization, so a
personal value staged here survives the subject's erasure for as long as the
retention window below keeps the row, and no model validator or erasure sweep
reports it. The storage half's `StageAsync` returns `ValidationFailed` and adds no
row when the payload has a `[RedactData]` marker anywhere the serializer
reaches: on a type, on a member the serializer writes, on a constructor
parameter, or on a value held in a member declared as `object`. It reads markers, not
values: a personal value on an unmarked member still passes, so the consumer
remains responsible for the precondition at its own staging call.

A settled row is not kept forever. The relay deletes published rows older than `PublishedRetention` and abandoned rows
whose last attempt is older than `AbandonedRetention`, after each tick's deliveries. A staged row is never deleted by
age, because it has not been delivered yet; `RetryLimit` decides when the relay stops attempting it. Both windows have
positive defaults and neither accepts a value meaning never; the windows exist to stop the table of payloads growing
without bound.
