<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# D2

**Apache-2.0 libraries for .NET and TypeScript** — errors-as-values, typed
error catalogs, auth vocabulary & request context, resilient callers, tiered
caches, payload encryption, RabbitMQ messaging, geo & reference data, contacts
& location value objects, validation, i18n, handlers, ASP.NET helpers, logging
& OpenTelemetry — as installable NuGet and npm packages.

This repo is **portable app-building libs only**. Full inbound JWT runtime,
host mega-aggregators, product boundary gRPC clients, IDL/codegen factory
tools, and other product-specific composition kits are **not published here**
(they are not part of this open library surface).

| | |
| --- | --- |
| **License** | [Apache License 2.0](LICENSE) |
| **NuGet** | [`DcsvIo.D2.*`](https://www.nuget.org/profiles/dcsv-io) |
| **npm** | [`@dcsv-io/d2-*`](https://www.npmjs.com/org/dcsv-io) |
| **Releases** | [GitHub Releases](https://github.com/DCSV-io/D2-Public/releases) |
| **Issues** | [GitHub Issues](https://github.com/DCSV-io/D2-Public/issues) |
| **Open inventory** | **46** versioned NuGet packages · **32** npm packages (catalog source-gen shells may exist on disk for parents; they are not separate publish rows) |

```bash
# .NET
dotnet add package DcsvIo.D2.Result

# TypeScript
pnpm add @dcsv-io/d2-result
```

See also: [`DcsvIo.D2.Result`](packages/dotnet/result/core/README.md) ·
[`@dcsv-io/d2-result`](packages/typescript/result/README.md)

---

## What you get

D2 is a **set of composable libraries**, not a single app or host. Install what
you need and wire it into *your* services.

| You need… | D2 gives you… |
| --- | --- |
| **Errors without exception soup** | [`DcsvIo.D2.Result`](packages/dotnet/result/core/README.md) / [`@dcsv-io/d2-result`](packages/typescript/result/README.md) — typed success/failure, semantic factories, i18n-ready user messages |
| **Stable error codes across languages** | Spec-driven catalogs → [`DcsvIo.D2.ErrorCodes.Registry`](packages/dotnet/error-codes/registry/README.md) / [`@dcsv-io/d2-error-codes-registry`](packages/typescript/error-codes-registry/README.md) that stay in lockstep |
| **Portable auth vocabulary** | [`DcsvIo.D2.Auth.Abstractions`](packages/dotnet/auth/abstractions/README.md) + [`AuthContext.Abstractions`](packages/dotnet/auth/context-abstractions/README.md) — scopes, audiences, JWT claims, JWKS/session ports (full inbound JWT runtime is product composition, not an open install package) |
| **Safe workload identity** | [`DcsvIo.D2.Spiffe`](packages/dotnet/workload-identity/README.md) — SPIFFE SAN / trust-domain grammar for mTLS peers |
| **Caches that stay coherent** | [`DcsvIo.D2.Caching.*`](packages/dotnet/caching/abstractions/README.md) / [`@dcsv-io/d2-caching-*`](packages/typescript/caching/abstractions/README.md) — local, Redis, tiered + invalidation backplane |
| **Encrypted async messaging** | [`DcsvIo.D2.Messaging.RabbitMq`](packages/dotnet/messaging/rabbitmq/README.md) / [`@dcsv-io/d2-messaging-rabbitmq`](packages/typescript/messaging/rabbitmq/README.md) + [`DcsvIo.D2.Encryption`](packages/dotnet/encryption/core/README.md) / [`@dcsv-io/d2-encryption`](packages/typescript/encryption/README.md) |
| **PII that doesn’t leak into logs** | [`DcsvIo.D2.Logging`](packages/dotnet/logging/README.md), [`DcsvIo.D2.Contacts`](packages/dotnet/contacts/core/README.md), [`DcsvIo.D2.DataGovernance.*`](packages/dotnet/data-governance/abstractions/README.md) |
| **Geo & world reference data** | [`DcsvIo.D2.Geo.Default`](packages/dotnet/geo/default/README.md) / [`@dcsv-io/d2-geo-default`](packages/typescript/geo/default/README.md) — full typed catalogs for **countries, subdivisions, currencies, languages, locales, timezones, and geopolitical entities**, with O(1) lookups, nested accessors (e.g. `Subdivisions.US.NY`), cross-links between catalogs, and free-text name resolution — driven from [`contracts/geo/`](contracts/geo/) on both stacks |
| **Locations you can store and dedupe** | [`DcsvIo.D2.Location`](packages/dotnet/location/core/README.md) — coordinates (lat/lon, geohash, plus-code), street addresses, admin hierarchy, content-addressable hashes |
| **Contact details as value objects** | [`DcsvIo.D2.Contacts`](packages/dotnet/contacts/core/README.md) — personal name, affixes, demographics, professional, email, phone — smart constructors, redaction-aware |
| **Validation that matches on both stacks** | [`DcsvIo.D2.Validation`](packages/dotnet/validation/default/README.md) / [`@dcsv-io/d2-validation`](packages/typescript/validation/default/README.md) — email, phone (E.164), postal codes |
| **i18n you can type-check** | [`DcsvIo.D2.I18n.Keys`](packages/dotnet/i18n/keys/README.md) / [`@dcsv-io/d2-i18n-keys`](packages/typescript/i18n-keys/README.md) + runtimes |
| **Handlers with observability baked in** | [`DcsvIo.D2.Handler`](packages/dotnet/handler/core/README.md) — scopes, OTel, log scope, universal catch |
| **ASP.NET Core host helpers** | [`DcsvIo.D2.AspNetCore`](packages/dotnet/aspnetcore/README.md) — CORS, health, ProblemDetails, mTLS helpers (full host mega-aggregators stay product composition, not this package) |

Same specs under [`contracts/`](contracts/README.md) drive much of the .NET and
TypeScript surface, so catalogs and wire shapes don’t drift.

Full per-package docs live next to the code:

- **.NET index** → [`packages/dotnet/README.md`](packages/dotnet/README.md)
- **TypeScript index** → [`packages/typescript/README.md`](packages/typescript/README.md)

---

## Packages at a glance

Package IDs: **NuGet** `DcsvIo.D2.<Name>` · **npm** `@dcsv-io/d2-<name>`  
(early packages may still be pre-stable `0.x` — check the registry for current versions.)

**Every package name below is a link to that package’s README.**

### Core & control flow

| Concern | .NET | TypeScript | Why it matters |
| --- | --- | --- | --- |
| Results | [`DcsvIo.D2.Result`](packages/dotnet/result/core/README.md) · [`DcsvIo.D2.Result.Grpc`](packages/dotnet/result/grpc/README.md) | [`@dcsv-io/d2-result`](packages/typescript/result/README.md) | Errors-as-values; semantic factories instead of throw/catch control flow |
| Error categories & registry | [`DcsvIo.D2.ErrorCodes.Category`](packages/dotnet/error-codes/category/README.md) · [`DcsvIo.D2.ErrorCodes.Registry`](packages/dotnet/error-codes/registry/README.md) | [`@dcsv-io/d2-error-category`](packages/typescript/error-category/README.md) · [`@dcsv-io/d2-error-codes-registry`](packages/typescript/error-codes-registry/README.md) | One closed category set + merged code→metadata lookup |
| Utilities | [`DcsvIo.D2.Utilities`](packages/dotnet/utilities/README.md) | [`@dcsv-io/d2-utilities`](packages/typescript/utilities/README.md) | `Falsey`/`Truthy`, parse helpers, safe JSON — shared boundary habits |
| Resilience | [`DcsvIo.D2.Resilience`](packages/dotnet/resilience/README.md) | [`@dcsv-io/d2-resilience`](packages/typescript/resilience/README.md) | Retry, circuit breaker, single-flight, timeout — opt-in caller-side |
| Time | [`DcsvIo.D2.Time`](packages/dotnet/time/README.md) | [`@dcsv-io/d2-time`](packages/typescript/time/README.md) | Injectable clock + temporal types with cross-runtime parity fixtures |
| Problem Details | [`DcsvIo.D2.ProblemDetails.Abstractions`](packages/dotnet/problem-details/abstractions/README.md) | [`@dcsv-io/d2-problem-details-abstractions`](packages/typescript/problem-details-abstractions/README.md) | RFC 7807 catalog (type URI, extension keys, titles) |

### Auth, identity & context

| Concern | .NET | TypeScript | Why it matters |
| --- | --- | --- | --- |
| Auth vocabulary | [`DcsvIo.D2.Auth.Abstractions`](packages/dotnet/auth/abstractions/README.md) | [`@dcsv-io/d2-auth-abstractions`](packages/typescript/auth/abstractions/README.md) | Scopes, audiences, claims catalogs + JWKS/session ports (JWT runtime middleware is host-supplied) |
| Auth / request context | [`DcsvIo.D2.AuthContext.Abstractions`](packages/dotnet/auth/context-abstractions/README.md) · [`DcsvIo.D2.Context.Abstractions`](packages/dotnet/context/abstractions/README.md) | [`@dcsv-io/d2-auth-context-abstractions`](packages/typescript/auth/context-abstractions/README.md) · [`@dcsv-io/d2-request-context-abstractions`](packages/typescript/request-context-abstractions/README.md) | Who is calling, org, scopes — and how context hops safely |
| Workload identity | [`DcsvIo.D2.Spiffe`](packages/dotnet/workload-identity/README.md) | — | SPIFFE SAN + trust-domain grammar for mTLS peers |
| Wire headers | [`DcsvIo.D2.Headers.Common`](packages/dotnet/headers/common/README.md) · [`DcsvIo.D2.Headers.Http`](packages/dotnet/headers/http/README.md) · [`DcsvIo.D2.Headers.Grpc`](packages/dotnet/headers/grpc/README.md) · [`DcsvIo.D2.Headers.Amqp`](packages/dotnet/headers/amqp/README.md) | [`@dcsv-io/d2-headers-common`](packages/typescript/headers/common/README.md) · [`@dcsv-io/d2-headers-http`](packages/typescript/headers/http/README.md) · [`@dcsv-io/d2-headers-grpc`](packages/typescript/headers/grpc/README.md) · [`@dcsv-io/d2-headers-amqp`](packages/typescript/headers/amqp/README.md) | One catalog for HTTP / gRPC / AMQP header names (route-guard helpers are host-supplied) |

### Caching, crypto & messaging

| Concern | .NET | TypeScript | Why it matters |
| --- | --- | --- | --- |
| Cache ports | [`DcsvIo.D2.Caching.Abstractions`](packages/dotnet/caching/abstractions/README.md) | [`@dcsv-io/d2-caching-abstractions`](packages/typescript/caching/abstractions/README.md) | Local / distributed / tiered markers; every op returns `D2Result` |
| Local / Redis / tiered | [`DcsvIo.D2.Caching.Local.Default`](packages/dotnet/caching/local-default/README.md) · [`DcsvIo.D2.Caching.Distributed.Redis`](packages/dotnet/caching/distributed-redis/README.md) · [`DcsvIo.D2.Caching.Tiered`](packages/dotnet/caching/tiered/README.md) | [`@dcsv-io/d2-caching-local-default`](packages/typescript/caching/local-default/README.md) · [`@dcsv-io/d2-caching-distributed-redis`](packages/typescript/caching/distributed-redis/README.md) · [`@dcsv-io/d2-caching-tiered`](packages/typescript/caching/tiered/README.md) | L1 + Redis L2 + broadcast invalidation (`d2:cache:invalidations`) |
| Encryption | [`DcsvIo.D2.Encryption`](packages/dotnet/encryption/core/README.md) | [`@dcsv-io/d2-encryption`](packages/typescript/encryption/README.md) · [`@dcsv-io/d2-encryption-abstractions`](packages/typescript/encryption-abstractions/README.md) | AES-256-GCM + sealed ECDH frames; KAT-pinned cross-language |
| Messaging | [`DcsvIo.D2.Messaging.Abstractions`](packages/dotnet/messaging/abstractions/README.md) · [`DcsvIo.D2.Messaging.RabbitMq`](packages/dotnet/messaging/rabbitmq/README.md) | [`@dcsv-io/d2-messaging-abstractions`](packages/typescript/messaging-abstractions/README.md) · [`@dcsv-io/d2-messaging-rabbitmq`](packages/typescript/messaging/rabbitmq/README.md) | Topology, encrypt-on-publish, DLQ metadata, idempotency seam |

### Domain value objects & reference data

| Concern | .NET | TypeScript | Why it matters |
| --- | --- | --- | --- |
| Geo types & codes | [`DcsvIo.D2.Geo.Abstractions`](packages/dotnet/geo/abstractions/README.md) | [`@dcsv-io/d2-geo-abstractions`](packages/typescript/geo/abstractions/README.md) | Typed codes/records for countries, subdivisions, currencies, languages, locales, timezones, geopolitical entities; name resolution helpers |
| Geo catalog data | [`DcsvIo.D2.Geo.Default`](packages/dotnet/geo/default/README.md) | [`@dcsv-io/d2-geo-default`](packages/typescript/geo/default/README.md) | Full generated data for all seven catalogs — lookups, nested accessors, cross-catalog links |
| Location | [`DcsvIo.D2.Location`](packages/dotnet/location/core/README.md) · [`DcsvIo.D2.Location.EntityFrameworkCore`](packages/dotnet/location/entity-framework-core/README.md) | — | Coordinates, street address, admin location; hash-deduplicatable |
| Contacts | [`DcsvIo.D2.Contacts`](packages/dotnet/contacts/core/README.md) · [`DcsvIo.D2.Contacts.EntityFrameworkCore`](packages/dotnet/contacts/entity-framework-core/README.md) | — | Name, email, phone, demographics, professional — composable, redaction-aware |
| Validation | [`DcsvIo.D2.Validation`](packages/dotnet/validation/default/README.md) · [`DcsvIo.D2.Validation.Abstractions`](packages/dotnet/validation/abstractions/README.md) | [`@dcsv-io/d2-validation`](packages/typescript/validation/default/README.md) · [`@dcsv-io/d2-validation-abstractions`](packages/typescript/validation/abstractions/README.md) | Email / phone / postal — shared fixture corpus |
| Data governance | [`DcsvIo.D2.DataGovernance.Abstractions`](packages/dotnet/data-governance/abstractions/README.md) · [`DcsvIo.D2.DataGovernance.EntityFrameworkCore`](packages/dotnet/data-governance/entity-framework-core/README.md) | — | GDPR-style anonymization markers + EF wiring |
| EF helpers | [`DcsvIo.D2.EntityFrameworkCore`](packages/dotnet/entity-framework-core/core/README.md) · [`DcsvIo.D2.EntityFrameworkCore.Postgres`](packages/dotnet/entity-framework-core/postgres/README.md) | — | Migrations, advisory-lock startup, complex-property indexes |

### Handlers, host & observability

| Concern | .NET | TypeScript | Why it matters |
| --- | --- | --- | --- |
| Handler pipeline | [`DcsvIo.D2.Handler`](packages/dotnet/handler/core/README.md) · [`DcsvIo.D2.Handler.Abstractions`](packages/dotnet/handler/abstractions/README.md) · [`DcsvIo.D2.Handler.Repo`](packages/dotnet/handler/repo/README.md) · [`DcsvIo.D2.Handler.Repo.Abstractions`](packages/dotnet/handler/repo-abstractions/README.md) · [`DcsvIo.D2.Handler.Repo.Postgres`](packages/dotnet/handler/repo-postgres/README.md) | — | Scope checks, OTel, logging, DB exception → `D2Result` |
| ASP.NET Core | [`DcsvIo.D2.AspNetCore`](packages/dotnet/aspnetcore/README.md) | — | Security headers, CORS, health, ProblemDetails, mTLS helpers (host mega-aggregators are host-supplied) |
| Logging | [`DcsvIo.D2.Logging`](packages/dotnet/logging/README.md) | [`@dcsv-io/d2-logging`](packages/typescript/logging/README.md) | Structured logging + PII redaction culture |
| Telemetry | [`DcsvIo.D2.Telemetry`](packages/dotnet/telemetry/core/README.md) | [`@dcsv-io/d2-telemetry`](packages/typescript/telemetry/README.md) | OpenTelemetry setup (traces / metrics / logs) |
| i18n | [`DcsvIo.D2.I18n`](packages/dotnet/i18n/core/README.md) · [`DcsvIo.D2.I18n.Keys`](packages/dotnet/i18n/keys/README.md) · [`DcsvIo.D2.I18n.Abstractions`](packages/dotnet/i18n/abstractions/README.md) | [`@dcsv-io/d2-i18n`](packages/typescript/i18n/README.md) · [`@dcsv-io/d2-i18n-keys`](packages/typescript/i18n-keys/README.md) · [`@dcsv-io/d2-i18n-abstractions`](packages/typescript/i18n-abstractions/README.md) | Typed message keys + runtime translation |

### Contracts, IDL & protos

| Concern | Path / package | Why it matters |
| --- | --- | --- |
| Shared specs | [`contracts/`](contracts/README.md) | Source of truth for catalogs, headers, error codes, geo, messages… |
| Protos | [`contracts/protos/`](contracts/protos/README.md) · [`@dcsv-io/d2-protos`](packages/typescript/protos/README.md) | Common gRPC / wire types |
| Architecture decisions | [`docs/adrs/`](docs/adrs/README.md) | Why the framework looks the way it does |

Cluster indexes and every package’s own README have APIs, dependencies, and
codegen notes. Prefer those over this overview when you integrate.

---

## Repository layout

| Path | Contents |
| --- | --- |
| [`packages/dotnet/`](packages/dotnet/README.md) | Shared .NET libraries (see index for every package) |
| [`packages/typescript/`](packages/typescript/README.md) | Shared TypeScript packages (see index for every package) |
| [`contracts/`](contracts/README.md) | Specs, schemas, protos, message catalogs that drive both stacks |
| [`docs/adrs/`](docs/adrs/README.md) | Architecture decisions for these open libraries |
| [`D2.Public.slnx`](D2.Public.slnx) | .NET solution for the libraries in this repository |
| [`LICENSE`](LICENSE) | Apache-2.0 |

---

## Build from source (optional)

Most apps should depend on **published packages**. To build the open surface
from a clone:

```bash
dotnet build D2.Public.slnx
dotnet test D2.Public.slnx

# TypeScript — see package READMEs; typically pnpm workspace under packages/typescript
```

SDK / engine pins live in `global.json` and package engines when present.

---

## Versioning

Consumable libraries use **per-package semver** (`0.x` while early). Versions and
notes appear on **NuGet / npm** and on
**[Releases](https://github.com/DCSV-io/D2-Public/releases)**. Each package’s
`CHANGELOG.md` tracks that package only.

Open **publish inventory** is the set of versioned packages under
`packages/{dotnet,typescript}/` that are meant to ship to NuGet/npm. Supporting
catalog **source-gen shells** may sit next to parent packages for build-time
codegen; they are not separate public install products.

---

## About this repository

Source for the portable libraries and contracts above — published so you can
read code next to NuGet/npm releases and open issues. Contribution model
(public mirror; issues welcome, PRs not merged upstream) is in
[CONTRIBUTING.md](CONTRIBUTING.md).

---

## Security

Do not file public issues that include credentials or unreleased exploit detail.
Prefer a private report to DCSV security contacts on the organization profile
when available; otherwise open a high-level issue without an exploit payload and
request a private channel.

---

## License

[Apache License 2.0](LICENSE) — Copyright (c) DCSV.
