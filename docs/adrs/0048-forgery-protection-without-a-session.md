<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0048: Forgery protection without a session

- **Status**: Accepted
- **Date**: 2026-10-03

## Context

`UseD2ForgeryProtection` protects cookie-authenticated mutations. It runs no check on a safe method (GET, HEAD,
OPTIONS), on a request presenting a non-empty `Authorization` header, or on an endpoint declaring
`D2ForgeryPostures.EXEMPT`. Otherwise it reads the endpoint's declared posture:

- `D2ForgeryPostures.METADATA` runs the fetch-metadata half, which checks only where the request came from.
- `D2ForgeryPostures.REQUIRED`, and an endpoint declaring nothing, run the fetch-metadata half and the token half,
  which checks a signed token bound to the caller's session.

The token half needs a session. When the host's `ID2ForgeryTokenVerifier` returns `NoSession`, a middleware that
passed the request through before the fetch-metadata half ran, on the reasoning that a request with no session has
nothing to protect, would make the strictest posture weaker than the middle one in that state. A session-less
cross-site POST would be refused on a `METADATA` endpoint and admitted on a `REQUIRED` or undeclared one. Sign-in,
sign-up, password-reset requests and every other session-less mutation would be open to cross-site submission unless
each declared `METADATA`. That is login forgery: the attack signs the victim in as the attacker rather than using the
victim's authority. The class remarks and the README describe an undeclared route as closed.

## Decision

**A non-safe cookie-borne request to a `REQUIRED` or undeclared endpoint must pass the fetch-metadata half whether or not
a session resolved. Only the token half is skipped when no session resolved**, because there is no session to bind a
token to.

The fetch-metadata half runs before the no-session arm and takes the same disposition on every posture that requires it:

- `Sec-Fetch-Site: same-origin` or `none` passes; `same-site` and `cross-site` are refused with `METADATA_CROSS_SITE`;
  any other value is refused with `METADATA_UNKNOWN_VALUE`.
- With `Sec-Fetch-Site` absent, an `Origin` naming the request's own scheme, host and port passes, a different one
  is refused with `ORIGIN_MISMATCH`, and an absent `Origin` is refused with `HEADERS_ABSENT`.

A request that passes that half with no session reaches the endpoint, whose own authorization decides what it gets.
The skip is counted on `ForgeryProtectionTelemetry.SR_TokenHalfSkipped` and logged at Debug with the route, so a host
that places the stage ahead of its session resolution, and therefore reads `NoSession` on every request, is visible in
metrics and logs.
A refusal on that arm re-issues no token cookie, because no session resolved for one to be bound to.

## Consequences

- A cross-site mutation with no session is refused on every endpoint that does not declare `EXEMPT`, so a
  session-less form is closed without a per-route declaration.
- A caller that reaches the stage with no session, no `Authorization` header and neither fetch-metadata header is
  refused. Non-browser clients typically send neither header. A host that serves such traffic has
  those callers present `Authorization`, declares `EXEMPT` on their endpoints, or keeps that traffic out of the stage.

## Alternatives considered

**Pass a session-less request with no check.** Rejected: its premise, that nothing needs
protecting without an ambient credential, is correct for the token half and incorrect for the fetch-metadata half,
which needs no session.

**Require every session-less mutation to declare `METADATA`.** Rejected because protection would depend on each
author remembering the declaration, and an undeclared route is meant to be closed rather than open.

**Admit a session-less request that carries neither fetch-metadata header.** Not taken. A `REQUIRED` or undeclared
endpoint would again admit a request a `METADATA` endpoint refuses, so the strictest posture would not be the
strictest.

## References

- `packages/dotnet/aspnetcore/Forgery/ForgeryProtectionMiddleware.cs` - the classification, and the fetch-metadata half
  running ahead of the no-session arm.
- `packages/dotnet/aspnetcore/Forgery/D2ForgeryRefusalReasons.cs` - the refusal reasons.
- `packages/dotnet/aspnetcore/Forgery/ForgeryProtectionTelemetry.cs` - the token-half skip counter.
- `packages/dotnet/aspnetcore/README.md` - the `AddD2ForgeryProtection` section.
- `packages/dotnet/tests/Integration/AspNetCore/ForgeryProtectionMiddlewareTests.cs` - one case per posture and
  header state with no session.
