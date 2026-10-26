<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0001: Contacts are a folded owned-component library, not a standalone contacts service or per-service contacts DB

- **Status**: Accepted
- **Date**: 2026-05-30

## Context

"Contacts" (a person's or organization's name, email, phone, postal address) can be modeled as a standalone contacts
service, as a per-service contacts database, or as value objects folded into the entity that owns them. This ADR
records the choice of the last.

A contact is almost always a *subset of a host entity*. The cross-service machinery of the standalone-service and
per-service-database models - a per-service contacts database, a required external-key triple that names the owning
service, repository handlers, and delivery-time resolution of contacts by a separate service - exists only to support
a contact-PII centralization the project does not want. That machinery is unnecessary once contacts fold into their
host entities.

The EF mapping is a pure VO-as-property model mapped via infra `IEntityTypeConfiguration<T>`, using EF complex types
for multi-field VOs and value converters for single-value VOs. Empirical spikes confirmed this model for domain purity
(no EF references on domain types), SQL cleanliness (JOIN-free SQL), and engine compatibility. Research inputs: EF
Core 10 complex-type + value-converter capabilities + reusable-mapping patterns; DDD value-object-vs-entity modeling
(snapshot-vs-reference for invoice addresses); GDPR data-minimization + right-to-erasure for per-host PII.

## Decision

`DcsvIo.D2.Contacts` is a **.NET-only composable PII value-object toolkit + reusable EF Core mapping**. It is **not a
service, not a database, and exposes no cross-service contact lookup**. Contacts are **folded into each consuming
service's own aggregates, tables, and `DbContext`**.

**Composable PII value objects** (`Personal`, `NameAffixes`, `Demographics`, `Professional`, `EmailAddress`,
`PhoneNumber`) in `DcsvIo.D2.Contacts`, each `Create(...) -> D2Result<T>`, pure-domain, `[RedactData]` on PII, no EF
references. A separate **reusable EF mapping** library (`DcsvIo.D2.Contacts.EntityFrameworkCore`) ships per-VO
helpers; no contacts DB, no contacts `DbContext`, no migrations.

**VO-as-property model.** Host aggregates have plain VO-typed CLR properties - no EF references, no EF attributes on
domain types. All EF mapping (columns, converters, lengths, indexes, anonymization) is defined in the host's infra
`IEntityTypeConfiguration<T>`. These CLR-shape concessions are not EF dependencies: VOs use `required init` properties
(EF 10 materializes them via `GetUninitializedObject` + property setters - no parameterless ctor needed) and every
mapped member is EF-settable via `init`.

**EF mechanism split.** Multi-field VOs (`Personal`, `NameAffixes`, `Demographics`, `Professional`) -> EF **complex
types** (`ComplexProperty`) - first-class queryable member columns, clean JOIN-free SQL, value semantics. Single-value
VOs (`EmailAddress`, `PhoneNumber`) -> EF **value converters** - one column, native unique index, root-scoped
anonymize templates. `OwnsOne` / `OwnsMany` are not used: the anonymization engine's bulk `ExecuteUpdate` erasure
cannot reach owned child-table rows or `.ToJson()` blobs. Unbounded erasable collections with contact fields become
first-class root entities (their own FK and metadata, with contact VOs mapped once).

**Personal correlation `HashId`.** `"v1." + SHA-256` over the normalized First|Middle|Last, **excluding**
PreferredName - so a display-name change leaves the digest stable. This is **correlation, not dedup** - contacts are
PII; Location's hash-dedup trait deliberately does not extend to them. `Professional` has no `HashId`.

**NameAffixes / Demographics split.** Honorific affixes (`Prefix`/`Suffix`, closed taxonomy + `Other` escape hatch) and
demographics (`DateOfBirth` / `BiologicalSex`) are separate optional VOs from the core `Personal` name, so a host
composes only what it needs.

**Field-constraints catalog.** A spec-driven codegen catalog (`contracts/validation/field-constraints.spec.json`)
emits shared field-length `FieldConstraints` constants + the taxonomy enums (`NamePrefix` / `NameSuffix` /
`BiologicalSex`) to .NET (`DcsvIo.D2.Validation.Abstractions`) and TypeScript (`@dcsv-io/d2-validation-abstractions`).
The VO `Create` gates, Location, and frontend Zod schemas consume them - one source of truth for field-length caps.

**Consumes `DcsvIo.D2.DataGovernance`** (the standalone anonymization engine - ADR-0015). The library ships no
sweeper; host entities are marked `IUserOwned` / `IOrgOwned` / `IAnonymizationTrackable` and VO columns are decorated
via the fluent `.Anonymize*` API from `DcsvIo.D2.DataGovernance.EntityFrameworkCore`.

**EF mapping packages.** EF mapping is split per-area into independent siblings:
`DcsvIo.D2.Contacts.EntityFrameworkCore` (contact-VO helpers) and `DcsvIo.D2.Location.EntityFrameworkCore`
(`MapStreetAddress` / `MapAdminLocation` / `MapCoordinates` + `LocationVoDecorator`) with no dependency between them.
A shared generic `DcsvIo.D2.EntityFrameworkCore` owns the VO-agnostic `CreateD2Index<TEntity>` typed migration helper
(the EF-10 complex-member-index workaround - see Consequences). `DcsvIo.D2.Location` is in `location/core/` and
`DcsvIo.D2.Contacts` is in `contacts/core/`. `DcsvIo.D2.DataGovernance.EntityFrameworkCore` has a native
`ComplexTypePropertyBuilder<T>` `.Anonymize*` overload, so both EF libs use the fluent path uniformly.

**Correlation + erasure keys.** Every contact-bearing host entity has optional `Guid? UserId` /
`Guid? OrgId`; guests/externals fall back to the channel address. Erasure is a subject-id fan-out via
`DcsvIo.D2.DataGovernance`; legal-hold rows are field-overwritten with tombstone values in place.

**Delivery preferences + consent** are a separate concern owned centrally by a delivery service outside this library
(suppression keyed by channel address; routing prefs keyed by subject id).

## Consequences

**Positive.**

- No contacts service, no contacts DB, no cross-service lookup / SAGA / cache-eviction apparatus.
- PII is purpose-limited per host (GDPR-aligned data-minimization); no central PII store that concentrates the risk, and
  no cross-service PII store.
- **Subject-keyed erasure and correlation** are achieved by marking host entities with the `DcsvIo.D2.DataGovernance`
  markers and decorating VO columns via the fluent `.Anonymize*` API; the engine sweeps the host model. No bespoke
  sweeper ships.
- Direct reuse of `DcsvIo.D2.Validation` + `DcsvIo.D2.Location` libraries; one-line EF folding for consumers.
- Complex-type VOs are first-class queryable columns (`WHERE personal_first_name = 'John'`,
  `ORDER BY professional_company_name`) - no JOIN overhead, no shadow-key columns.

**Negative / risks (with mitigations).**

- **Migration cascade** - a contact-shape change can require a migration in every adopting service (migrations live in
  the host context). Mitigated by **semver + additive-only-within-a-major**, per-service adoption cadence, and keeping
  the library pure (no business behavior); `DcsvIo.D2.Validation` + `DcsvIo.D2.Location` pin transitively so
  validation cannot skew independently of shape.
- **Validation version skew** across services on different library versions - bounded by additive-only discipline +
  transitive pinning.
- **EF Core 10 complex-member-index limitation** - model-aware indexes on `ComplexProperty` member columns are not
  expressible in EF 10 (fluent `HasIndex(u => u.Vo.Member)` throws; metadata-path indexes are silently discarded at
  finalization). The only EF-10 path is a raw `migrationBuilder.CreateIndex(...)` line in the host migration. The
  toolkit ships `CreateD2Index<TEntity>(u => u.Vo.Member)` (`DcsvIo.D2.EntityFrameworkCore`) to keep that line typed
  and short. EF 11 (issue [#31246](https://github.com/dotnet/efcore/issues/31246), merged 2026-05-19, shipping
  ~Nov 2026) makes `HasIndex(u => u.Vo.Member)` native; migrating existing `CreateD2Index` calls to fluent `HasIndex`
  once the host adopts EF 11 is a tracked follow-up. Value-converter indexes and complex-member _queries_ have no such
  limitation.
- **Shared-kernel coupling** - contained by shipping only pure data + mapping + validation, no business behavior.

## Alternatives considered

- **Standalone per-service contacts DB.** Rejected: it needs a whole DB / `DbContext` / migration / repository /
  ext-key apparatus to support a cross-service resolution the folded model removes; the external-key triple exists
  mainly to support centralized contact storage and delivery-time resolution by a separate service, neither of which
  the project wants.
- **Content-addressable contact** (Location's hash-dedup model). Rejected: contacts are PII, where dedup does not
  belong. Contacts take only Location's structural trait: they are *folded into a host entity*, never
  *content-addressed*.

## References

- Per-lib READMEs for the shipped surface: [`contacts/core/`](../../packages/dotnet/contacts/core/README.md) -
  [`contacts/entity-framework-core/`](../../packages/dotnet/contacts/entity-framework-core/README.md) -
  [`location/entity-framework-core/`](../../packages/dotnet/location/entity-framework-core/README.md) -
  [`entity-framework-core/`](../../packages/dotnet/entity-framework-core/README.md).
- ADR-0015 ([`0015-anonymization-data-governance.md`](0015-anonymization-data-governance.md)) - the standalone
  anonymization engine this library consumes.
- Research inputs: EF Core 10 complex types + value converters + reusable mapping; DDD snapshot-vs-reference; GDPR
  data-minimization + right-to-erasure.
- [Michael Nygard's ADR essay](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) - the format
  this record follows.
