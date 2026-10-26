<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# data-governance/

> Parent: [`packages/dotnet/`](../README.md)

GDPR anonymization framework. The cluster provides the vocabulary and marker interfaces that every service shares
(`abstractions/`) and the EF Core engine, fluent decoration API, and startup model guard (`entity-framework-core/`).

- [`abstractions/`](abstractions/README.md) - `DcsvIo.D2.DataGovernance.Abstractions`: GDPR-anonymization markers
  (`IUserOwned`, `IOrgOwned`, `IExemptFromAnonymization`, `IAnonymizationTrackable`), the `[Anonymizable]` attribute,
  `AnonymizeKind` / `AnonymizationRule` / `AnonymizationOutcome`, and the `IAnonymizationEngine` seam. Its project
  references neither EF Core nor DI.
- [`entity-framework-core/`](entity-framework-core/README.md) - `DcsvIo.D2.DataGovernance.EntityFrameworkCore`:
  fluent `.Anonymize*` extension methods, `AnonymizableAttributeConvention`, `AnonymizationEngine` (Tier-A/B subject
  erasure), `AnonymizationModelValidator` (deny-by-default startup guard), and the `AddD2DataGovernance` DI entry point.
