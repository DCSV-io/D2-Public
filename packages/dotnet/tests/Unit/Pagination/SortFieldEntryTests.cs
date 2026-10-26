// -----------------------------------------------------------------------
// <copyright file="SortFieldEntryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Pagination.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// The keyset predicate a registered sort field applies - as the real Npgsql
/// translator renders it, and as it selects rows.
/// </summary>
/// <remarks>
/// <para>
/// The model never opens a connection. The rendering tests observe the command
/// text a live query would send. A value inlined as an expression-tree constant
/// appears in that text, and a value passed as a query parameter does not.
/// </para>
/// <para>
/// The ascending tests run the predicate over an in-memory sequence instead,
/// because what they pin is which rows the boundary keeps rather than how it
/// is written out.
/// </para>
/// </remarks>
public sealed class SortFieldEntryTests
{
    private static readonly Guid sr_lowerId = new("00000000-0000-0000-0000-000000000001");

    private static readonly Guid sr_boundaryId = new("00000000-0000-0000-0000-000000000002");

    private static readonly Guid sr_higherId = new("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void ApplyCursor_ComparablePrimary_ParameterizesTheDecodedKeyRatherThanInliningIt()
    {
        // The decoded primary key is the boundary row's own sort value - an
        // electronic mail address on the surfaces this exists for - and the
        // cursor carrying it is unauthenticated, so it is also caller-chosen
        // text. Inlined into the command text it reaches the provider's command
        // logger and its exception detail with no sensitive-data switch
        // involved.
        var (declarations, command) = RenderCursorPage(
            "ada.lovelace@example.com",
            Guid.CreateVersion7());

        command.Should().NotContain("ada.lovelace");
        command.Should().Contain("@");
        declarations.Should().Contain("ada.lovelace@example.com");
    }

    [Fact]
    public void ApplyCursor_ComparablePrimary_ParameterizesTheTieBreak()
    {
        var tieBreak = Guid.CreateVersion7();

        var (declarations, command) = RenderCursorPage("ada@example.com", tieBreak);

        command.Should().NotContain(tieBreak.ToString());
        declarations.Should().Contain(tieBreak.ToString());
    }

    [Fact]
    public void ApplyCursor_TwoDifferentBoundaryRows_RenderTheSameCommandText()
    {
        // One statement per shape rather than one per boundary value, which is
        // what keeps a caller from driving statement-cache occupancy and plan
        // churn by paging.
        var (_, first) = RenderCursorPage("ada@example.com", Guid.CreateVersion7());
        var (_, second) = RenderCursorPage("grace@example.com", Guid.CreateVersion7());

        second.Should().Be(first);
    }

    [Fact]
    public void ApplyCursor_OrderedPrimary_ParameterizesTheTieBreakRatherThanInliningIt()
    {
        // The relevance entry is the other factory, and it is the one every
        // relevance cursor page is composed from. Its tie-break is the same
        // caller-chosen Guid arriving in the same unauthenticated cursor.
        var tieBreak = Guid.CreateVersion7();

        var (declarations, command) = RenderRelevanceCursorPage("0.375", tieBreak);

        command.Should().NotContain(tieBreak.ToString());
        declarations.Should().Contain(tieBreak.ToString());
    }

    [Fact]
    public void ApplyCursor_OrderedPrimary_ParameterizesTheScoreBoundary()
    {
        var (declarations, command) = RenderRelevanceCursorPage("0.375", Guid.CreateVersion7());

        command.Should().NotContain("0.375");
        declarations.Should().Contain("0.375");
    }

    [Fact]
    public void ApplyCursor_OrderedPrimaryTwoDifferentBoundaryRows_RenderTheSameCommandText()
    {
        var (_, first) = RenderRelevanceCursorPage("0.375", Guid.CreateVersion7());
        var (_, second) = RenderRelevanceCursorPage("0.25", Guid.CreateVersion7());

        second.Should().Be(first);
    }

    [Fact]
    public void ApplyCursor_OrderedPrimaryAscending_KeepsOnlyTheRowsPastTheBoundary()
    {
        // Relevance is locked descending, so nothing in the product takes this
        // arm yet. A keyset boundary wrong in this direction drops or repeats
        // rows with no other symptom, so the rows it keeps are pinned.
        var applied = BuildCatalog()
            .BuildRelevanceEntry()
            .ApplyCursor(ScoredProbeRows(), "0.5", sr_boundaryId, SortDirection.Asc);

        applied.Success.Should().BeTrue();
        applied.Data!.AsEnumerable().Select(s => (s.Score, s.Row.Id))
            .Should()
            .BeEquivalentTo(new[] { (0.5d, sr_higherId), (0.75d, sr_lowerId) });
    }

    [Fact]
    public void ApplyOrder_OrderedPrimaryAscending_SortsByPrimaryThenTieBreakUpward()
    {
        var ordered = BuildCatalog()
            .BuildRelevanceEntry()
            .ApplyOrder(ScoredProbeRows(), SortDirection.Asc);

        ordered.AsEnumerable().Select(s => (s.Score, s.Row.Id))
            .Should()
            .Equal(
                (0.25d, sr_higherId),
                (0.5d, sr_lowerId),
                (0.5d, sr_higherId),
                (0.75d, sr_lowerId));
    }

    [Fact]
    public void ApplyCursor_UndecodableKey_CursorKeyUndecodable()
    {
        var db = new TrigramProbeDbFixture();
        var entry = EmailEntry();

        var applied = entry.ApplyCursor(db.Rows, null!, Guid.CreateVersion7(), SortDirection.Asc);

        applied.ErrorCode.Should()
            .Be(D2.Pagination.Errors.PaginationErrorCodes.LIB_PAGINATION_CURSOR_KEY_UNDECODABLE);
    }

    private static SortFieldEntry<TrigramProbeRowFixture> EmailEntry()
    {
        var catalog = new SortCatalog<TrigramProbeRowFixture>().Text(r => r.Email, r => r.Id);

        catalog.TryGet("email", out var entry).Should().BeTrue();
        return entry;
    }

    private static SearchCatalog<TrigramProbeRowFixture> BuildCatalog() =>
        new SearchCatalogBuilder<TrigramProbeRowFixture>()
            .Blob(r => r.SearchBlob)
            .RankField(r => r.Email, 2d)
            .TieBreak(r => r.Id)
            .Build();

    /// <summary>
    /// Scored rows straddling one boundary, arranged in neither the ascending
    /// nor the descending order, so an ordering assertion over them cannot
    /// pass on the arrangement they arrived in.
    /// </summary>
    /// <returns>The scored rows.</returns>
    private static IQueryable<Scored<TrigramProbeRowFixture>> ScoredProbeRows() =>
        new[]
        {
            ScoredRow(0.75d, sr_lowerId),
            ScoredRow(0.5d, sr_lowerId),
            ScoredRow(0.25d, sr_higherId),
            ScoredRow(0.5d, sr_higherId),
        }.AsQueryable();

    private static Scored<TrigramProbeRowFixture> ScoredRow(double score, Guid id) =>
        new() { Score = score, Row = new TrigramProbeRowFixture { Id = id } };

    private static (string Declarations, string Command) RenderCursorPage(
        string primaryKey,
        Guid tieBreak)
    {
        var db = new TrigramProbeDbFixture();
        var applied = EmailEntry().ApplyCursor(db.Rows, primaryKey, tieBreak, SortDirection.Asc);

        applied.Success.Should().BeTrue();
        return Split(applied.Data!.ToQueryString());
    }

    /// <summary>
    /// Renders one relevance cursor page the way a search page composes it: the
    /// rows projected to their score, then the relevance entry's keyset
    /// predicate over that projection.
    /// </summary>
    /// <param name="scoreWire">The boundary row's score in its cursor wire form.</param>
    /// <param name="tieBreak">The boundary row's tie-break.</param>
    /// <returns>The declarations and the statement.</returns>
    private static (string Declarations, string Command) RenderRelevanceCursorPage(
        string scoreWire,
        Guid tieBreak)
    {
        var db = new TrigramProbeDbFixture();
        var catalog = BuildCatalog();
        var query = SearchQuery.Create("lovelace", new SearchPolicy()).Data!;
        var scored = db.Rows.Select(
            catalog.Project(new TrigramSearchDialect<TrigramProbeRowFixture>(), query));
        var applied = catalog.BuildRelevanceEntry()
            .ApplyCursor(scored, scoreWire, tieBreak, SortDirection.Desc);

        applied.Success.Should().BeTrue();
        return Split(applied.Data!.ToQueryString());
    }

    /// <summary>
    /// Splits a rendered query into the parameter declarations the provider
    /// writes above the statement and the statement itself.
    /// </summary>
    /// <param name="rendered">The rendered query.</param>
    /// <returns>The declarations and the statement.</returns>
    private static (string Declarations, string Command) Split(string rendered)
    {
        var lines = rendered.Split('\n');
        var start = 0;

        while (start < lines.Length
            && (lines[start].StartsWith("--", StringComparison.Ordinal)
                || lines[start].Trim().Length == 0))
        {
            start++;
        }

        return (
            string.Join("\n", lines[..start]),
            string.Join("\n", lines[start..]));
    }
}
