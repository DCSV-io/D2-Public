<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# D2 Auth (public surface)

This tree contains portable auth vocabulary only, in open support packages: the catalogs and context ports that the
Context, Handler and Messaging package graphs depend on.

## Public packages (this tree)

| Package                                                                                               | Role                                                      |
| ----------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| `DcsvIo.D2.Auth.Abstractions`                                                                         | Scopes, audiences, JWT claim catalogs, JWKS/session ports |
| `DcsvIo.D2.AuthContext.Abstractions`                                                                  | Auth context shape consumed by request-context            |
| `scopes-source-gen`, `audiences-source-gen`, `protocol-audiences-source-gen`, `jwt-claims-source-gen` | Catalog generators                                        |

## Host-supplied (not in this repo)

Full inbound JWT runtime, transport bindings, outbound mint/forward, and related host composition are not published
here. Hosts supply JWT middleware and session/liveness adapters that use the catalogs and ports above.
