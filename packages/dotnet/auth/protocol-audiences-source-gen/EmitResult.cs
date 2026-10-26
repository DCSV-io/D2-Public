// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of <see cref="ProtocolAudiencesEmitter.Emit"/>. Pure data: the
/// generated source and the emit-side diagnostics, which become
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances. The generated
/// source is named <c>WellKnownAudiences.g.cs</c>.
/// </summary>
/// <param name="GeneratedSource">
/// The generated C# source for the <c>WellKnownAudiences</c> static partial class.
/// </param>
/// <param name="Diagnostics">Diagnostics emitted during validation and emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
