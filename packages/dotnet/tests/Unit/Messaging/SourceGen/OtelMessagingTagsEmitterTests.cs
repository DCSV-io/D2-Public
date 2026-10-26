// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.OtelMessagingTags.SourceGen;
using Xunit;

/// <summary>
/// Tests of the OtelMessagingTags emitter that need no I/O. They run the
/// emitter on a valid spec and on specs with deliberate drift, and assert both
/// the emitted source and the diagnostics.
/// </summary>
public sealed class OtelMessagingTagsEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllTags()
    {
        var spec = MakeSpec(
            new OtelMessagingTagEntry("MESSAGING_SYSTEM", "messaging.system", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string MESSAGING_SYSTEM = \"messaging.system\";");
        result.GeneratedSource.Should()
            .Contain("public static class MessagingActivityTags");
        result.GeneratedSource.Should()
            .Contain("public static IReadOnlyList<string> AllTags => sr_allTags;");
    }

    [Fact]
    public void Emit_MessagingOperationType_PinsTheCanonicalAttributeName()
    {
        // Regression pin: the OTel semantic-convention attribute is
        // "messaging.operation.type", not the non-standard
        // "messaging.operation". Taking the attribute name from the spec
        // structurally prevents drift from the standard.
        var spec = MakeSpec(
            new OtelMessagingTagEntry(
                "MESSAGING_OPERATION_TYPE", "messaging.operation.type", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain(
                "public const string MESSAGING_OPERATION_TYPE = \"messaging.operation.type\";");
        result.GeneratedSource.Should()
            .NotContain("\"messaging.operation\"");
    }

    // ---------------------------------------------------------------
    // Failure paths - deliberate drift cases: a duplicate constant name, a
    // duplicate value and an invalid constant name.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateConstName_EmitsDuplicateConstNameDiagnostic()
    {
        // Drift case: a duplicate constant name.
        var spec = MakeSpec(
            new OtelMessagingTagEntry("TAG", "a", "doc"),
            new OtelMessagingTagEntry("TAG", "b", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstName);
    }

    [Fact]
    public void Emit_DuplicateValue_EmitsDuplicateValueDiagnostic()
    {
        // Drift case: a duplicate value.
        var spec = MakeSpec(
            new OtelMessagingTagEntry("X", "a", "doc"),
            new OtelMessagingTagEntry("Y", "a", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateValue);
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        // Drift case: an invalid constant name.
        var spec = MakeSpec(new OtelMessagingTagEntry("lowerCase", "a", "doc"));

        var result = OtelMessagingTagsEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    private static OtelMessagingTagsSpec MakeSpec(params OtelMessagingTagEntry[] entries) =>
        new(entries.ToImmutableArray());
}
