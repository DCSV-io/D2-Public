<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# public/docs/adrs

> **Visibility: PUBLIC** — ADRs that document the open libraries in this repo and their true support on the public surface. Monorepo layout, product hosts, factory, and product-composition ADRs live under the private monorepo and are not required for a public clone.

Architectural decision records that ship with the open surface.

## Law

- Every ADR file under this folder carries a **Visibility: PUBLIC** banner.
- Content documents **general-utility open packages** (or their support/contracts) only — no product IP, monorepo layout/export operator law, non-export runbooks, or private paths as clone requirements.
- Numbering may be discontinuous (open set is pick-and-choose).
- **Eligibility** of packages/tools under `public/` is monorepo operator law (not an open ADR). Monorepo dual-tree layout lives as **private ADR-0026** (path in the product monorepo only: `private/docs/adrs/0026-public-private-monorepo-layout.md` — not exported).
- Product composition / host / IDL factory / service-structure ADRs (e.g. ADR-12/13/20/21/22/23) live under the private monorepo only — they are **not** open SoT. Do not add that class under `public/docs/adrs/`.

## Index (open libraries + residual private-product id rows)

| ADR | Title |
| --- | --- |
| [0001](0001-contacts-folded-owned-component.md) | Contacts as a folded owned component |
| [0002](0002-spec-driven-codegen.md) | Spec-driven codegen |
| [0003](0003-d2result-errors-as-values.md) | D2Result — errors as values |
| [0004](0004-i18n-tkmessage.md) | i18n TKMessage |
| [0005](0005-handler-pipeline.md) | Handler pipeline |
| [0006](0006-abstractions-implementation-split.md) | Abstractions / implementation split |
| [0007](0007-request-context-propagation.md) | Request-context propagation |
| [0008](0008-caching-marker-interfaces.md) | Caching marker interfaces |
| [0009](0009-async-messaging-encrypted-payloads.md) | Async messaging with encrypted payloads |
| [0010](0010-observability-dual-enrichment.md) | Observability dual enrichment |
| [0011](0011-pii-redaction-logging-safety.md) | PII redaction / logging safety |
| ADR-12 (private product — see monorepo private/docs/adrs; not public SoT) | Self-rolled .NET auth |
| ADR-13 (private product — see monorepo private/docs/adrs; not public SoT) | Service-defaults composition root |
| [0014](0014-resilience-primitives.md) | Resilience primitives |
| [0015](0015-anonymization-data-governance.md) | Anonymization / data governance |
| [0017](0017-ef-as-ddd-persistence.md) | EF-as-DDD persistence |
| [0018](0018-spec-driven-error-codes.md) | Spec-driven error codes |
| [0019](0019-wrapped-result-wire-model.md) | Wrapped-result wire model |
| ADR-20 (private product — see monorepo private/docs/adrs; not public SoT) | Service project structure |
| ADR-21 (private product — see monorepo private/docs/adrs; not public SoT) | Unified operation contract IDL |
| ADR-22 (private product — see monorepo private/docs/adrs; not public SoT) | Service auth — mint once, forward |
| [0024](0024-contract-api-versioning-strategy.md) | Contract / API versioning strategy |
| [0025](0025-request-context-establishment.md) | Request-context establishment |
