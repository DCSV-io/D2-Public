<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/request-context/`

`IRequestContext` interface spec - the per-request runtime context that extends `IAuthContext` with transport-level tracing fields (`traceId`, `correlationId`) and network/enrichment information populated by ASP.NET Core and RabbitMQ middleware.

## Establishment fields

The `Establishment` section declares these fields, which every trust boundary populates fresh on the context it
produces:

| Field             | Type                                | `propagate` | Meaning                                                                                                                                                                                                                                                                                                                                                                      |
| ----------------- | ----------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Origin`          | `RequestOrigin` (non-nullable enum) | no          | What kind of boundary produced this hop's context - an external ingress boundary, a cross-process mTLS-authenticated hop, an in-process module call, or an in-host system worker. The receiving boundary recomputes it locally; a wire-supplied value is never trusted because none is ever serialized. `Unestablished` (the enum's zero member) is the fail-closed default. |
| `ImmediateCaller` | `string?`                           | no          | Who called this hop - the validated mTLS client-certificate workload id on a cross-process hop, or the calling module's own id in-process. `null` on `EdgeInbound` / `System` (no upstream internal workload).                                                                                                                                                               |
| `CallPath`        | `IReadOnlyList<CallPathEntry>`      | **yes**     | The accumulated sequence of hops (`{id, kind, timestamp}`) the request has traversed, oldest-first, depth-bounded by `maxLength`. Operational telemetry only - no authority decision reads it.                                                                                                                                                                               |

`Origin` and `ImmediateCaller` are the only local, unforgeable facts in the spec. Every field with `propagate: true`
travels on `x-d2-context` and is therefore never suitable as an authority input on its own. That includes `CallPath`,
the only propagated list-of-records field in this spec.

`RequestOrigin`, `CallPathKind` and `CallPathEntry` are written by hand, not generated, in
`DcsvIo.D2.Auth.Abstractions`. The generated types of this spec also depend on that library for `ActorEntry`,
`OrgType` and `Role`. [ADR-0007](../../docs/adrs/0007-request-context-propagation.md) section 4 summarizes the design.

## Consumed by

- **.NET** - [`packages/dotnet/context/source-gen/`](../../packages/dotnet/context/source-gen/README.md) (Roslyn source-gen -> `PropagatedContext` + extensions + serializer in `DcsvIo.D2.Context.Abstractions`; the same generator also emits the auth-context layer this one extends)
- **TypeScript** - constants/types in `@dcsv-io/d2-request-context-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
- [ADR-0007](../../docs/adrs/0007-request-context-propagation.md) - the spec-driven context model this spec implements.
- [ADR-0007](../../docs/adrs/0007-request-context-propagation.md) - the propagation model and a summary of the
  establishment fields (`Origin` / `ImmediateCaller` / `CallPath`)
