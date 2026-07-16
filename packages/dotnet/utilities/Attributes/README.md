<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities — Attributes

> Part of [`DcsvIo.D2.Utilities`](../README.md).

Marker attributes consumed reflectively elsewhere in the stack. Zero behavior on their own — they exist to label types/members so other infrastructure (Serilog destructuring, codegen, contract tests) can pick them up.

| File                     | Contents                                                                                                                                                                                                                   |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RedactDataAttribute.cs` | `[RedactData(Reason = ..., CustomReason = "...")]` — marker attribute consumed by a Serilog destructuring policy (platform hosts typically register via monorepo-private `DcsvIo.D2.Private.ServiceDefaults`; portable hosts register the policy themselves). Targets `AttributeTargets.All` (types, properties, fields, parameters). |

## `[RedactData]` attribute

Marker attribute consumed by a Serilog destructuring policy at host startup. Platform product hosts usually register it via monorepo-private `DcsvIo.D2.Private.ServiceDefaults` (PackageId; not on public export); portable / open hosts that compose logging without that aggregator must register the `[RedactData]`-aware policy themselves. Apply to types, properties, fields, parameters — anywhere PII or secrets might leak into logs/spans/metrics.

```csharp
public sealed record User
{
    public required string Id { get; init; }

    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Email { get; init; }

    [RedactData(Reason = RedactReason.SecretInformation, CustomReason = "OAuth bearer token")]
    public required string AccessToken { get; init; }
}
```

`RedactReason` values: `Unspecified`, `PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`. See [`Enums/README.md`](../Enums/README.md) for the full enum.
