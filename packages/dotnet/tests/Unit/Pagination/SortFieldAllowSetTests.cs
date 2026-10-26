// -----------------------------------------------------------------------
// <copyright file="SortFieldAllowSetTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using Xunit;

/// <summary>
/// The direction lock: the flags enumeration, the per-field specification and
/// the allow-set that holds them.
/// </summary>
public sealed class SortFieldAllowSetTests
{
    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void AllowedDirections_PinsItsMemberValues()
    {
        ((int)AllowedDirections.None).Should().Be(0);
        ((int)AllowedDirections.Ascending).Should().Be(1);
        ((int)AllowedDirections.Descending).Should().Be(2);
        ((int)AllowedDirections.Both).Should().Be(3);
        AllowedDirections.Both.Should()
            .Be(AllowedDirections.Ascending | AllowedDirections.Descending);
    }

    [Fact]
    public void SortFieldNames_Relevance_PinsItsWireValue()
    {
        SortFieldNames.RELEVANCE.Should().Be("relevance");
    }

    [Theory]
    [InlineData(AllowedDirections.Ascending, SortDirection.Asc)]
    [InlineData(AllowedDirections.Descending, SortDirection.Desc)]
    public void SoleDirection_IsTheOnePermittedDirection(
        AllowedDirections allowed,
        SortDirection expected)
    {
        new SortFieldSpec("email", allowed).SoleDirection.Should().Be(expected);
    }

    [Fact]
    public void SoleDirection_IsNull_WhenBothArePermitted()
    {
        new SortFieldSpec("email", AllowedDirections.Both).SoleDirection.Should().BeNull();
    }

    [Fact]
    public void SortFieldSpec_ConstructedWithNone_Throws()
    {
        var act = () => new SortFieldSpec("email", AllowedDirections.None);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SortFieldSpec_WithExpressionSettingNone_Throws()
    {
        // A positional record's copy constructor copies backing fields
        // directly, so a guard written only in the primary constructor would be
        // bypassed here and reproduce the value it exists to prevent.
        var spec = new SortFieldSpec("email", AllowedDirections.Both);
        var act = () => spec with { Allowed = AllowedDirections.None };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SortFieldSpec_WithExpressionSettingAnUndeclaredFlag_Throws()
    {
        var spec = new SortFieldSpec("email", AllowedDirections.Both);
        var act = () => spec with { Allowed = (AllowedDirections)4 };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SortFieldSpec_WithExpressionKeepingAPermittedValue_Succeeds()
    {
        var spec = new SortFieldSpec("email", AllowedDirections.Both);
        var narrowed = spec with { Allowed = AllowedDirections.Ascending };

        narrowed.Field.Should().Be("email");
        narrowed.SoleDirection.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void From_KeepsEveryFieldAndItsDirections()
    {
        var set = SortFieldAllowSet.From(
            new SortFieldSpec("email", AllowedDirections.Ascending),
            new SortFieldSpec("joinedAt", AllowedDirections.Both));

        set.Fields.Should().BeEquivalentTo(["email", "joinedAt"]);
        set.TryGet("email", out var email).Should().BeTrue();
        email!.Allowed.Should().Be(AllowedDirections.Ascending);
    }

    [Fact]
    public void From_NoSpecs_ProducesAnEmptySet()
    {
        SortFieldAllowSet.From().Fields.Should().BeEmpty();
    }

    [Fact]
    public void From_RepeatedField_KeepsTheLastSpecification()
    {
        var set = SortFieldAllowSet.From(
            new SortFieldSpec("email", AllowedDirections.Ascending),
            new SortFieldSpec("email", AllowedDirections.Descending));

        set.Fields.Should().ContainSingle();
        set.TryGet("email", out var spec).Should().BeTrue();
        spec!.SoleDirection.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void From_NullSpec_Throws()
    {
        var act = () => SortFieldAllowSet.From(new SortFieldSpec[] { null! });
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    public void From_FalseyFieldName_Throws(string field)
    {
        var act = () => SortFieldAllowSet.From(
            new SortFieldSpec(field, AllowedDirections.Both));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BothDirections_PermitsBothOnEveryField()
    {
        var set = SortFieldAllowSet.BothDirections("email", "joinedAt");

        foreach (var field in set.Fields)
        {
            set.TryGet(field, out var spec).Should().BeTrue();
            spec!.Allowed.Should().Be(AllowedDirections.Both);
        }
    }

    [Theory]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("  ")]
    public void BothDirections_FalseyFieldName_Throws(string field)
    {
        var act = () => SortFieldAllowSet.BothDirections(field);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryGet_IsOrdinalAndCaseSensitive()
    {
        var set = SortFieldAllowSet.BothDirections("joinedAt");

        set.TryGet("joinedAt", out _).Should().BeTrue();
        set.TryGet("JOINEDAT", out _).Should().BeFalse();
        set.TryGet("joinedat", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    public void TryGet_FalseyField_IsNotFound(string? field)
    {
        SortFieldAllowSet.BothDirections("joinedAt")
            .TryGet(field, out var spec).Should().BeFalse();

        spec.Should().BeNull();
    }

    [Fact]
    public void IntersectWith_KeepsOnlyRegisteredFields_AndTheirSpecifications()
    {
        var set = SortFieldAllowSet.From(
            new SortFieldSpec("email", AllowedDirections.Ascending),
            new SortFieldSpec("joinedAt", AllowedDirections.Both));

        var narrowed = set.IntersectWith(["joinedAt", "notInTheSet"]);

        narrowed.Fields.Should().BeEquivalentTo(["joinedAt"]);
        narrowed.TryGet("joinedAt", out var spec).Should().BeTrue();
        spec!.Allowed.Should().Be(AllowedDirections.Both);
    }

    [Fact]
    public void IntersectWith_LeavesTheSourceSetUntouched()
    {
        var set = SortFieldAllowSet.BothDirections("email", "joinedAt");

        set.IntersectWith(["joinedAt"]).Fields.Should().ContainSingle();
        set.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void IntersectWith_FalseyCatalogField_IsIgnored()
    {
        var set = SortFieldAllowSet.BothDirections("joinedAt");

        set.IntersectWith(["  ", "joinedAt"]).Fields.Should().BeEquivalentTo(["joinedAt"]);
    }

    [Fact]
    public void IntersectWith_NullCatalogFields_Throws()
    {
        var act = () => SortFieldAllowSet.BothDirections("joinedAt").IntersectWith(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void With_ReadmitsAFieldAndKeepsItsSpecification()
    {
        var declared = SortFieldAllowSet.From(
            new SortFieldSpec(SortFieldNames.RELEVANCE, AllowedDirections.Descending));
        var narrowed = SortFieldAllowSet.BothDirections("joinedAt");

        declared.TryGet(SortFieldNames.RELEVANCE, out var relevance).Should().BeTrue();
        var widened = narrowed.With(relevance!);

        widened.Fields.Should().BeEquivalentTo(["joinedAt", SortFieldNames.RELEVANCE]);
        widened.TryGet(SortFieldNames.RELEVANCE, out var carried).Should().BeTrue();
        carried!.SoleDirection.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void With_LeavesTheSourceSetUntouched()
    {
        var set = SortFieldAllowSet.BothDirections("joinedAt");

        set.With(new SortFieldSpec("email", AllowedDirections.Both))
            .Fields.Should().HaveCount(2);

        set.Fields.Should().ContainSingle();
    }

    [Fact]
    public void With_ReplacesAnExistingEntryForTheSameField()
    {
        var set = SortFieldAllowSet.BothDirections("email");
        var replaced = set.With(new SortFieldSpec("email", AllowedDirections.Ascending));

        replaced.Fields.Should().ContainSingle();
        replaced.TryGet("email", out var spec).Should().BeTrue();
        spec!.SoleDirection.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void With_NullSpec_Throws()
    {
        var act = () => SortFieldAllowSet.BothDirections("email").With(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void With_FalseyFieldName_Throws()
    {
        var act = () => SortFieldAllowSet.BothDirections("email")
            .With(new SortFieldSpec(" ", AllowedDirections.Both));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task AllowSet_ServesConcurrentReadersWithoutInterference()
    {
        var set = SortFieldAllowSet.From(
            new SortFieldSpec("email", AllowedDirections.Ascending),
            new SortFieldSpec("joinedAt", AllowedDirections.Both));

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            // Half the callers narrow, half widen. The shared instance must be
            // unchanged by either, because a process-wide allow-set is what
            // generated code holds.
            _ = i % 2 == 0
                ? set.IntersectWith(["email"])
                : set.With(new SortFieldSpec("displayName", AllowedDirections.Both));

            set.TryGet("email", out var spec).Should().BeTrue();
            return spec!.Allowed;
        }));

        var seen = await Task.WhenAll(tasks);

        seen.Should().AllBeEquivalentTo(AllowedDirections.Ascending);
        set.Fields.Should().BeEquivalentTo(["email", "joinedAt"]);
    }
}
