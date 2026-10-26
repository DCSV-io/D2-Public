<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Contacts

> Parent: [`packages/dotnet/`](../README.md)

> Audience: backend .NET service engineers composing contact PII value objects + EF mapping into their own aggregates.

A folded owned-component contacts library. It provides composable value objects plus reusable Entity Framework Core
mapping that fold into each host service's own entities and `DbContext`. It owns no database and ships no migrations.

- [`core/`](core/README.md) - `DcsvIo.D2.Contacts`: the composable, self-redacting PII value objects
  (`Personal`, `NameAffixes`, `Demographics`, `Professional`, `EmailAddress`, `PhoneNumber`), each constructed through
  a `Create(...) -> D2Result<T>` smart constructor.
- [`entity-framework-core/`](entity-framework-core/README.md) - `DcsvIo.D2.Contacts.EntityFrameworkCore`: per-VO
  complex-type and value-converter mapping helpers (`MapPersonal`, `MapProfessional`, `MapEmailAddress`, etc.) called
  from the host's `IEntityTypeConfiguration<T>`; wires max-length, converters, and anonymize defaults. Ships
  separately from `core/`.
