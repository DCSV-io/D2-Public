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

## Host-supplied (not in this repo)

Full inbound JWT runtime, transport bindings, outbound mint/forward, and related host composition are **not published here**. Hosts supply JWT middleware and session/liveness adapters that bind the catalogs and ports above.
