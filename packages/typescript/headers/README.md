<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# headers/

> Parent: [`public/packages/typescript/`](../README.md)

The D2 wire-protocol header **constant catalogs** for TS consumers, split per transport. Codegen-emitted from the same `contracts/headers/headers.spec.json` spec that drives the .NET `DcsvIo.D2.Headers.*` catalogs, so a header that appears on multiple transports carries an identical wire value across both languages.

BFF-side glue (JWT decode, `x-d2-context` decode, ProblemDetails builder, route guards) is **private composition** under monorepo `private/packages/typescript/headers-core/` as `@dcsv-io/d2-private-headers` — not a public Built root.

## Packages

| Package                       | Description                                                                                                          |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| [`common/`](common/README.md) | Cross-transport headers (`PROPAGATED_CONTEXT`, `TRACEPARENT`, `TRACESTATE`, `AUTHORIZATION`). Mirrors `DcsvIo.D2.Headers.Common`. |
| [`http/`](http/README.md)     | HTTP-applicable headers (HTTP-only entries plus cross-transport entries inline). Mirrors `DcsvIo.D2.Headers.Http`. |
| [`amqp/`](amqp/README.md)     | AMQP-applicable headers (AMQP-only entries plus cross-transport entries inline). Mirrors `DcsvIo.D2.Headers.Amqp`. |
| [`grpc/`](grpc/README.md)     | gRPC-applicable headers. Mirrors `DcsvIo.D2.Headers.Grpc`.                                                          |