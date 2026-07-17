<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Http

D2 wire-protocol headers for the HTTP transport. Includes HTTP-only entries (`Authorization`, `Idempotency-Key`, `X-D2-Client-Fingerprint`, `X-D2-Internal-Token`) and cross-transport entries (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values. Codegen-emitted from the headers contract spec via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Contains("http")`). Mirrors the TypeScript package `@dcsv-io/d2-headers-http` at byte-equal wire values; parity is asserted by shared contract tests.

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
| `HttpHeaders.INTERNAL_TOKEN`     | `const string "X-D2-Internal-Token"`     | Boundary-acquired internal token (host hop)                    |
| `HttpHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`            | Base64url-of-JSON propagated context envelope (cross-transport) |
| `HttpHeaders.TRACEPARENT`        | `const string "traceparent"`             | W3C Trace Context (cross-transport)                             |
| `HttpHeaders.TRACESTATE`         | `const string "tracestate"`              | W3C tracestate (cross-transport)                                |
| `HttpHeaders.AllHttpHeaders`     | `IReadOnlyList<string>`                  | All wire values in `constName` order                            |

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Http` from any HTTP-context consumer — host JWT middleware, outbound token-exchange clients, ASP.NET CORS configuration, idempotency middleware. The catalog includes both the HTTP-only entries and the cross-transport entries that an HTTP pipeline can encounter; one `using` covers everything.

## Spec contract

The headers contract spec is the single source of truth. Every entry whose `applicability` array contains `"http"` lives in this catalog. Cross-transport entries also live in `DcsvIo.D2.Headers.Common` and every other transport catalog they apply to, all at identical wire values (codegen-guaranteed and verified by header catalog consistency tests).

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

Sister packages: `DcsvIo.D2.Headers.Common`, `DcsvIo.D2.Headers.Amqp`, `DcsvIo.D2.Headers.Grpc`.
