<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# D2 Auth (public surface)

**Portable auth vocabulary only** (open support packages). Catalogs and context ports that Context / Handler / Messaging graphs depend on.

## Public packages (this tree)

| Package | Role |
| --- | --- |
| `DcsvIo.D2.Auth.Abstractions` | Scopes, audiences, JWT claim catalogs, JWKS/session ports |
| `DcsvIo.D2.AuthContext.Abstractions` | Auth context shape consumed by request-context |
| `scopes|audiences|protocol-audiences|jwt-claims-source-gen` | Catalog generators |

## Private product composition (not under `public/`)

Full inbound JWT runtime, transport bindings, outbound mint/forward, events, ServiceDefaults, and related source-gens live under **`private/packages/dotnet/auth/*`** and **`private/packages/dotnet/service-defaults/`** as `DcsvIo.D2.Private.*` PackageIds (AssemblyName policy A keeps open pre-move assembly names for IVT/generators). See monorepo `private/packages/README.md` and private ADR-0012 / ADR-0022.
