// -----------------------------------------------------------------------
// <copyright file="ProjectedPageRowTranslationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.Pagination.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// What a relational provider will and will not do with a filter or an
/// ordering applied after the page row has been built. Every page extension in
/// this library composes exactly that way - it receives a query already
/// projected to the page row and then filters and orders it. Which
/// projection shapes survive that composition is something the library
/// requires, and no other test records it.
/// </summary>
/// <remarks>
/// An in-memory provider gives a wrong result for this question, because it
/// evaluates the projection in .NET and every shape then works.
/// </remarks>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class ProjectedPageRowTranslationTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private string r_connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ProjectedPageRowTranslationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public ProjectedPageRowTranslationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        r_connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(ProjectedPageRowTranslationTests));

        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);
        await ctx.Database.EnsureCreatedAsync();

        ctx.Members.Add(new TrigramMemberEntityFixture
        {
            Id = Guid.CreateVersion7(),
            ReachId = Guid.CreateVersion7(),
            DisplayName = "Ada Lovelace",
            Email = "ada@example.com",
        });

        await ctx.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(r_connectionString);

    [Fact]
    public async Task WhereOverAMemberBoundProjection_Translates()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var rows = await ctx.PageRows()
            .Where(r => r.DisplayName == "Ada Lovelace")
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task OrderByOverAMemberBoundProjection_Translates()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var rows = await ctx.PageRows()
            .OrderBy(r => r.DisplayName)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task WhereOverAConstructorProjection_IsRefusedByTheProvider()
    {
        // Pinned because it is a constraint on every caller of this library
        // and no other test states it: Entity Framework rewrites a later
        // filter into the projection it follows, and it cannot bind a member
        // read back to a constructor argument the way it binds one to a settable member. A
        // page row built through a positional record's constructor therefore
        // reaches the provider as an expression the provider refuses, and the
        // refusal arrives at the first request rather than at the build.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        // Captured rather than reached through the context, which this scope
        // owns and disposes.
        var query = ctx.Members.AsNoTracking()
            .Select(e => new TrigramMemberCtorRowFixture(e.DisplayName))
            .Where(r => r.DisplayName == "Ada Lovelace");

        var act = async () => await query.ToListAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*could not be translated*");
    }

    /// <summary>A page row whose member exists only as a constructor argument.</summary>
    /// <param name="DisplayName">A member a later filter reads.</param>
    private sealed record TrigramMemberCtorRowFixture(string DisplayName);
}
