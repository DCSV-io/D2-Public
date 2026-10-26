<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# outbox/

> Parent: [`packages/dotnet/`](../README.md)

The transactional outbox for outward announcements. Staging a message joins the
caller's own unit of work, so the change and the staged message become
durable together or not at all. A broker that is down delays delivery instead of
losing it, and a rolled-back change announces nothing.

The cluster is split along the database boundary. The database-agnostic package
contains the message states, the ports a consumer calls, the retry and abandon
policy inputs, and the dispatcher registry; it runs nothing. The storage package
contains the row, its mapping and the staging implementation, and it is a
separate package because the staged row has to be in the same database as the change
it is staged with, which is the property that makes the outbox transactional. It binds
no provider. The relay binds one, because it drains the table under a PostgreSQL
advisory lock, so the relay is a leaf package of its own and a service that
stages messages without running one takes no database driver with the row.

Delivery is at-least-once: a relay that dies between a transport confirm and
recording it dispatches the message again, so consumers must be idempotent.

## Packages

| Package                                                                       | Description                                                                                                                                                                                                                |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                                                     | `IMessageOutbox<TDb>` / `IOutboxDispatcher`, the staged / published / abandoned message states, `OutboxOptions` / `OutboxOptionsFor<TDb>` and the `AddD2Outbox<TDb>` registration surface. No storage and no runtime work. |
| [`entity-framework-core/`](entity-framework-core/README.md)                   | `OutboxMessageRow` and its EF mapping, `EfMessageOutbox<TDb, TDbContext>` staging onto the consumer's context, `EfOutboxStore<TDb, TDbContext>`, and `OutboxTelemetry`. Provider-neutral.                                  |
| [`entity-framework-core-postgres/`](entity-framework-core-postgres/README.md) | The `OutboxRelay<TDb>` hosted service that claims, dispatches and records under a PostgreSQL advisory lock, and `AddD2OutboxRelayPostgres<TDb>`.                                                                           |
