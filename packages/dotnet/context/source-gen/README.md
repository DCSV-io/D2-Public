<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Context.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contracts:** [`contracts/auth-context/`](../../../../contracts/auth-context/README.md) +
[`contracts/request-context/`](../../../../contracts/request-context/README.md)

A Roslyn incremental source generator that emits the context interfaces and the mutable concrete class from JSON
spec files. It emits different files for each consuming assembly (see [Catalog dispatch](#catalog-dispatch)).

The spec files define the shape of the auth context and the request context. To add a property, change
`contracts/auth-context/IAuthContext.spec.json` or `contracts/request-context/IRequestContext.spec.json` by one line.
The next build updates the interface, the mutable concrete class and the factory methods (`FromClaims`,
`FromJwtPayloadNoValidation`).

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`;
diagnostic IDs `D2...`).

> Cross-hop propagation carries only the spec properties marked `"propagate": true`, such as `RequestId`,
> `RequestPath`, the fingerprints and `WhoIsHashId`, which a downstream consumer cannot recompute. The generator emits
> the `PropagatedContext` record and its serializer into `DcsvIo.D2.Context.Abstractions`, as the
> [Catalog dispatch](#catalog-dispatch) table lists; they are not hand-written, and they travel in the `x-d2-context` header on AMQP, gRPC and HTTP.
> Identity (UserId, OrgId, Scopes and so on) is rebuilt at every hop from the JWT and is never propagated.

---

## Catalog dispatch

| Assembly                             | Emitted file(s)                                                                         |
| ------------------------------------ | --------------------------------------------------------------------------------------- |
| `DcsvIo.D2.AuthContext.Abstractions` | `IAuthContext.g.cs`                                                                     |
| `DcsvIo.D2.Context.Abstractions`     | `IRequestContext.g.cs` (extends `IAuthContext`)                                         |
| `DcsvIo.D2.Context.Abstractions`     | `MutableRequestContext.g.cs`                                                            |
| `DcsvIo.D2.Context.Abstractions`     | `PropagatedContext.g.cs` (cross-hop wire record)                                        |
| `DcsvIo.D2.Context.Abstractions`     | `PropagatedContextExtensions.g.cs` (`MutableRequestContext.ToPropagated()` / `Apply()`) |
| `DcsvIo.D2.Context.Abstractions`     | `PropagatedContextSerializer.g.cs` (`Encode` / `TryDecode` for transport headers)       |
| Anything else                        | nothing                                                                                 |

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                             |
| ---------- | -------- | ------------------------------------------------------------------- |
| `D2CTX001` | Error    | Spec file is malformed JSON or violates the schema                  |
| `D2CTX002` | Error    | Spec property uses a type outside the closed vocabulary             |
| `D2CTX003` | Error    | Two properties in the combined spec hierarchy declare the same name |
| `D2CTX004` | Error    | `extends` references an interface not surfaced to the generator     |
| `D2CTX005` | Warning  | Property declared `derived` with an unrecognized rule name          |
| `D2CTX006` | Error    | No `*.spec.json` found in `AdditionalFiles` for a target assembly   |

`DiagnosticIds.cs` also declares `D2CTX007`, an error for a property whose `claimForm` value the emitter does not
implement for the property's type. The emitter implements `verbatimSet` for an `IReadOnlySet<string>` property and
`spaceJoined` for a `string?` property, which `contracts/auth-context/schema.json` describes.

---

## Spec-driven `[RedactData]` placement

Setting `"redact": true` on a property in either spec marks it as PII-bearing.
The generator emits `[RedactData(Reason = RedactReason.PersonalInformation)]`
on both the interface property and the matching concrete property on `MutableRequestContext`. The Serilog
destructuring policy (`DcsvIo.D2.Logging.Destructuring.RedactDataDestructuringPolicy`) reads the attributes from the
runtime instance type at log time. The attribute on the concrete property triggers redaction. The attribute on the
interface satisfies the cross-spec parity test
(`packages/dotnet/tests/Unit/SpecsConsistency/RedactDataVsSpecRedactConsistencyTests.cs`).
Both must stay in sync. The generator places both unconditionally when the spec says `redact: true`, so they diverge
only if someone hand-edits the generated output, which carries the auto-generated banner.

The generated TypeScript catalogs committed in the `@dcsv-io/d2-*` packages emit a sibling `<TypeName>RedactPaths`
constant from the same spec field. `setupLogger({ redactPaths })` takes it as Pino's redact configuration. Both
languages read `redact` from the same spec field.

## Closed type vocabulary

These are the only types a spec property can declare (enforced via `D2CTX002`):

```
string?, bool?, int?, double?, Guid?, DateTimeOffset?,
OrgType?, Role?, ActorKind?, ImpersonationKind?,
IReadOnlyList<ActorEntry>, IReadOnlyList<string>, IReadOnlySet<string>
```

`TypeVocabulary.cs` also accepts `RequestOrigin` and `IReadOnlyList<CallPathEntry>`, which the list above omits.

A new type requires changes to the JSON Schema enum of the `*.spec.json` files, to `TypeVocabulary` and to the
per-type emit helpers of `MutableEmitter`, all made together.

## Derived rules

Only one derived rule is recognized:

| Rule         | Effect                                                                                                                             |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `actorChain` | Emitted as a computed getter that walks `ActorChain` to compute impersonation flavor / impersonator org / service-client-id / etc. |

A new rule requires a change to `TypeVocabulary.IsValidDerivedRule` and a per-property emit case in
`MutableEmitter.EmitActorChainDerivedGetter` (or a new helper for the new rule).

---

## Generated runtime helpers (provided by the request-context concrete library, not this generator)

The generated `MutableRequestContext.g.cs` calls:

- `ActorChainParser.ParseFromJson(JsonElement)` / `ActorChainParser.ParseFromJsonString(string)` - RFC 8693 actor-chain
  parsing
- `ScopeClaimParser.Parse(JsonElement)` / `ScopeClaimParser.ParseString(string)` - RFC 6749 section 3.3 parsing of a
  scope as a space-separated string or a JSON array

These hand-written helpers live in `DcsvIo.D2.Context.Abstractions`. Their parsing rules come from stable RFC text and
do not change with the context spec, so the parsers are hand-written rather than generated. Tests for the parsers check
RFC compliance.

---

## Reference

- [`contracts/auth-context/`](../../../../contracts/auth-context/) - auth-context spec + JSON Schema
- [`contracts/request-context/`](../../../../contracts/request-context/) - request-context spec + JSON Schema
- [`DcsvIo.D2.Auth.Scopes.SourceGen`](../../auth/scopes-source-gen/README.md) - sibling generator with the same
  incremental-generator and diagnostic-split pattern
- [RFC 8693 section 2.1](https://datatracker.ietf.org/doc/html/rfc8693#section-2.1) - actor chain semantics
- [RFC 6749 section 3.3](https://datatracker.ietf.org/doc/html/rfc6749#section-3.3) - `scope` claim format

---

## Validation ledger

| Emitter             | Validated against                                                                                                                                                                            | Test double stands in for                                                                      | What retires the double              |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `InterfaceEmitter`  | `InterfaceEmitterTests` drives the real emitter over spec entries and asserts the emitted source.                                                                                            | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `MutableEmitter`    | `MutableEmitterTests` and `MutableEmitterClaimFormTests` drive the real emitter, the second over each claim form the spec admits.                                                            | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `PropagatedEmitter` | `PropagatedEmitterTests` drives the real emitter and asserts the emitted propagation table.                                                                                                  | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Whole generator     | `ContextGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `ContextEmitterDogfoodTests` runs the emitters over this repository's own committed context spec. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `SpecLoader`        | `SpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                             | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics         | `DiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                           | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
