<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Analyzers

Roslyn analyzer companion for `DcsvIo.D2.Handler` / `DcsvIo.D2.Handler.Abstractions`. It enforces
handler scope binding as defense-in-depth, so an operation contract cannot be satisfied by a second
implementer that declares no scope requirement.

## Install

```bash
dotnet add package DcsvIo.D2.Handler.Analyzers
```

The package reference wires the analyzer into the compilation on its own. It carries no runtime assembly, so
nothing in it reaches the built output.

## Public API surface

### `HandlerScopeAnalyzer`

This package publishes one analyzer, a `DiagnosticAnalyzer` registered on `SymbolKind.NamedType`.
It fires on a class that implements an interface named `I{Op}Handler` which itself derives from
`DcsvIo.D2.Handler.Abstractions.IHandler<TInput, TOutput>`. An interface of that shape that does not
derive from the platform contract - a consumer's own `IRequestHandler` or `ICommandHandler` - is not
an operation contract and raises nothing. The type declares the diagnostic ids as public constants
`D2HND001`, `D2HND002` and `D2HND003`, so a host that suppresses one or changes its severity can refer to it from
code rather than by string.

## Diagnostics

| Id       | Severity | Contract                                                                                                               |
| -------- | -------- | ---------------------------------------------------------------------------------------------------------------------- |
| D2HND001 | Error    | A class implementing `I{Op}Handler` must be named `{Op}Handler`                                                        |
| D2HND002 | Error    | That class must be declared `partial`                                                                                  |
| D2HND003 | Error    | That class must declare `protected sealed override HandlerOptions DefaultOptions` or carry `[D2GeneratedHandlerScope]` |

The carve-out applies to the assembly name, matched by exact suffix: `.Tests`, `.UnitTests` or
`.IntegrationTests`, case-insensitive. The type's own name never exempts it, so a product
handler named `TestConnectionHandler` or `SampleImportHandler` is held to every rule above.

## Configuration

None. The analyzer reads no `.editorconfig` key of its own and has no options. A host that needs a
diagnostic at a different severity sets it the standard way, by id, in its own `.editorconfig`.

## Dependencies

| Package dependency              | Why                                                                                                             |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `Microsoft.CodeAnalysis.CSharp` | The `DiagnosticAnalyzer` base type, the symbol model, and the C# syntax nodes the partial-modifier check walks. |

The analyzer resolves `DcsvIo.D2.Handler.Abstractions.IHandler<TInput, TOutput>` from the compilation
under analysis rather than referencing it, so this package takes no dependency on the handler
libraries themselves.

## Usage

Add the analyzer to a project that declares handlers, then declare each handler as the diagnostics
above require:

```csharp
[D2GeneratedHandlerScope(D2HandlerScopeMode.Any)]
public sealed partial class ArchiveDocumentHandler : IArchiveDocumentHandler
{
    protected sealed override HandlerOptions DefaultOptions => new()
    {
        ScopeRequirement = new ScopeRequirement(HandlerScopeMatch.Any, RequiredScopes),
    };
}
```

## Telemetry

None. An analyzer runs inside the compiler and emits diagnostics, not meters or spans.

## Edge cases / gotchas

- The platform contract must be resolvable. When the compilation under analysis does not
  reference `DcsvIo.D2.Handler.Abstractions`, no `I{Op}Handler` can derive from the contract and the
  analyzer reports nothing at all.
- A type has one contract. A class implementing several `I{Op}Handler` interfaces is reported
  against the first one found, so a rename fixes one diagnostic set rather than several duplicates.
- Generated code is analyzed. The analyzer opts into generated-code analysis and reports on it,
  because the generated `Scope` partial is where the binding is expected to be declared.
- A `sealed` modifier on the property is optional on a sealed class. A sealed handler class makes
  a member-level `sealed` modifier redundant, so an `override` alone satisfies D2HND003 there.
- The analyzer does not replace the authoring-time check. The TypeSpec `D2TSP004` rule still requires
  an operation to declare either a scope requirement or an infrastructure exemption; this analyzer checks
  that the C# implementer binds what was declared.
- Hand-written handlers bind the same way. A handler with no generated `Scope` partial declares
  the sealed `DefaultOptions` in its own `Scope.cs` file under the same D2HND003 contract.
