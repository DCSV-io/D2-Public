<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Result.Grpc

Converts a `D2Result` to a gRPC `D2ResultProto` response envelope and back, so the in-memory -> wire -> in-memory
round trip keeps every field. A service handler returns its `D2Result` unchanged, the framework wraps it in the
envelope, and the consumer rebuilds an equal `D2Result` on the other side, so the caller needs no try-catch for
business failures.

## Install

```bash
dotnet add package DcsvIo.D2.Result.Grpc
```

## Core API

```csharp
// Server side: wrap
var response = new FindThingsResponse
{
    Result = result.ToProto(),
};
response.Data.AddRange(things);
return response;

// Consumer side: re-materialize
var res = await client.FindThings(req).HandleAsync(r => r.Result, r => r.Data);
if (res.Category == ErrorCategory.NotFound) { /* typed, no registry lookup */ }
res.StatusCode == HttpStatusCode.Conflict; // exact HTTP status, not a lossy gRPC bucket
```

## Key properties

- **Full fidelity**: every field - `Success`, `StatusCode` (exact integer), `ErrorCode`,
  `Category`, `Messages` (key + params), `InputErrors`, `TraceId`, `Data` - round-trips
  equal.
- **Typed `ErrorCategory`**: factories populate it at generation time. `ErrorCategory.NotFound`
  is available directly on the rebuilt result, with no runtime registry lookup on the hot path.
- **`TKMessageProto`**: structured message carrier (`key` + `map<string,string> params`).
  It keeps every parameter: a `TKMessage` with substitution bindings survives the round trip
  unchanged.
- **Transport faults**: `RpcException` -> `ServiceUnavailable`, except `Unauthenticated` ->
  `Unauthorized`, `PermissionDenied` -> `Forbidden` and `Cancelled` -> <!-- british-spelling:exempt-line -->
  `Canceled`; any other `Exception` -> `UnhandledException`. `Unauthorized` and `Forbidden` are not
  transient-retryable, so a retry pipeline does not repeat a rejected credential. User-facing
  messages stay the factory TK constants and never contain raw transport strings.
- **PII-safe logging**: `[LoggerMessage]` signatures do NOT accept `Exception`. The code logs transport
  detail through `SanitizedExceptionRender.TypeName` + `FirstFrame` + `StatusCode` and
  never logs `ex.Message` (which can embed AMQP URIs, JWT contents, configured secrets).
- **`IsTransientGrpcException`**: classifies `DeadlineExceeded`, `ResourceExhausted`,
  `Aborted`, `Internal`, `Unavailable` as transient for retry callers.

## Wire mechanism boundary

Two mechanisms coexist on every gRPC call. Each has its own role, and they must not be mixed up:

| Mechanism                                                                    | What it carries                                                                                                                                                                                                        | gRPC status on the wire                                                                                                      | Who reads it                                                                                                    |
| ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| **`D2ResultProto` response envelope** (this lib)                             | Business results - both success and failure. A handler returning `NotFound()` or `ValidationFailed()` produces a normal gRPC `OK` response; the failure detail rides inside the `D2ResultProto result` envelope field. | gRPC `OK` (status 0) + response body                                                                                         | `HandleAsync` / `ToD2Result<T>` - the caller receives a `D2Result` and needs no try-catch for business failures |
| **`RpcException` + trailers** (host-supplied auth/transport binding)         | Transport/auth rejections - JWT validation failure, JWKS unavailable, scope insufficient. These are transport-layer faults, not handler outcomes.                                                                      | Non-`OK` gRPC status (`Unauthenticated` (16) or `Unavailable` (14)) + trailers (`d2_error_code` / `d2_messages` / `traceId`) | Auth middleware on the server side; retry/circuit-breaker on the client side                                    |

**Boundary rule**: a `401` from the JWT interceptor is `RpcException(Unauthenticated)` - never a
`D2ResultProto{ success=false, status_code=401 }`. A `404` from a handler is
`D2ResultProto{ success=false, status_code=404 }` - never an `RpcException`. The paths are separate and must
stay separate.

## Proto contract

The shared `d2_result.proto` contract (`contracts/protos/d2/common/v2/`, package
`d2.common.v2`) is the single source of truth. The generated
`D2ResultProto` / `TKMessageProto` / `InputErrorProto` types live in namespace
`D2.Services.Protos.Common.V2`. `D2ResultReflection` is the generated reflection class of `d2_result.proto`; its
`Descriptor` property returns a `Google.Protobuf.Reflection.FileDescriptor`.

## Dependencies

- `DcsvIo.D2.Result` - result-core (`D2Result`, `InputError`, `TKMessage` surface)
- `DcsvIo.D2.ErrorCodes.Category` - `ErrorCategory`, `ErrorCategoryWire`
- `DcsvIo.D2.Utilities` - `Falsey()` null-guard
- `DcsvIo.D2.I18n.Abstractions` - `TKMessage` ctor (`InternalsVisibleTo`)
- `Google.Protobuf` - proto runtime
- `Grpc.Net.Client` - `AsyncUnaryCall<T>`, `RpcException`
