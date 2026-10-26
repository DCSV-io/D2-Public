<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Http

D2 wire-protocol header names for the HTTP transport, as string constants. The catalog has the HTTP-only entries
(`Authorization`, `Idempotency-Key`, `X-D2-Client-Fingerprint`, `X-D2-Internal-Token`) and the cross-transport entries
(`x-d2-context`, `traceparent`, `tracestate`), which have the same wire values in every transport catalog.
`DcsvIo.D2.Headers.SourceGen` generates it from the headers contract spec and keeps the entries that pass
`applicability.Contains("http")`. The TypeScript package `@dcsv-io/d2-headers-http` has byte-equal wire values, and
shared contract tests assert that parity.

## Install

```bash
dotnet add package DcsvIo.D2.Headers.Http
```

## Public API

| Member                           | Type                                     | Purpose                                                         |
| -------------------------------- | ---------------------------------------- | --------------------------------------------------------------- |
| `HttpHeaders.AUTHORIZATION`      | `const string "Authorization"`           | RFC 6750 bearer token header                                    |
| `HttpHeaders.CLIENT_FINGERPRINT` | `const string "X-D2-Client-Fingerprint"` | Client-computed device fingerprint                              |
| `HttpHeaders.IDEMPOTENCY_KEY`    | `const string "Idempotency-Key"`         | Stripe-style request-deduplication key                          |
| `HttpHeaders.INTERNAL_TOKEN`     | `const string "X-D2-Internal-Token"`     | Internal token a host acquires at its boundary                  |
| `HttpHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`            | Base64url-of-JSON propagated context envelope (cross-transport) |
| `HttpHeaders.TRACEPARENT`        | `const string "traceparent"`             | W3C Trace Context (cross-transport)                             |
| `HttpHeaders.TRACESTATE`         | `const string "tracestate"`              | W3C tracestate (cross-transport)                                |
| `HttpHeaders.AllHttpHeaders`     | `IReadOnlyList<string>`                  | All wire values in `constName` order                            |

## When to use this catalog

Use `DcsvIo.D2.Headers.Http` from any HTTP-context consumer, such as host JWT middleware, outbound token-exchange
clients, ASP.NET CORS configuration or idempotency middleware. The catalog has both the HTTP-only entries and the
cross-transport entries that an HTTP pipeline can receive, so one `using` covers them.

## Spec contract

The headers contract spec is the only source of the entries. Every entry whose `applicability` array contains `"http"`
is in this catalog. Cross-transport entries are also in `DcsvIo.D2.Headers.Common` and in every other transport
catalog they apply to, all with the same wire values. The generator guarantees the equality, and header catalog
consistency tests verify it.

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer)

No runtime dependencies - pure constants.

Sister packages: `DcsvIo.D2.Headers.Common`, `DcsvIo.D2.Headers.Amqp`, `DcsvIo.D2.Headers.Grpc`.
