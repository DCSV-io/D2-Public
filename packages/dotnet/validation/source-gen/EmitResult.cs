// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of a per-source emit run. Pure data - the diagnostics are emit-side
/// records and not <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances,
/// and the generated source is plain text that
/// <c>Microsoft.CodeAnalysis.SourceProductionContext.AddSource</c> accepts.
/// </summary>
/// <param name="HintName">
/// The Roslyn hint name (a stable, unique-per-source identifier ending in
/// <c>.g.cs</c>) under which the generated source is added.
/// </param>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during validation and emission.</param>
internal sealed record EmitResult(
    string HintName,
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
