<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Tests

> Parent: [`packages/dotnet/`](../README.md)

The test project for every `DcsvIo.D2.*` library. It is one test csproj and not one per library, because the
foundational shared libraries are small enough that a test project for each would be more than they need.

This project contains shared library tests only. Service-host test projects are kept with their services and are not
part of this package tree.

---

## Layout

```
packages/dotnet/tests/
+- DcsvIo.D2.Tests.csproj
+- Unit/                             # in-process unit and behavior tests
|  +- AdvisoryLocks/                 # -> entity-framework-core/locks-source-gen
|  +- AspNetCore/                    # -> aspnetcore (forgery, mTLS, problem-details, security headers)
|  +- Auth/                          # -> auth/abstractions, plus SourceGen/ for scopes and audiences
|  +- AuthContext/                   # -> auth/context-abstractions
|  +- Caching/                       # -> caching/abstractions, caching/local-default, caching/distributed-redis
|  +- Contacts/                      # -> contacts/core and contacts/entity-framework-core
|  +- Context/                       # -> context/abstractions, plus SourceGen/ for context/source-gen
|  +- DataGovernance/                # -> data-governance/abstractions and its EF Core engine
|  +- Encryption/                    # -> encryption/core and its source generators
|  +- EnvFiles/                      # -> env-files
|  +- ErrorCodesCategory/            # -> error-codes/category-source-gen
|  +- ErrorCodesRegistry/            # -> error-codes/registry and registry-source-gen
|  +- Geo/                           # -> geo/abstractions, geo/default, geo/source-gen
|  +- Handler/                       # -> handler/core, handler/abstractions, handler/analyzers
|  +- HandlerRepo/                   # -> handler/repo, repo-abstractions, repo-postgres
|  +- Headers/                       # -> headers
|  +- I18n/                          # -> i18n/core, i18n/abstractions, i18n/source-gen
|  +- InProcessKeys/                 # -> encryption/in-process-keys-source-gen
|  +- Location/                      # -> location/core and location/entity-framework-core
|  +- Logging/                       # -> logging
|  +- Messaging/                     # -> messaging/abstractions, messaging/rabbitmq, messaging/source-gen
|  +- Oss/                           # -> the public-clone layout guards
|  +- Outbox/                        # -> outbox/core and outbox/entity-framework-core
|  +- Pagination/                    # -> pagination and its EF Core packages
|  +- PasswordHashing/               # -> password-hashing
|  +- ProblemDetails/                # -> problem-details/abstractions and problem-details/source-gen
|  +- RequestContext/                # -> context/abstractions (request-context entities)
|  +- RequestContextAbstractions/    # -> context/abstractions (request-context interfaces)
|  +- Resilience/                    # -> resilience
|  +- Result/                        # -> result/core, result/grpc, result/envelope-source-gen
|  +- SharedEntityFrameworkCore/     # -> entity-framework-core/core
|  +- SourceGen/                     # -> source-gen-shared
|  +- SpecsConsistency/              # -> cross-catalog parity over the contracts tree
|  +- Telemetry/                     # -> telemetry and telemetry/tags-source-gen
|  +- Time/                          # -> time
|  +- Usernames/                     # -> usernames
|  +- Utilities/                     # -> utilities and utilities/Configuration
|  +- Validation/                    # -> validation/abstractions, validation/default, validation/source-gen
|  +- WireShapes/                    # -> the JSON wire shapes crossing a process boundary
|  +- WorkloadIdentity/              # -> workload-identity
+- Integration/                      # Testcontainers-backed real-infrastructure tests
   +- AspNetCore/                    # -> aspnetcore, driven through a composed pipeline
   +- Caching/                       # -> caching/distributed-redis and caching/tiered (Redis container)
   +- Contacts/                      # -> contacts/entity-framework-core (PostgreSQL container)
   +- ContractFixtures/              # emits cross-language parity fixture JSON for the TypeScript twins
   +- DataGovernance/                # -> data-governance/entity-framework-core (PostgreSQL container)
   +- Encryption/                    # -> encryption/core against real key material
   +- Logging/                       # -> logging, through a composed Serilog pipeline
   +- Messaging/                     # -> messaging/rabbitmq (RabbitMQ container)
   +- Outbox/                        # -> outbox/entity-framework-core (PostgreSQL container)
   +- Pagination/                    # -> pagination EF Core packages (PostgreSQL container)
   +- SharedEntityFrameworkCore/     # -> entity-framework-core/core (PostgreSQL container)
   +- SharedEntityFrameworkCorePostgres/  # -> entity-framework-core/postgres (PostgreSQL container)
   +- Telemetry/                     # -> telemetry, through a composed OpenTelemetry pipeline
```

The tree mostly follows the source layout (`Unit/{LibName}/{LibSourceFile}Tests.cs` per library). `Unit/RequestContext/`
and `Unit/RequestContextAbstractions/` both cover code that is under `context/abstractions/`. These test folders keep
the request-context entities and the request-context abstractions apart so that a reviewer can find each; both
subtrees compile in the same csproj.

Where a folder groups its test doubles in a `Fixtures/` sub-folder, the types in it have a `Fixture`, `Fake`, `Stub`,
`Test` or `Sample` marker in their name. Elsewhere a fixture type is next to the test that uses it.

Integration tests use xUnit collection fixtures (`[Collection("Redis")]`, `[Collection("RabbitMq")]`), so each
container fixture starts once per test run.

---

## Stack

| Tool                                               | Version     | Purpose                                                                                                                                                                                                 |
| -------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `xunit.v3`                                         | per CPM     | Test framework. `[Fact]`, `[Theory]`, `Assert`.                                                                                                                                                         |
| `xunit.runner.visualstudio`                        | per CPM     | Test discovery for Rider / VS / `dotnet test`.                                                                                                                                                          |
| `Microsoft.NET.Test.Sdk`                           | per CPM     | MSBuild test integration.                                                                                                                                                                               |
| `AwesomeAssertions`                                | per CPM     | Fluent assertion API (`result.Should().BeOk()`). MIT-licensed fork of FluentAssertions; FluentAssertions v8 and later use a commercial license, and AwesomeAssertions continues the Apache 2.0 lineage. |
| `JetBrains.Annotations`                            | per CPM     | `[MustDisposeResource]`, `[Pure]`, etc. on test fixtures.                                                                                                                                               |
| `Testcontainers.Redis` / `Testcontainers.RabbitMq` | per CPM     | Integration test containers (real Redis and real RabbitMQ through Docker). Skipped when Docker is not reachable.                                                                                        |
| MTP (Microsoft Testing Platform)                   | SDK-bundled | Test runner, enabled by `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in the csproj. It replaces VSTest.                                                                |

Test packages are pinned in `Directory.Packages.props` (Central Package Management); this csproj references them by ID
only.

---

## Conventions

### Test naming

`MethodName_Scenario_ExpectedResult`. The method name documents the test:

```csharp
[Fact]
public void Ok_WithTraceId_PreservesTraceId()
[Fact]
public void Bind_OnFailure_DoesNotInvokeNext()
[Fact]
public void IsTransientRetryable_ExplicitlyFalseOnUnhandledException()
```

CS1591 and SA1600 (missing XML doc) are suppressed in this csproj only; non-test libraries still require XML doc on
every public member. The reason is in the csproj `<NoWarn>` comment.

### Local constants

`snake_case` for local consts in test bodies:

```csharp
const string trace_id = "trace-abc-123";
```

Non-const test locals (`var`, `out var`, and so on) must be `camelCase`.

### Adversarial coverage

Every test file should cover the following categories where they apply:

1. **Happy path** - every factory creates the expected shape
2. **Garbage input** - null/empty/whitespace, oversized, malformed
3. **Boundary values** - empty list vs single-item vs multi-item; default(T) vs explicit; off-by-one
4. **Format validation** - regex / length / type checks
5. **Cross-field deps** - interaction between two arguments / two state slots
6. **Error propagation** - `BubbleFail` chain preservation, `Bind` short-circuiting, exception -> D2Result mapping
7. **Idempotency** - repeat-call semantics on mutating ops
8. **Concurrency** - thread-stress on mutable shared state (`Parallel.ForEachAsync`, `Task.WhenAll`)

For pure value types like `D2Result`, format validation, idempotency and concurrency are degenerate. For libraries with
I/O (caching, messaging, repository) those categories apply, and tests cover them.

### Lazy-evaluation tests

Monadic and async-chain tests assert that continuations are not invoked after an upstream failure. Pattern:

```csharp
var nextInvoked = false;
var result = upstream.Bind(x =>
{
    nextInvoked = true;
    return D2Result<int>.Ok(x);
});
nextInvoked.Should().BeFalse();
```

Use this pattern in every chaining test, sync and async.

### Monadic laws

For each monad-shaped type (currently only `D2Result<T>`'s `Bind`), verify:

- **Left identity**: `M.Ok(x).Bind(f) ≡ f(x)`
- **Right identity**: `m.Bind(M.Ok) ≡ m`
- **Associativity**: `m.Bind(f).Bind(g) ≡ m.Bind(x => f(x).Bind(g))`

Exercise both the success path and the failure path.

### `[LoggerMessage]` PII contract

Every `[LoggerMessage]` partial method whose call sites in the library observe exceptions that can contain PII (broker
URIs, connection strings, OAuth tokens, raw user input) must take no `Exception` parameter.
`Unit/Messaging/Telemetry/LoggerMessageDelegateContractTests.cs` checks this by reflection. The same guard is used in
the messaging, caching/distributed-redis, caching/tiered and handler libraries. The contract test lists
the messaging delegates, and the sibling libraries follow the same rule.

---

## Running

```bash
dotnet test packages/dotnet/tests                # all tests in this project
dotnet test D2.Public.slnx                       # public solution (all public test projects)
```

Tests are discovered through xunit.v3 and MTP. Rider and the Visual Studio Test Explorer find tests automatically.
Integration tests are skipped when Docker is not reachable.

### Coverage

Coverage runs through `coverlet.console`, which needs no MSBuild hook and no package reference in this project. Install
it once and point it at the built test executable:

```bash
dotnet tool install --global coverlet.console
dotnet build packages/dotnet/tests/DcsvIo.D2.Tests.csproj
dotnet coverlet packages/dotnet/tests/bin/Debug/net10.0   --target packages/dotnet/tests/bin/Debug/net10.0/DcsvIo.D2.Tests.exe --targetargs ""   --include "[DcsvIo.D2*]*" --exclude "[*Tests]*"   --exclude-by-attribute "GeneratedCode"   --format cobertura --output .coverage/
```

Render the report with `dotnet tool install --global dotnet-reportgenerator-globaltool` and
`reportgenerator -reports:.coverage/coverage.cobertura.xml -targetdir:.coverage/html`. The report lists every file with
its uncovered lines and has a page per class with each source line marked hit or missed.

These parts of that command decide whether the coverage numbers are accurate:

- `--include "[DcsvIo.D2*]*"` selects the assemblies that coverlet rewrites, so only these assemblies are instrumented.
  A collector that also traces dependencies runs out of memory on the suites that reach libphonenumber. The
  alternative that avoids this by sampling less drops whole assemblies from the report without a message. Code that is
  missing from the report is not counted as uncovered, so those gaps look like a coverage improvement.
- `--target` is the test executable and not `dotnet test`. This project runs on Microsoft's Testing Platform, which
  builds and runs in one target, so a collector hooked into MSBuild produces no output here whatever properties it is
  passed.
- A run that stops part way leaves the assemblies instrumented. Coverlet rewrites assemblies on disk and restores them
  when it finishes. A killed run leaves the rewritten copies, and `dotnet build` does not replace them because MSBuild
  skips the copy when the destination is newer. Run `dotnet clean` and rebuild.

For a line-by-line view in the IDE, Rider's "Cover Unit Tests" gutter button produces the same data.

Run coverage before you commit the tests of a new library, to find:

- library lines, branches and methods that no test runs
- branches missed because a test covers only one side of a `??`, a ternary or a nullable check, which is the most common
  gap

> Rider reports 0% for the `_OnFailure_ShortCircuits` and `_DoesNotInvokeProjection` lambdas, and that is expected:
> each test asserts that the lambda body is never invoked. Coverage tooling cannot tell "untested" from "asserted never
> called", so 0% on those lambdas is the test passing. Read library coverage from the `DcsvIo.D2.{LibName}` row in the
> Cobertura report.

---

## When to expand this project

When you add a shared library in `packages/dotnet/{lib}/`, create `Unit/{Lib}/` here with one test file per source file
(`{SourceFile}Tests.cs`). If the library needs real infrastructure (a real database, a real broker, a real cache), add
`Integration/{Lib}/` with a Testcontainers fixture instead. Add the project reference to the library in
`DcsvIo.D2.Tests.csproj`.
