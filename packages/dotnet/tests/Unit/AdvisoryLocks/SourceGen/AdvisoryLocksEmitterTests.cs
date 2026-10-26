// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for <see cref="AdvisoryLocksEmitter"/>:
/// emitted class shape, per-database grouping, filter, duplicate detection,
/// invalid constName detection, and the <c>SnakeToPascal</c> helper.
/// The tests use fixture namespaces and no product assembly names.
/// </summary>
public sealed class AdvisoryLocksEmitterTests
{
    private const string _FIXTURE_NS = "Fixture.Module.A.Infra";

    // =========================================================================
    // Happy-path shape
    // =========================================================================

    [Fact]
    public void Emitter_FixtureNamespace_EmitsAdvisoryLocksClassShape()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"namespace {_FIXTURE_NS};");
        result.GeneratedSource.Should().Contain("public static class AdvisoryLocks");
        result.GeneratedSource.Should().Contain("public static class D2FixtureAlpha");
        result.GeneratedSource.Should().Contain("public const long REINDEX = 1001L;");
    }

    [Fact]
    public void Emitter_EmptyFilter_EmitsNoDatabaseNests()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(
            spec,
            _FIXTURE_NS,
            databaseFilter: []);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class AdvisoryLocks");
        result.GeneratedSource.Should().NotContain("D2FixtureAlpha");
        result.GeneratedSource.Should().NotContain("REINDEX");
    }

    [Fact]
    public void Emitter_SnakeToPascal_AndXmlDocEscape_OnFixtureEntries()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry(
                "LOCK_A",
                "d2-fixture_alpha",
                1L,
                "Uses <tag> & ampersand > ok"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.GeneratedSource.Should().Contain("public static class D2FixtureAlpha");
        result.GeneratedSource.Should()
            .Contain("Uses &lt;tag&gt; &amp; ampersand &gt; ok");
    }

    [Fact]
    public void Emitter_FullCatalogUniquenessThenFilter_DropsForeignDatabaseNests()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("ALPHA", "d2-fixture-alpha", 1L, "a"),
            new AdvisoryLockEntry("BETA", "d2-fixture-beta", 2L, "b"));

        var result = AdvisoryLocksEmitter.Emit(
            spec,
            _FIXTURE_NS,
            databaseFilter: ["d2-fixture-alpha"]);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("ALPHA");
        result.GeneratedSource.Should().Contain("D2FixtureAlpha");
        result.GeneratedSource.Should().NotContain("BETA");
        result.GeneratedSource.Should().NotContain("D2FixtureBeta");
    }

    [Fact]
    public void Emitter_SameInputs_ProducesIdenticalSource()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "doc"));

        var a = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null).GeneratedSource;
        var b = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null).GeneratedSource;

        a.Should().Be(b);
    }

    [Fact]
    public void Emitter_DuplicateKeys_SurfacesViaEmitDiagnosticsPath()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_A", "db", 42L, "doc"),
            new AdvisoryLockEntry("LOCK_B", "db", 42L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateKeyInDatabase);
    }

    [Fact]
    public void Emitter_FilterOrdinal_DoesNotMatchWrongCasingDatabaseId()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(
            spec,
            _FIXTURE_NS,
            databaseFilter: ["D2-Fixture-Alpha"]);

        result.GeneratedSource.Should().NotContain("REINDEX");
        result.GeneratedSource.Should().NotContain("D2FixtureAlpha");
    }

    [Fact]
    public void Emit_TwoEntriesSameDatabase_EmitsOneNestedClassWithBothConstants()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "mig"),
            new AdvisoryLockEntry("CLEANUP", "d2-fixture-alpha", 2002L, "rot"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const long REINDEX = 1001L;");
        result.GeneratedSource.Should().Contain("public const long CLEANUP = 2002L;");
        result.GeneratedSource.Split("public static class D2FixtureAlpha").Length.Should().Be(2);
    }

    [Fact]
    public void Emit_TwoDatabasesWithSameKeyValue_EmitsBothDatabasesNoError()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "db_one", 9999L, "doc"),
            new AdvisoryLockEntry("REINDEX", "db_two", 9999L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class DbOne");
        result.GeneratedSource.Should().Contain("public static class DbTwo");
    }

    [Fact]
    public void Emit_DuplicateConstNameSameDatabase_EmitsDuplicateConstNameDiagnostic()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_A", "db", 1L, "doc"),
            new AdvisoryLockEntry("LOCK_A", "db", 2L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstNameInDatabase);
    }

    [Fact]
    public void Emit_DuplicateConstNameDifferentDatabases_NoDiagnostic()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("REINDEX", "db_one", 1L, "doc"),
            new AdvisoryLockEntry("REINDEX", "db_two", 2L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Emit_LowerCaseConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("lowercase", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Emit_ConstNameStartsWithDigit_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("1LOCK", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Emit_AnySpec_GeneratedSourceContainsAutoGeneratedBanner()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("X", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        result.GeneratedSource.Should().Contain("<auto-generated>");
        result.GeneratedSource.Should().Contain("Manual edits will be lost on rebuild.");
    }

    [Theory]
    [InlineData("d2-fixture-db", "D2FixtureDb")]
    [InlineData("my_long_db_name", "MyLongDbName")]
    [InlineData("single", "Single")]
    [InlineData("a_b_c", "ABC")]
    [InlineData("", "")]
    public void SnakeToPascal_VariousInputs_ProducesExpectedOutput(
        string input, string expected)
    {
        AdvisoryLocksEmitter.SnakeToPascal(input).Should().Be(expected);
    }

    [Fact]
    public void Emit_TwoDatabases_DatabaseClassesEmittedAlphabetically()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_B", "zz_db", 2L, "doc"),
            new AdvisoryLockEntry("LOCK_A", "aa_db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, null);

        var aaIdx = result.GeneratedSource.IndexOf(
            "public static class AaDb",
            System.StringComparison.Ordinal);
        var zzIdx = result.GeneratedSource.IndexOf(
            "public static class ZzDb",
            System.StringComparison.Ordinal);

        aaIdx.Should().BeLessThan(zzIdx, "databases must be emitted alphabetically by key");
    }

    // =========================================================================
    // Two-integer lock classes
    // =========================================================================

    [Fact]
    public void Emitter_AClassEntry_EmitsItInTheClassesNest()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class D2FixtureAlpha");
        result.GeneratedSource.Should().Contain(
            $"public static class {AdvisoryLocksEmitter.CLASSES_NEST_NAME}");
        result.GeneratedSource.Should().Contain("public const int ACCOUNT = 700700;");
    }

    [Fact]
    public void Emitter_ClassesNestName_IsClasses()
    {
        AdvisoryLocksEmitter.CLASSES_NEST_NAME.Should().Be("Classes");
    }

    [Fact]
    public void Emitter_ADatabaseWithLocksAndClasses_EmitsBothUnderOneNest()
    {
        var spec = new AdvisoryLocksSpec(
            [new AdvisoryLockEntry("REINDEX", "d2-fixture-alpha", 1001L, "doc")],
            [new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc")]);

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const long REINDEX = 1001L;");
        result.GeneratedSource.Should().Contain("public const int ACCOUNT = 700700;");
        result.GeneratedSource.Split("public static class D2FixtureAlpha").Length
            .Should().Be(2, "one database contributes exactly one nest");
    }

    [Fact]
    public void Emitter_TwoClassesWithOneValueInOneDatabase_IsRefused()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc"),
            new AdvisoryLockClassEntry("OTHER", "d2-fixture-alpha", 700700, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.DuplicateClassInDatabase);
        result.GeneratedSource.Should().NotContain("OTHER");
    }

    [Fact]
    public void Emitter_OneClassValueInTwoDatabases_IsLegal()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc"),
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-beta", 700700, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().BeEmpty("class keyspaces are per-database");
        result.GeneratedSource.Should().Contain("public static class D2FixtureAlpha");
        result.GeneratedSource.Should().Contain("public static class D2FixtureBeta");
    }

    [Fact]
    public void Emitter_TwoClassesWithOneNameInOneDatabase_IsRefused()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc"),
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700701, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.DuplicateConstNameInDatabase);
    }

    [Fact]
    public void Emitter_AClassWithABadName_IsRefused()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("lowerCase", "d2-fixture-alpha", 700700, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, databaseFilter: null);

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.InvalidConstName);
        result.GeneratedSource.Should().NotContain("lowerCase");
    }

    [Fact]
    public void Emitter_ADatabaseFilter_DropsTheClassesOfEveryOtherDatabase()
    {
        var spec = MakeClassSpec(
            new AdvisoryLockClassEntry("ACCOUNT", "d2-fixture-alpha", 700700, "doc"),
            new AdvisoryLockClassEntry("OTHER", "d2-fixture-beta", 700701, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec, _FIXTURE_NS, ["d2-fixture-alpha"]);

        result.GeneratedSource.Should().Contain("ACCOUNT");
        result.GeneratedSource.Should().NotContain("OTHER");
    }

    private static AdvisoryLocksSpec MakeSpec(params AdvisoryLockEntry[] entries) =>
        new(entries.ToImmutableArray(), ImmutableArray<AdvisoryLockClassEntry>.Empty);

    private static AdvisoryLocksSpec MakeClassSpec(params AdvisoryLockClassEntry[] classes) =>
        new(ImmutableArray<AdvisoryLockEntry>.Empty, classes.ToImmutableArray());
}
