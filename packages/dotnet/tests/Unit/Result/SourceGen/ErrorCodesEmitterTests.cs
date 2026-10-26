// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Registry;
using Xunit;
using CatalogConfig = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.CatalogConfig;
using ConstantsEmitter = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ConstantsEmitter;
using DiagnosticIds = ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.DiagnosticIds;
using ErrorCodeEntry = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodeEntry;
using ErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;
using ErrorCodesSpec = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodesSpec;

/// <summary>
/// Pure-logic tests for the generic ErrorCodes emission, driving the shared
/// <c>ConstantsEmitter</c> with the real generic catalog config + synthetic
/// specs. Includes per-VALUE pins for every entry in the shipping spec so a
/// wire-value drift surfaces at the emitter level.
/// </summary>
public sealed class ErrorCodesEmitterTests
{
    private static CatalogConfig Config => ErrorCodesGenerator.Config;

    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllCodesAndGetHttpStatus()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X_THING", 404, "X thing doc."));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const string GEN_X_THING = \"GEN_X_THING\";");
        result.GeneratedSource.Should().Contain("namespace DcsvIo.D2.Result;");
        result.GeneratedSource.Should().Contain("public static class ErrorCodes");
        result.GeneratedSource.Should().Contain(
            "public static int GetHttpStatus(string errorCode)");
        result.GeneratedSource.Should().Contain("\"GEN_X_THING\" => 404,");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlyList<string> AllCodes => sr_allCodes;");

        // Generic catalog has NO KebabCase helper (auth-only).
        result.GeneratedSource.Should().NotContain("public static string KebabCase");
    }

    [Fact]
    public void Emit_DuplicateCode_EmitsDuplicateCodeDiagnostic()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("GEN_DUPE", 400, "X"),
            new ErrorCodeEntry("GEN_DUPE", 400, "Y"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Emit_InvalidHttpStatus_EmitsInvalidHttpStatusDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 418, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Emit_InvalidCodeLowercase_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("lowercase", 400, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeEmpty_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry(string.Empty, 400, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeWhitespace_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("   ", 400, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_InvalidCodeStartingWithDigit_EmitsInvalidCodeDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("9NOPE", 400, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidCode);
    }

    [Fact]
    public void Emit_MissingDoc_EmitsMissingDocDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 400, string.Empty));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.MissingDoc);
    }

    [Fact]
    public void Emit_WhitespaceDoc_EmitsMissingDocDiagnostic()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 400, "   "));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.MissingDoc);
    }

    [Fact]
    public void Emit_PreservesSpecOrder()
    {
        var spec = MakeSpec(
            new ErrorCodeEntry("GEN_ZEBRA", 400, "Z"),
            new ErrorCodeEntry("GEN_ALPHA", 400, "A"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();

        // ZEBRA must appear before ALPHA in the emitted source: the emitter keeps
        // the spec order, and alphabetical sorting would change the diff on spec edits.
        var zebraIndex = result.GeneratedSource.IndexOf("GEN_ZEBRA", System.StringComparison.Ordinal);
        var alphaIndex = result.GeneratedSource.IndexOf("GEN_ALPHA", System.StringComparison.Ordinal);
        zebraIndex.Should().BeLessThan(alphaIndex);
    }

    [Fact]
    public void Emit_GenericCatalog_RequiresGenDomainPrefix()
    {
        // Under GEN_ owner law, an unprefixed code in the generic catalog
        // MUST fire the domain-prefix diagnostic (D2ERC001).
        var spec = MakeSpec(new ErrorCodeEntry("NOT_FOUND", 404, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public void Emit_GenericCatalog_AcceptsGenPrefixedCode()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_NOT_FOUND", 404, "X"));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("GEN_NOT_FOUND");
    }

    [Fact]
    public void Emit_RunsTwiceWithIdenticalInput_ProducesIdenticalSource()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 400, "X doc."));

        var first = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);
        var second = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    /// <summary>
    /// Per-VALUE pin for every shipping spec entry: the literal table below is
    /// the pin, and it is asserted twice - against the shipping registry, which
    /// is generated from the catalog this table mirrors, and against the
    /// source the emitter produces for the same entry.
    /// </summary>
    /// <remarks>
    /// A drifted row fails the registry assertion. Without that assertion, the
    /// synthetic spec would carry the row's own status into the emitter and back
    /// out, so a status changed in the shipping catalog would leave this table
    /// with the old value and no test would fail.
    /// </remarks>
    /// <param name="code">The wire-format error code expected on the emitted constant.</param>
    /// <param name="httpStatus">
    /// The HTTP status the shipping catalog declares and the emitted switch arm
    /// must map the code to.
    /// </param>
    [Theory]
    [InlineData("GEN_NOT_FOUND", 404)]
    [InlineData("GEN_FORBIDDEN", 403)]
    [InlineData("GEN_UNAUTHORIZED", 401)]
    [InlineData("GEN_VALIDATION_FAILED", 400)]
    [InlineData("GEN_CONFLICT", 409)]
    [InlineData("GEN_UNHANDLED_EXCEPTION", 500)]
    [InlineData("GEN_COULD_NOT_BE_SERIALIZED", 500)]
    [InlineData("GEN_COULD_NOT_BE_DESERIALIZED", 500)]
    [InlineData("GEN_SERVICE_UNAVAILABLE", 503)]
    [InlineData("GEN_SOME_FOUND", 206)]
    [InlineData("GEN_PARTIAL_SUCCESS", 503)]
    [InlineData("GEN_RATE_LIMITED", 429)]
    [InlineData("GEN_IDEMPOTENCY_IN_FLIGHT", 409)]
    [InlineData("GEN_PAYLOAD_TOO_LARGE", 413)]
    [InlineData("GEN_CANCELED", 400)]
    [InlineData("GEN_FORGERY_REFUSED", 403)]
    public void Emit_ShippingSpecEntry_EmitsConstantAndHttpStatusMapping(
        string code, int httpStatus)
    {
        ErrorCodeRegistry.All
            .Should().ContainSingle(entry => entry.Code == code)
            .Which.HttpStatus.Should().Be(httpStatus);

        var spec = MakeSpec(new ErrorCodeEntry(code, httpStatus, $"{code} doc."));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"public const string {code} = \"{code}\";");
        result.GeneratedSource.Should().Contain($"\"{code}\" => {httpStatus},");
    }

    [Fact]
    public void Emit_XmlDocSpecialChars_AreEscaped()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 400, "Has <angle> & ampersand."));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("&lt;angle&gt; &amp; ampersand.");
    }

    [Fact]
    public void Emit_ShortDoc_KeepsCompactSingleLineSummary()
    {
        var spec = MakeSpec(new ErrorCodeEntry("GEN_X", 400, "Short doc."));

        var result = ConstantsEmitter.Emit(spec, Config, ImmutableHashSet<string>.Empty);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("    /// <summary>Short doc.</summary>");
    }

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
