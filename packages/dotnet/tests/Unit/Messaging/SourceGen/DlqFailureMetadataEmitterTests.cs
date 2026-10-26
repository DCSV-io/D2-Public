// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.DlqMetadata.SourceGen;
using Xunit;

/// <summary>
/// Tests of the DlqFailureMetadata emitter that need no I/O, for the fields
/// half and the causes half. The drift tests below run both halves.
/// </summary>
public sealed class DlqFailureMetadataEmitterTests
{
    [Fact]
    public void EmitFields_ValidSingleEntry_EmitsConstant()
    {
        var spec = MakeSpec(
            new[] { new DlqFieldEntry("CAUSE", "cause", "doc") },
            System.Array.Empty<DlqCauseEntry>());

        var result = DlqFieldsEmitter.EmitFieldsCatalog(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string CAUSE = \"cause\";");
        result.GeneratedSource.Should().Contain("public static class DlqFailureMetadataFields");
    }

    [Fact]
    public void EmitCauses_ValidSingleEntry_EmitsConstant()
    {
        var spec = MakeSpec(
            System.Array.Empty<DlqFieldEntry>(),
            new[] { new DlqCauseEntry("HANDLER_EXCEPTION", "HANDLER_EXCEPTION", "doc") });

        var result = DlqCausesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string HANDLER_EXCEPTION = \"HANDLER_EXCEPTION\";");
        result.GeneratedSource.Should().Contain("public static class DlqFailureCauses");
    }

    // ---------------------------------------------------------------
    // Failure paths - deliberate drift cases: a duplicate field constant
    // name, a duplicate cause and an invalid constant name.
    // ---------------------------------------------------------------

    [Fact]
    public void EmitFields_DuplicateFieldConstName_EmitsDuplicateFieldConstNameDiagnostic()
    {
        var spec = MakeSpec(
            new[]
            {
                new DlqFieldEntry("X", "a", "doc"),
                new DlqFieldEntry("X", "b", "doc"),
            },
            System.Array.Empty<DlqCauseEntry>());

        var result = DlqFieldsEmitter.EmitFieldsCatalog(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateFieldConstName);
    }

    [Fact]
    public void EmitCauses_DuplicateCauseConstName_EmitsDuplicateCauseDiagnostic()
    {
        var spec = MakeSpec(
            System.Array.Empty<DlqFieldEntry>(),
            new[]
            {
                new DlqCauseEntry("X", "a", "doc"),
                new DlqCauseEntry("X", "b", "doc"),
            });

        var result = DlqCausesEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCause);
    }

    [Fact]
    public void EmitFields_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(
            new[] { new DlqFieldEntry("lowerCase", "a", "doc") },
            System.Array.Empty<DlqCauseEntry>());

        var result = DlqFieldsEmitter.EmitFieldsCatalog(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    private static DlqFailureMetadataSpec MakeSpec(
        System.Collections.Generic.IEnumerable<DlqFieldEntry> fields,
        System.Collections.Generic.IEnumerable<DlqCauseEntry> causes) =>
        new(fields.ToImmutableArray(), causes.ToImmutableArray());
}
