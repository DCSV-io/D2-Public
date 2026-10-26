<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities - Enums

> Part of [`DcsvIo.D2.Utilities`](../README.md).

Standard enums shared across the framework. They live here and not in each consuming lib, so the reason and isolation
values are the same in every lib that surfaces them.

| File                | Contents                                                                                                                          |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `IsolationLevel.cs` | DB isolation level enum with phenomena-matrix doc, mirroring the standard SQL isolation taxonomy.                                 |
| `RedactReason.cs`   | Standard reasons for redaction (`PersonalInformation`, `FinancialInformation`, `SecretInformation`, etc.) used by `[RedactData]`. |

## `IsolationLevel`

Standard SQL isolation level enum with phenomena matrix. Values: `ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`, `Serializable`. Use to parametrize EF Core / Npgsql transaction scopes.

| Level             | Dirty reads | Non-repeatable reads | Phantom reads   | Serialization anomaly |
| ----------------- | ----------- | -------------------- | --------------- | --------------------- |
| `ReadUncommitted` | yes         | yes                  | yes             | yes                   |
| `ReadCommitted`   | no          | yes                  | yes             | yes                   |
| `RepeatableRead`  | no          | no                   | yes (not in PG) | yes                   |
| `Serializable`    | no          | no                   | no              | no                    |

> PostgreSQL note: `ReadUncommitted` behaves identically to `ReadCommitted`; `RepeatableRead` does not allow phantom reads.

## `RedactReason`

Closed enum of the reasons a value is redacted from logs / traces / metrics. Set on
`[RedactData(Reason = ..., CustomReason = "...")]` - the Serilog destructuring policy reads both fields and elides the
value at write time. See [`Attributes/README.md`](../Attributes/README.md) for the attribute usage.

Values: `Unspecified`, `PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`.
