<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities - Configuration

> Part of [`DcsvIo.D2.Utilities`](../README.md).

Host-side configuration helpers - env-var <-> wire-format conversion, and the named `[Range]` bound strings options
POCOs annotate their `TimeSpan` properties with. Used by every service composition root. The `.env*` file reader lives
in `DcsvIo.D2.EnvFiles`.

| File                        | Contents                                                                          |
| --------------------------- | --------------------------------------------------------------------------------- |
| `ConnectionStringHelper.cs` | URI <-> wire-format converters for Redis / PostgreSQL / RabbitMQ env vars.        |
| `TimeSpanRangeBounds.cs`    | Named bound strings for `[Range]` annotations over `TimeSpan` options properties. |

## `ConnectionStringHelper` - URI <-> wire-format conversion

Bridges standard URI-format env vars (`REDIS_URL=redis://:p@host:6379`, `*_DATABASE_URL=postgresql://u:p@host/db`,
`RABBITMQ_URL=amqp://...`) into the wire formats expected by .NET clients (`StackExchange.Redis`, `Npgsql`). RabbitMQ
accepts AMQP URIs natively - the RMQ helper just reads the env var.

```csharp
// Inside Geo.API/Program.cs:
var redis    = ConnectionStringHelper.GetRedis();
var postgres = ConnectionStringHelper.GetPostgres("GEO_DATABASE_URL");
var rabbit   = ConnectionStringHelper.GetRabbitMq();

// Or parse a known string directly (no env var read):
ConnectionStringHelper.ParseRedisUri("redis://:secret@host:6380");
// "host:6380,password=secret"

ConnectionStringHelper.ParsePostgresUri("postgresql://u:p@h:5433/db");
// "Host=h;Port=5433;Username=u;Password=p;Database=db"
```

Pass-through for already-converted values: `ParseRedisUri("h:6380,password=x")` -> unchanged,
`ParsePostgresUri("Host=h;Port=5432;Database=db")` -> unchanged. Defaults: Redis port `6379`, PostgreSQL port `5432`.
URL-encoded credentials (`%40` -> `@`, `%3A` -> `:`) are unescaped automatically.

`Get*` overloads throw `InvalidOperationException` with a "Check your `.env.local` file." hint when the env var is
missing or empty - never silently fall back to localhost.

## `TimeSpanRangeBounds` - named bounds for `[Range]`

`RangeAttribute` parses its `TimeSpan` bounds from compile-time strings and has no minimum-only form, so an options
property whose real constraint is a floor still has to state a maximum. `NO_CEILING` is `TimeSpan.MaxValue` written
out, so the annotation shows that the property has no upper limit:

```csharp
[Range(typeof(TimeSpan), "00:00:01", TimeSpanRangeBounds.NO_CEILING)]
public TimeSpan RotationCheckInterval { get; set; }
```

The floor differs per property. The upper bound is the same everywhere and reads as "no ceiling" and not as a value
somebody chose.
