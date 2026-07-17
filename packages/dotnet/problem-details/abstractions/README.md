<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ProblemDetails.Abstractions

Single home for the RFC 7807 ProblemDetails wire-format catalog consumed by every .NET emit path. Declares one static class — `DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys` — carrying the `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension-key constants, `TITLE_*` per-status title constants, and the `TitleFor(HttpStatusCode)` switch helper. The class is codegen-emitted from the problem-details contract spec by `DcsvIo.D2.ProblemDetails.SourceGen`.

Zero runtime dependencies. Consumers pull the constants via a package reference and get the full catalog at compile time. No transitive infrastructure surface.

## Install

```bash
dotnet add package DcsvIo.D2.ProblemDetails.Abstractions
```

## Consumers

| Consumer                                                                                                            | Use                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Host-supplied JWT / auth middleware — path A `ToProblemDetails`                                                     | Builds RFC 7807 body from auth `D2Result` failures; populates `Type`/`Title`/`Status`/`Instance` + 6 extension keys from spec constants.                                                                                                                                   |
| `DcsvIo.D2.AspNetCore` — path B `D2ProblemDetailsCustomizer.Apply`                                                  | Reads originating `D2Result` from `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]`; sets identical body fields from same spec constants. Plus unconditional `traceId` + `correlationId` extensions for diagnostic correlation even when no D2Result is stashed. |
| Host auth middleware write path                                                                                     | Sets `Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE` (the spec-driven `application/problem+json` per RFC 7807 §6.1).                                                                                                                                            |

## Cross-language parity

The same spec drives the TypeScript package `@dcsv-io/d2-problem-details-abstractions`. Wire values for the URI prefix, MIME type, extension keys, and per-status titles are byte-equal across .NET and TS by construction — cross-language drift is structurally impossible.

## Dependencies

- `DcsvIo.D2.ProblemDetails.SourceGen` (build-time analyzer)

No runtime dependencies — pure constants.

## Reference

- RFC 7807 — Problem Details for HTTP APIs: https://datatracker.ietf.org/doc/html/rfc7807
