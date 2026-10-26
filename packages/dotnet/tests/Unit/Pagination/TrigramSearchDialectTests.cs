// -----------------------------------------------------------------------
// <copyright file="TrigramSearchDialectTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Linq.Expressions;
using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Pagination.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// The PostgreSQL trigram dialect: the escaping a pattern dialect must perform, and
/// the SQL the provider renders from the expressions it builds.
/// </summary>
/// <remarks>
/// The rendering half runs the real Npgsql translator over a model that never
/// opens a connection, so it observes what a live query would send without
/// needing a database. Execution against a live database is
/// <c>TrigramSearchIntegrationTests</c>.
/// </remarks>
public sealed class TrigramSearchDialectTests
{
    private static readonly SearchPolicy sr_searchPolicy = new();

    [Fact]
    public void EscapeCharacter_PinsItsValue()
    {
        TrigramSearchDialect<TrigramProbeRowFixture>.ESCAPE_CHARACTER.Should().Be("\\");
    }

    [Theory]
    [InlineData("ada", "ada")]
    [InlineData("100%", "100\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("%_%", "\\%\\_\\%")]
    public void EscapePattern_EscapesTheMetacharactersAndTheEscapeCharacter(
        string literal,
        string expected)
    {
        TrigramSearchDialect<TrigramProbeRowFixture>.EscapePattern(literal).Should().Be(expected);
    }

    [Theory]
    [InlineData("ada.lovelace@example.com")]
    [InlineData("a-b c/d:e")]
    [InlineData("[bracket]{brace}")]
    public void EscapePattern_EscapesNothingElse(string literal)
    {
        TrigramSearchDialect<TrigramProbeRowFixture>.EscapePattern(literal).Should().Be(literal);
    }

    [Fact]
    public void EscapePattern_EmptyText_IsUnchanged()
    {
        TrigramSearchDialect<TrigramProbeRowFixture>.EscapePattern(string.Empty)
            .Should().Be(string.Empty);
    }

    [Fact]
    public void EscapePattern_NullText_Throws()
    {
        var act = () => TrigramSearchDialect<TrigramProbeRowFixture>.EscapePattern(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Filter_RendersACaseInsensitivePatternMatchWithAnEscapeClause()
    {
        var (_, command) = Render(Filtered("ada"));

        command.Should().Contain("ILIKE");
        command.Should().Contain("ESCAPE");
    }

    [Fact]
    public void Filter_ParameterizesTheCallersSearchTextRatherThanInliningIt()
    {
        // The caller's search text on the surfaces this capability is built for
        // is a person's name or electronic mail address. Inlined into the
        // command text it is logged by the provider's command logger and
        // carried in provider exception detail, with no sensitive-data logging
        // switch involved - and every distinct term becomes a distinct
        // statement, a distinct plan and a distinct compiled-query cache entry.
        var (declarations, command) = Render(Filtered("lovelace"));

        command.Should().NotContain("lovelace");
        command.Should().Contain("@");
        declarations.Should().Contain("lovelace");
    }

    [Fact]
    public void Filter_EscapesTheMetacharactersIntoTheParameterValue()
    {
        // The escaping still happens; it happens in the value the parameter
        // holds rather than in the statement.
        var (declarations, command) = Render(Filtered("100%"));

        declarations.Should().Contain("%100\\%%");
        command.Should().NotContain("100");
    }

    [Fact]
    public void Filter_TwoDifferentTerms_RenderTheSameCommandText()
    {
        // One statement serves every term, so a caller cannot drive statement-cache
        // occupancy and plan churn by varying a query string.
        var (_, first) = Render(Filtered("ada"));
        var (_, second) = Render(Filtered("katherine johnson"));

        second.Should().Be(first);
    }

    [Fact]
    public void Filter_AsksTheDatabaseToFoldNeitherSide()
    {
        // Case-insensitivity is a property of the operator, so the statement
        // wraps neither the stored column nor the pattern in a folding call.
        var (_, command) = Render(Filtered("Ada"));

        command.Should().NotContain("lower(");
        command.Should().NotContain("upper(");
    }

    [Fact]
    public void Filter_KeepsTheBlobLambdaParameter()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        Expression<Func<TrigramProbeRowFixture, string>> blob = r => r.SearchBlob;
        var filter = dialect.Filter(blob, query);

        filter.Parameters.Should().ContainSingle();
        filter.Parameters[0].Should().BeSameAs(blob.Parameters[0]);
    }

    [Fact]
    public void Filter_NullArguments_Throw()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var nullBlob = () => dialect.Filter(null!, query);
        var nullQuery = () => dialect.Filter(r => r.SearchBlob, null!);

        nullBlob.Should().Throw<ArgumentNullException>();
        nullQuery.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Score_RendersOneSimilarityCallPerRankField()
    {
        var (_, command) = Render(Scored("ada"));

        command.Should().Contain("similarity(");
        CountOccurrences(command, "similarity(").Should().Be(2);
    }

    [Fact]
    public void Score_ParameterizesTheCallersSearchTextRatherThanInliningIt()
    {
        var (declarations, command) = Render(Scored("lovelace"));

        command.Should().NotContain("lovelace");
        declarations.Should().Contain("lovelace");
    }

    [Fact]
    public void Score_TwoDifferentTerms_RenderTheSameCommandText()
    {
        var (_, first) = Render(Scored("ada"));
        var (_, second) = Render(Scored("katherine johnson"));

        second.Should().Be(first);
    }

    [Fact]
    public void Score_SumsEveryRankFieldWeightedBySimilarity()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var score = dialect.Score(
            [
                new SearchRankField<TrigramProbeRowFixture>(r => r.Email, 2d),
                new SearchRankField<TrigramProbeRowFixture>(r => r.Username, 1d),
            ],
            query);

        score.ToString().Should().Contain("TrigramsSimilarity");

        // Each weight read back off the similarity term it multiplies, rather
        // than looked for anywhere in the rendering: a weight that ends up on
        // the wrong field still renders both digits, so an assertion that only
        // sees the digits passes with the two swapped.
        new WeightedTermReaderFixture().Read(score.Body).Should().Equal(
            [("Email", 2d), ("Username", 1d)]);
    }

    [Fact]
    public void Score_WeightsFollowTheFieldTheyWereDeclaredOn()
    {
        // The same fields, Email and Username, with the weights exchanged.
        // The reader must report the exchange, so the assertion above fails
        // when it does not.
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var score = dialect.Score(
            [
                new SearchRankField<TrigramProbeRowFixture>(r => r.Email, 1d),
                new SearchRankField<TrigramProbeRowFixture>(r => r.Username, 2d),
            ],
            query);

        new WeightedTermReaderFixture().Read(score.Body).Should().Equal(
            [("Email", 1d), ("Username", 2d)]);
    }

    [Fact]
    public void Score_RebasesEveryRankFieldOntoOneParameter()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var score = dialect.Score(
            [
                new SearchRankField<TrigramProbeRowFixture>(r => r.Email, 2d),
                new SearchRankField<TrigramProbeRowFixture>(x => x.Username, 1d),
            ],
            query);

        score.Parameters.Should().ContainSingle();
        new ParameterCounterFixture().Count(score.Body).Should().Be(1);
    }

    [Fact]
    public void Score_NoRankFields_IsZero()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var score = dialect.Score([], query).Compile();

        score(new TrigramProbeRowFixture()).Should().Be(0d);
    }

    [Fact]
    public void Score_NullArguments_Throw()
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create("ada", sr_searchPolicy).Data!;

        var nullFields = () => dialect.Score(null!, query);
        var nullQuery = () => dialect.Score([], null!);

        nullFields.Should().Throw<ArgumentNullException>();
        nullQuery.Should().Throw<ArgumentNullException>();
    }

    private static IQueryable<TrigramProbeRowFixture> Filtered(string term)
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create(term, sr_searchPolicy).Data!;
        var db = new TrigramProbeDbFixture();

        return db.Rows.Where(dialect.Filter(r => r.SearchBlob, query));
    }

    private static IQueryable<double> Scored(string term)
    {
        var dialect = new TrigramSearchDialect<TrigramProbeRowFixture>();
        var query = SearchQuery.Create(term, sr_searchPolicy).Data!;
        var db = new TrigramProbeDbFixture();

        return db.Rows.Select(
            dialect.Score(
                [
                    new SearchRankField<TrigramProbeRowFixture>(r => r.Email, 2d),
                    new SearchRankField<TrigramProbeRowFixture>(r => r.Username, 1d),
                ],
                query));
    }

    /// <summary>
    /// Splits a rendered query into the parameter declarations the provider
    /// writes above the statement and the statement itself.
    /// </summary>
    /// <remarks>
    /// The declarations are comment lines containing each parameter's value, so
    /// they are where a parameterized value appears and the statement is where
    /// an inlined literal would. Keeping them apart lets one assertion say the
    /// value traveled as a parameter rather than as command text.
    /// </remarks>
    private static (string Declarations, string Command) Render(IQueryable query)
    {
        var lines = query.ToQueryString().Split('\n');
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

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var at = text.IndexOf(token, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// Reads back the weighted similarity terms of a score expression as the
    /// pairs they were declared as - the member each similarity call reads,
    /// with the weight that multiplies it.
    /// </summary>
    private sealed class WeightedTermReaderFixture : ExpressionVisitor
    {
        private readonly List<(string Member, double Weight)> r_terms = [];

        public IReadOnlyList<(string Member, double Weight)> Read(Expression expression)
        {
            Visit(expression);
            return r_terms;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Multiply
                && node.Left is ConstantExpression { Value: double weight }
                && node.Right is UnaryExpression { NodeType: ExpressionType.Convert } convert
                && convert.Operand is MethodCallExpression call
                && call.Arguments.Count == 3
                && call.Arguments[1] is MemberExpression member)
            {
                r_terms.Add((member.Member.Name, weight));
            }

            return base.VisitBinary(node);
        }
    }

    /// <summary>Counts the distinct parameters an expression tree references.</summary>
    private sealed class ParameterCounterFixture : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> r_seen = [];

        public int Count(Expression expression)
        {
            Visit(expression);
            return r_seen.Count;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            r_seen.Add(node);
            return base.VisitParameter(node);
        }
    }
}
