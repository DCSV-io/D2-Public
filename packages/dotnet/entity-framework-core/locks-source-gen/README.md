<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.AdvisoryLocks.SourceGen

> Parent: [`packages/dotnet/entity-framework-core/`](../README.md)

A Roslyn incremental source generator that emits the `AdvisoryLocks` static class -
the spec-driven registry of PostgreSQL session advisory lock keys and two-integer
lock classes - from a host-supplied AdditionalFiles entry named
`advisory-locks.spec.json`.

The consuming project owns the catalog file and wires it via `<AdditionalFiles>`.

Convention: spec-driven Roslyn `IIncrementalGenerator` pattern.

## Opt-in emit contract (MSBuild props)

The generator emits only when the consuming project sets both MSBuild properties below, and it reports no diagnostic
when neither is set. The properties reach the analyzer as `build_property.*` through the consumer's
`CompilerVisibleProperty` items:

| Property                       | Role                                                                        |
| ------------------------------ | --------------------------------------------------------------------------- |
| `D2AdvisoryLocksEmitNamespace` | C# namespace for the emitted `AdvisoryLocks` class                          |
| `D2AdvisoryLocksDatabases`     | Comma-separated catalog `database` ids to include (Ordinal; tokens trimmed) |

| Config shape                                                 | Behavior                                                                                      |
| ------------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| Both absent / empty / whitespace                             | No emit (no diagnostic)                                                                       |
| Exactly one non-empty                                        | `D2LCK006` and no source                                                                      |
| Databases empty after split/trim while namespace set         | `D2LCK006` and no source                                                                      |
| Both set + AdditionalFiles present + all filter tokens known | Full-catalog uniqueness, then emit the filtered nests into the declared namespace             |
| Any filter token not in catalog                              | Full-catalog uniqueness diagnostics still appear; `D2LCK007` and no source (no partial class) |
| Props set, no AdditionalFiles                                | No emit and no diagnostic (the props alone do not provide a catalog)                          |

### Sample owning-module csproj (portable names)

```xml
<PropertyGroup>
  <D2AdvisoryLocksEmitNamespace>Your.Module.Infra</D2AdvisoryLocksEmitNamespace>
  <D2AdvisoryLocksDatabases>d2-your-domain</D2AdvisoryLocksDatabases>
</PropertyGroup>

<ItemGroup>
  <ProjectReference
    Include="path/to/DcsvIo.D2.AdvisoryLocks.SourceGen.csproj"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
  <AdditionalFiles Include="path/to/your/advisory-locks.spec.json" />
</ItemGroup>
```

## What this emits

Destination: the consuming assembly that sets both props and registers the
catalog AdditionalFiles. The shared Postgres package owns only the mechanism
(`PgAdvisoryLock`, migrator); domain lock-key catalogs live with the owning module.

```csharp
// namespace Your.Module.Infra;
public static class AdvisoryLocks
{
    /// <summary>Advisory locks owned by a sample domain DB.</summary>
    public static class SampleDomain
    {
        /// <summary>Blocking startup-migration lock ...</summary>
        public const long MIGRATOR = 1001001001L;

        /// <summary>Try-lock guarding unattended rotation ticks ...</summary>
        public const long ROTATION = 2002002002L;

        /// <summary>Two-integer advisory-lock classes owned by a sample domain DB.</summary>
        public static class Classes
        {
            /// <summary>Per-row serialization on an account ...</summary>
            public const int ACCOUNT_ROW = 42;
        }
    }
}
```

Consumers reach a lock key as `AdvisoryLocks.SampleDomain.MIGRATOR`, which makes the
database affinity visible in the type system.

The catalog's `locks` array produces the `public const long` members directly under each
database nest. Its `classes` array produces the `Classes` nest beside them, one
`public const int` per entry. A database that contributes only classes still gets its
nest, and one that contributes only locks gets no `Classes` nest. PostgreSQL tags a
two-integer advisory lock differently from a single-bigint one, so a class value and a
key value never name the same lock; the second integer is supplied at runtime, usually
folded from a row identifier.

## Central catalog + uniqueness

The host supplies one catalog (`advisory-locks.spec.json`). Uniqueness
diagnostics run against the full catalog whenever an opted-in assembly builds,
before the per-assembly database filter is applied. The generator never emits a nest
for another assembly's database into this assembly.

## Why spec-drive this

PostgreSQL advisory locks share one global 64-bit keyspace per database. If two locks
share a key in the same database, one critical section is skipped without any error,
because the lock the second critical section requests is the lock the first one holds, and
nothing distinguishes the two. The generator enforces per-database key uniqueness at build
time: a collision (`D2LCK003`) fails the build, so the collision does not reach runtime.

## Per-database uniqueness rule

The uniqueness check is scoped to each database:

- Duplicate `key` within the same `database` -> `D2LCK003` (build error).
- Duplicate `constName` within the same `database` -> `D2LCK002` (build error).
- Duplicate `class` within the same `database` -> `D2LCK008` (build error).
- Duplicate class `constName` within the same `database` -> `D2LCK002` (build error).
- A `class` value outside the signed 32-bit range -> `D2LCK009` (build error).
- The same `key` value in two different `database` entries -> no diagnostic
  (different keyspaces; each database's advisory lock namespace is independent).

The `locks` and `classes` name sets are checked separately, so a class may carry the
same `constName` as a key in the same database: they are emitted into different nests and
cannot collide in C#.

## No TypeScript emitter

Advisory locks are a PostgreSQL server-side runtime primitive used only by the
.NET migrator and rotation hosted services. No TypeScript process opens an
`NpgsqlConnection` or calls `pg_advisory_lock`, so a TypeScript emitter would generate code that nothing uses.
The generator is `.NET-only` by design, and the missing TypeScript emitter is not a parity gap.

## Diagnostics

| ID         | Title                                               | Severity |
| ---------- | --------------------------------------------------- | -------- |
| `D2LCK001` | Advisory locks spec is malformed                    | Error    |
| `D2LCK002` | Duplicate constName within database                 | Error    |
| `D2LCK003` | Duplicate key within database                       | Error    |
| `D2LCK004` | constName has invalid shape                         | Error    |
| `D2LCK005` | Key value out of signed 64-bit range                | Error    |
| `D2LCK006` | Incomplete emit configuration (namespace/databases) | Error    |
| `D2LCK007` | Unknown database filter token(s); source suppressed | Error    |
| `D2LCK008` | Duplicate class within database                     | Error    |
| `D2LCK009` | Class value out of signed 32-bit range              | Error    |

## Telemetry

N/A - analyzer/source-gen package; no runtime logging or metrics surface.

## Dependencies

N/A as a published runtime dependency graph for app hosts - this package is an
analyzer (`OutputItemType=Analyzer`, `ReferenceOutputAssembly=false`). It
references Roslyn APIs only for compile-time emit.

## Validation ledger

| Emitter / surface                                           | Validated against                                                                                      | Doubles                                                                                                                                                                                | Replace-trigger                                                                                        |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `AdvisoryLocksEmitter.Emit(spec, ns, filter)`               | Public unit tests (`Unit/AdvisoryLocks/SourceGen/AdvisoryLocksEmitterTests`) - fixture namespaces only | None (pure function)                                                                                                                                                                   | -                                                                                                      |
| `AdvisoryLocksGenerator` (props + AdditionalFiles pipeline) | Public unit tests (`AdvisoryLocksGeneratorTests`)                                                      | `FixtureAnalyzerConfigOptionsProvider` + `InMemoryAdditionalTextFixture` asserting real `build_property.D2AdvisoryLocksEmitNamespace` / `build_property.D2AdvisoryLocksDatabases` keys | Real MSBuild props + AdditionalFiles on the assembly named by `D2AdvisoryLocksEmitNamespace`           |
| Host committed-file parity                                  | Owning-module host test suite (happy path + deliberate-drifts)                                         | Same Fixture options provider                                                                                                                                                          | Committed `Generated/**/AdvisoryLocks.g.cs` under the project that sets `D2AdvisoryLocksEmitNamespace` |
