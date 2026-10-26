<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0036: A role has an authority order in addition to its capability sets

- **Status**: Accepted
- **Date**: 2026-10-03

## Context

`Role` in `packages/dotnet/auth/abstractions/Role.cs` has the members `Auditor`, `Agent`, `Officer` and `Owner`.
The `grantedTo` field of each scope in `contracts/auth-scopes/scopes.spec.json` declares what a role may do, per
pair of organization type and role, and a token issuer reads the generated `Scopes.GrantedScopes` lookup at mint
time.

Two questions are asked about roles. The first is capability: an `Auditor` reads more than an `Agent` and writes
nothing, so neither is below the other, and capability is not ordered.

The second question is authority: may this member invite, remove or change the role of that member? Every consumer
that needs the answer compares two roles with an order it derives locally, and writes that comparison again at each
call site that needs it, unless the type that defines the roles declares the order.

## Decision

**Capability and authority are separate questions, and the type states the answer to each.**

- **Capability - what a role may do - is not ordered.** It is declared per organization type and role, in
  each scope's `grantedTo`. The authority order does not change it.
- **Authority over another member - who may act on whom - is a total order.** `Auditor` ranks below `Agent`,
  below `Officer`, below `Owner`. The order is declared once, in the static class `RoleAuthority`
  (`packages/dotnet/auth/abstractions/RoleAuthority.cs`).

`RoleAuthority` has these members:

- `RankOf(Role)` returns 0 for `Auditor`, 1 for `Agent`, 2 for `Officer` and 3 for `Owner`, and throws
  `ArgumentOutOfRangeException` for a value outside the enumeration rather than inventing a rank.
- `TryRankOf(Role, out int)` returns the same ranks without throwing, and false for a value outside the
  enumeration.
- `Outranks(Role actor, Role target)` is true when the actor's rank is strictly greater, so a role does not outrank
  itself. A value outside the enumeration on either side returns false, because an authority decision denies
  rather than throws.

The remarks on `Role` state both facts, and say that reading the authority order as a capability order would
grant an auditor a write.

There is no TypeScript counterpart. Every decision this order determines is taken where a membership write happens,
and the TypeScript `Role` catalog is a generated name list that no consumer compares. If a TypeScript consumer
needs to rank two roles, the order is to be emitted from the same specification as the catalog rather than written
a second time by hand.

## Consequences

- `RoleAuthority` is additive: no member, name or value of `Role` changes.
- A consumer that compares two roles calls `Outranks` or `RankOf` instead of keeping its own order. A rule that
  differs from the strict order, such as letting an `Owner` grant `Owner`, is stated by the consumer as an
  exception on top of `Outranks`, where a reader can see it.
- The integer values of `Role` are declaration order and decide neither question. `RoleTests` pins them as
  declaration order, and `RoleAuthorityTests` pins the ranks, so a reordering of the enumeration cannot change an
  authority result without failing a test.
- A role parsed from text can fall outside the enumeration: `Enum.TryParse` accepts a numeric string. `Outranks`
  denies such a value on either side, and `TryRankOf` lets a caller test for it without catching an exception.
- Using the authority order to decide what a role may do is a misuse the type's documentation identifies. Capability
  checks keep reading the scopes a token contains.

## Alternatives considered

- **State that roles form no hierarchy and leave each consumer to compare roles.** The statement is correct about the
  capability question and leaves the authority question to every call site, each with its own order. The statement can
  also be read as covering both questions, so a consumer cannot tell which one it applies to.
- **State that roles form a hierarchy.** It would replace one wrong statement with another: an `Auditor`
  still has read scopes an `Agent` lacks, so a single hierarchy is false for capability.
- **Compare the enumeration's integer values.** The values happen to follow the same order, but they are a
  declaration detail; the wire and the database carry the role as a lowercase string. Tying authority to the values
  makes reordering or inserting a member an unseen authority change.
- **A TypeScript `RoleAuthority`.** No TypeScript consumer compares roles. A hand-written second copy would have no
  caller, and could drift from the .NET order with no test to notice.

## References

- `packages/dotnet/auth/abstractions/Role.cs` - the enumeration and its two-question remarks.
- `packages/dotnet/auth/abstractions/RoleAuthority.cs` - the authority order.
- `packages/dotnet/auth/abstractions/README.md` - the `RoleAuthority` section and its usage example.
- `packages/dotnet/tests/Unit/Auth/RoleAuthorityTests.cs` and `packages/dotnet/tests/Unit/Auth/RoleTests.cs`.
- `contracts/auth-scopes/scopes.spec.json` and `contracts/auth-scopes/schema.json` - `grantedTo`, where capability is
  declared.
