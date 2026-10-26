<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0019: Wrapped-result wire model - a `D2Result` crosses any transport faithfully, with zero caller try-catch

- **Status**: Accepted
- **Date**: 2026-06-09

## Context

`D2Result` is the single in-memory representation of an operation outcome (ADR-0003): success or one of a finite set
of failures, containing `success`, exact HTTP `status`, `errorCode`, `category`, `messages` (`TKMessage[]`),
`inputErrors`, `traceId`, and `data`. The framework's design goal is that a caller of *any* downstream - a gRPC
service, a REST endpoint - writes `var r = await <call>(...);`, gets a `D2Result<T>`, and branches on it. No
`try-catch` at the call site; transport faults are already `D2Result`s by the time the caller sees them; business
failures (404 / 409 / 400) cross the wire losslessly and re-materialize as the exact same `D2Result` the producer
returned.

The in-memory `D2Result` plus a strong supporting foundation already exist - `TKMessage` objects (not raw key
strings), structured `InputError` records, spec-pinned envelope / ProblemDetails field names, the merged registry, and
the typed `ErrorCategory` of ADR-0018. This decision adds per-transport pieces on that foundation:

- **The codec** - serialize a `D2Result` onto the wire and re-materialize it on the other side losslessly.
- **The resilience wrapper** - catch a transport / infrastructure fault and turn it into an informative `D2Result`, so
  the caller never writes `try-catch`.

Without them the wire loses information in specific ways. On gRPC there would be no general business-result serializer;
the only mechanism would be an auth-only trailer writer that maps a result into a lossy 3-way gRPC status bucket (404 /
409 / 400 collapsed). On HTTP, producers would emit RFC 7807 ProblemDetails but nothing would read one back: a
`application/problem+json` 401 would decode to a featureless `D2Result` with `d2_error_code` / `d2_messages` /
`d2_input_errors` silently dropped - indistinguishable from a 401 from a host that sends no D2 fields.

A second constraint is that the codec must structurally prevent these failure modes: logging the raw transport exception
(which leaks `Status.Detail` / broker-host detail through `ex.Message`), and copying `err.details` / `err.message`
directly into the user-facing `messages[]` array (both an info leak and an untranslated string sent to the client). Both
violate the project's PII / logging discipline, so the codec makes them unreachable.

The decision therefore adds the codec + resilience wrapper per transport on top of the existing specs and resilience
primitives. It is a small addition, not a new design.

## Decision

A `D2Result` crosses any transport faithfully and re-materializes without loss, and the boundary between a **business
failure** and a **transport fault** is explicit; the codecs and wrappers below each depend on it.

**The business-vs-transport boundary.** A business failure (404, 409, 400, ...) is a normal, expected outcome and
is sent over the wire *as data*: `success=false` inside the result that the transport carries. The transport itself
succeeded. `RpcException` / gRPC non-OK status / HTTP transport errors are reserved for *genuine* transport or
infrastructure faults (the channel broke, the auth middleware rejected the JWT, the host was unreachable). Conflating
the two - forcing a business 409 into a non-OK gRPC status - is the fidelity loss this model exists to avoid.

**gRPC carries the result as an envelope field in a normal OK response.** A service response message embeds a
`D2ResultProto` (`contracts/protos/d2/common/v2/d2_result.proto`) as field 1, with the typed payload in sibling
fields. A business failure returns a normal gRPC response with `success=false` in the embedded proto - it never
throws. The exact HTTP status crosses as `int32 status_code`, so 404 / 409 / 400 are preserved losslessly; `category`
is field 4 (a snake-case wire string), so a consumer gets the typed class without importing the producer's
catalog; `messages` and `input_errors` contain full `TKMessage` fidelity (key + params) via `TKMessageProto`. A
host-supplied auth trailer writer (`RpcException` + `Unauthenticated` / `Unavailable`) is used **only** for the
JWT-middleware transport-reject path - a genuine auth fault, distinct from an `Unauthorized()` `D2Result` from a
handler.

**HTTP carries the same `D2Result` via RFC 7807 ProblemDetails `d2_*` extensions.** The producers emit `d2_error_code`
/ `d2_messages` / `d2_input_errors` / `d2_category` / `traceId` as ProblemDetails extensions; a host-supplied HTTP
client re-materializes them (content-type-discriminated on `application/problem+json`, reading the spec-driven
`ProblemDetailsExtensionKeys.*` constants verbatim - never normalized, never hand-typed). This gives parity with the
gRPC envelope across the HTTP boundary.

**Codecs do the lossless serialize / re-materialize.** On .NET, `DcsvIo.D2.Result.Grpc` provides `D2Result.ToProto()`
and `D2ResultProto.ToD2Result<TData>(data)` (C# 14 extension members); on node, host-supplied codecs provide
`d2ResultToProto` / `d2ResultFromProto` (or an equivalent re-materialize path against the same proto). Optional-string
proto fields rehydrate via `Falsey()` so "absent" != "empty string"; an unknown `category` wire string parses to null
/ undefined via `TryFromWire`, never throwing (deploy-skew tolerance).

**Resilience wrappers map transport faults to an informative `D2Result` - zero caller try-catch.**
`AsyncUnaryCall<TProto>.HandleAsync<TData>(resultSelector, dataSelector, logger?, traceId?)` (.NET) and
its host-supplied node equivalent await the call, re-materialize on success, and
on a transport fault return a pre-built factory result: `RpcException` -> `ServiceUnavailable`
(with `Cancelled(12)` -> `Canceled`), <!-- british-spelling:exempt-line -->
`OperationCanceledException` -> `Canceled`, any other exception -> `UnhandledException`. The selectors are statically
typed against the generated response message, so a proto-field rename is a compile error - no reflection, no
marker-interface convention. Transient-fault classification (`IsTransientGrpcException` on .NET and a host-supplied
equivalent on node, gRPC codes 4 / 8 / 10 / 13 / 14) is in the codec lib, not the resilience lib, so the resilience
primitives stay gRPC-free; the predicate is passed to `RetryHelper.RetryD2ResultAsync`. On HTTP, a host-supplied
fetch wrapper has the same shape:
`AbortError` -> `Canceled`, `TimeoutError` -> `REQUEST_FAILED`, network error -> `UnhandledException`.

**PII safety is part of the codec.** The failure modes listed above are structurally prevented. On
.NET, `[LoggerMessage]` never accepts an `Exception`; transport faults are logged via
`SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` + the safe `ex.StatusCode` enum, never
`RpcException.Message`. On node, the user-facing `messages[]` is always a `TKMessage` constant
(`TK.common.errors.SERVICE_UNAVAILABLE` / `UNHANDLED_EXCEPTION` / `CANCELED` / `UNAUTHORIZED`), never `err.details` /
`err.message`; the safe numeric `err.code` is logged separately if at all. On the HTTP parse-back, the raw `title` /
`detail` / `instance` (operator English, not translation keys) are never copied into `messages` - only the structured
`d2_messages` `TKMessage[]` render.

**Cross-runtime codec parity is proven by fixtures.** A parity test asserts the `D2ResultProto` field set matches the
`d2result-envelope` spec and both runtimes' `D2Result` property sets; a fixture-based round-trip feeds a .NET-serialized
proto through node's `d2ResultFromProto` (and vice versa) and asserts equal field values, so the codecs round-trip
identically. Protos remain their own source of truth - there is no contracts->proto generator; the compiler and the
parity tests catch drift.

## Consequences

**Positive.**

- A caller of any downstream writes no `try-catch`. Transport faults arrive as `ServiceUnavailable` / `Canceled` / `UnhandledException` `D2Result`s; business failures arrive as the exact `D2Result` the producer returned.
- Status fidelity is lossless on gRPC: 404 vs 409 vs 400 survive as `int32 status_code`, not collapsed into a 3-way gRPC bucket.
- The same `D2Result` shape (including `category`) crosses both gRPC and HTTP, so a consumer's branching logic is
  transport-agnostic - and the HTTP parse-back keeps an auth 401 distinguishable from a
  401 from a host that sends no D2 fields.
- The business-vs-transport split keeps gRPC status semantics accurate (`OK` for a business failure, because the RPC
  itself succeeded) and keeps `RpcException` / trailers reserved for real transport / auth faults.
- The envelope model has no ~8KB trailer bound, so large `inputErrors` are sent intact, and it is per-message - it extends
  to server-streaming.
- PII safety is structural at the boundary: no `ex.Message` / `err.message` / `Status.Detail` / ProblemDetails `Detail` can reach a user-facing message or a log line.

**Negative / risks.**

- gRPC has both a business envelope and an auth transport-reject trailer as result-on-wire mechanisms. The
  boundary is documented, but it must not drift - a 401 from auth middleware is a different thing from an
  `Unauthorized()` handler result.
- The envelope requires a `D2ResultProto result = N` field on every response message and generated proto stubs, i.e. a
  real proto-codegen toolchain. This decision adds the proto toolchain only for the `D2ResultProto` stub (both
  runtimes); the broad per-service proto toolchain is separate infrastructure work that this decision does not include.
- The typed payload is in sibling response fields and is combined with the envelope in `ToD2Result`. The case of `SomeFound`
  with `success=false` *and* data present must be pinned by round-trip tests on both runtimes.
- Several transport codecs/wrappers are out of scope and not built: the node `@dcsv-io/d2-messaging` runtime, SignalR /
  gRPC push, SSE, the SSR gateway client, and the node gRPC *server* boilerplate. The model is decided; the transports
  not yet built depend on their owning service being built.
- RabbitMQ deliberately does **not** carry a `D2Result` in the payload (the payload is the domain DTO); the result lives
  in the publisher return and the consumer-handler return that drives ACK vs DLQ. This is correct but is a different
  shape from the sync codec, which engineers must keep distinct.

## Alternatives considered

**Trailer-patching the business result onto an `RpcException`.** Carry the whole `D2Result` across gRPC trailers on a
forced non-OK status. Rejected on every axis: it forces a non-OK gRPC status (lossy 3-way bucket -> status fidelity
loss needing recovery), every field becomes a string-typed trailer (large `inputErrors` can exceed the ~8KB trailer
bound), the typed `data` payload has nowhere to live on a failure-only exception, trailers are once-per-call so they
can't carry per-message streaming results. The trailer writer survives only for the genuine auth transport-reject.

**Exceptions as control flow across the wire.** Let business failures throw and be caught at a middleware boundary. Rejected for the same reasons ADR-0003 rejected exceptions in-process, amplified by the wire: the failure surface is invisible in the call signature, partial-success (`SOME_FOUND`) is awkward, and it reintroduces the `try-catch`-at-every-call-site cost the whole model exists to remove.

**Bespoke per-call error handling.** Let each call site hand-write its own `RpcException` / fetch-error mapping.
Rejected: it is the absence of a decision - it guarantees inconsistent status mapping, duplicated transient-fault
logic, and recurring PII leaks where a developer reaches for `ex.Message` because nothing stopped them. A single
codec + wrapper per transport makes the safe path the only path.

## References

- `contracts/protos/d2/common/v2/d2_result.proto` - the `D2ResultProto` envelope (with `category` field 4);
  `contracts/d2result-envelope/d2result-envelope.spec.json` - the JSON envelope field-name spec;
  `contracts/problem-details/problem-details.spec.json` - the ProblemDetails extension keys (incl. `d2_category`).
- `packages/dotnet/result/grpc/ProtoExtensions.cs` - `ToProto` / `ToD2Result<T>` / `HandleAsync<T>` /
  `IsTransientGrpcException`, with `SanitizedExceptionRender` PII discipline; `packages/dotnet/result/grpc/README.md`.
- Host-supplied gRPC client codec (`d2ResultToProto` / `d2ResultFromProto`) and HTTP ProblemDetails producers under
  `packages/dotnet/aspnetcore/Internal/` plus host auth middleware.
- [ADR-0003](0003-d2result-errors-as-values.md) - `D2Result` as the single in-memory outcome type this serializes
  faithfully. [ADR-0018](0018-spec-driven-error-codes.md) - the spec-driven codes + `ErrorCategory` the envelope
  carries. [ADR-0009](0009-async-messaging-encrypted-payloads.md) - the async-messaging boundary, where the `D2Result`
  is carried in the publisher / consumer return rather than the (encrypted) payload.
