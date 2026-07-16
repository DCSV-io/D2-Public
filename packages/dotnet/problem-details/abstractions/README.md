<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.ProblemDetails.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Single home for the RFC 7807 ProblemDetails wire-format catalog consumed by every .NET emit path. Declares one static class — `DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys` — carrying the `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension-key constants, `TITLE_*` per-status title constants, and the `TitleFor(HttpStatusCode)` switch helper. The class is codegen-emitted from `contracts/problem-details/problem-details.spec.json` by [`DcsvIo.D2.ProblemDetails.SourceGen`](../source-gen/README.md) — single-target dispatch on this assembly name.

Zero runtime dependencies. Consumers pull the constants via a single `<ProjectReference>` and get the full catalog at compile time. No transitive infrastructure surface.

## Consumers

| Consumer csproj / site                                                                                              | Use                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| monorepo-private `DcsvIo.D2.Private.Auth.Http` (PackageId; not on public export) — path A `ToProblemDetails`         | Builds RFC 7807 body from auth `D2Result` failures; populates `Type`/`Title`/`Status`/`Instance` + 6 extension keys from spec constants.                                                                                                                                   |
| public `DcsvIo.D2.AspNetCore` — path B `D2ProblemDetailsCustomizer.Apply`                                           | Reads originating `D2Result` from `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]`; sets identical body fields from same spec constants. Plus unconditional `traceId` + `correlationId` extensions for diagnostic correlation even when no D2Result is stashed. |
| monorepo-private Auth.Http `JwtAuthMiddleware.WriteProblemAsync`                                                    | Sets `Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE` (the spec-driven `application/problem+json` per RFC 7807 §6.1).                                                                                                                                            |

## Cross-language parity

The same spec drives the TS-side `@dcsv-io/d2-problem-details-abstractions` catalog (re-exported from `@dcsv-io/d2-private-headers` (private monorepo composition) for compat) via monorepo-private `private/tools/ts-codegen` (not on public export). Wire values for the URI prefix, MIME type, extension keys, and per-status titles are byte-equal across .NET and TS by construction — cross-language drift is structurally impossible.

Parity test: `private/packages/typescript/contract-tests/tests/problem-details.parity.test.ts` — fixture-driven; the .NET integration test `ProblemDetailsFixtureEmitter` reflects off `D2ProblemDetailsKeys` and writes JSON fixtures the TS side reads back.

## File layout

| Path                                                                                                                                | Role                                                                                             |
| ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `DcsvIo.D2.ProblemDetails.Abstractions.csproj`                                                                                      | csproj — `net10.0`, `EmitCompilerGeneratedFiles` + analyzer ref + `AdditionalFiles` for the spec |
| `Generated/DcsvIo.D2.ProblemDetails.SourceGen/DcsvIo.D2.ProblemDetails.SourceGen.ProblemDetailsGenerator/D2ProblemDetailsKeys.g.cs` | Codegen output — committed (visible in PR diffs without local build)                             |

## Reference

- [`../source-gen/README.md`](../source-gen/README.md) — the codegen that emits this assembly's contents
- monorepo-private `DcsvIo.D2.Private.Auth.Http` (PackageId; not on public export) — path A consumer
- [`../../aspnetcore/README.md`](../../aspnetcore/README.md) — path B consumer (Customizer)
- [`contracts/problem-details/problem-details.spec.json`](../../../../contracts/problem-details/problem-details.spec.json) — the source-of-truth catalog
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
