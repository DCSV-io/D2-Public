// -----------------------------------------------------------------------
// <copyright file="SearchQueryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using Xunit;

/// <summary>
/// The validated query, its normalization order and the token that binds a
/// cursor to it.
/// </summary>
public sealed class SearchQueryTests
{
    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void SearchPolicy_PinsItsDefaults()
    {
        SearchPolicy.DEFAULT_MIN_QUERY_LENGTH.Should().Be(3);
        SearchPolicy.DEFAULT_MAX_QUERY_LENGTH.Should().Be(128);

        var policy = new SearchPolicy();
        policy.MinQueryLength.Should().Be(SearchPolicy.DEFAULT_MIN_QUERY_LENGTH);
        policy.MaxQueryLength.Should().Be(SearchPolicy.DEFAULT_MAX_QUERY_LENGTH);
    }

    [Fact]
    public void SearchPolicy_AcceptsExplicitBounds()
    {
        var policy = new SearchPolicy(MinQueryLength: 1, MaxQueryLength: 8);

        policy.MinQueryLength.Should().Be(1);
        policy.MaxQueryLength.Should().Be(8);
    }

    [Theory]
    [InlineData(null)]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_NoQuery_IsOkWithNoValue(string? raw)
    {
        var result = SearchQuery.Create(raw, new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsCollapsesInnerWhitespaceAndFolds()
    {
        var result = SearchQuery.Create("  Ada \t\n Lovelace  ", new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.Normalized.Should().Be("ada lovelace");
    }

    [Fact]
    public void Create_KeepsTheLiteralText_NotAnEscapedForm()
    {
        // Escaping is the dialect's job, so the metacharacters survive
        // normalization exactly as the caller typed them.
        var result = SearchQuery.Create("100%_", new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.Normalized.Should().Be("100%_");
    }

    [Fact]
    public void Create_BelowMinimum_SearchQueryTooShort()
    {
        var result = SearchQuery.Create("ad", new SearchPolicy());

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Create_PunctuationOnlyQuery_SearchQueryTooShort()
    {
        // Long enough by character count and worth nothing to a trigram index:
        // the index is reached through runs of letters and digits, so a query
        // of punctuation leaves the database scanning every reachable row and
        // then scoring what survives. Sending it is cheap and serving it is
        // expensive.
        foreach (var raw in new[] { "...", "@@@", "---", "%%%%%%%%" })
        {
            SearchQuery.Create(raw, new SearchPolicy()).ErrorCode
                .Should()
                .Be(
                    PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT,
                    "'{0}' carries no run of letters or digits",
                    raw);
        }
    }

    [Fact]
    public void Create_LettersNeverThreeTogether_SearchQueryTooShort()
    {
        // Eight letters, and no run of three: the same scan, so the same
        // refusal. Counting letters rather than runs would let this through.
        SearchQuery.Create("a.b.c-d.e-f.g.h", new SearchPolicy()).ErrorCode
            .Should().Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT);
    }

    [Fact]
    public void Create_RunOfTheMinimumInsidePunctuation_IsAccepted()
    {
        // One run long enough is all the index needs, wherever it sits.
        var result = SearchQuery.Create("*.ada@", new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.Normalized.Should().Be("*.ada@");
    }

    [Fact]
    public void Create_DigitsCountTowardTheRun()
    {
        SearchQuery.Create("100", new SearchPolicy()).Success.Should().BeTrue();
    }

    [Fact]
    public void Create_MeasuresTheUpperBoundAfterCollapsing()
    {
        // "aaa  b" is six characters raw and five normalized, so it clears a
        // ceiling of five only because the collapse ran first.
        SearchQuery.Create("aaa  b", new SearchPolicy(MinQueryLength: 3, MaxQueryLength: 5))
            .Success.Should().BeTrue();
        SearchQuery.Create("aaa  bb", new SearchPolicy(MinQueryLength: 3, MaxQueryLength: 5))
            .ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_LONG);
    }

    [Fact]
    public void Create_AtTheMinimum_IsAccepted()
    {
        SearchQuery.Create("ada", new SearchPolicy()).Success.Should().BeTrue();
    }

    [Fact]
    public void Create_AtTheMaximum_IsAccepted()
    {
        SearchQuery.Create(new string('a', 128), new SearchPolicy())
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Create_AboveMaximum_SearchQueryTooLong()
    {
        var result = SearchQuery.Create(new string('a', 129), new SearchPolicy());

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_LONG);
    }

    [Fact]
    public void Create_NullPolicy_Throws()
    {
        var act = () => SearchQuery.Create("ada", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void QueryToken_IsIdenticalForTheSameNormalizedQuery()
    {
        var first = SearchQuery.Create("Ada  Lovelace", new SearchPolicy()).Data!;
        var second = SearchQuery.Create("  ada lovelace ", new SearchPolicy()).Data!;

        second.QueryToken.Should().Be(first.QueryToken);
    }

    [Fact]
    public void QueryToken_DiffersForADifferentQuery()
    {
        var ada = SearchQuery.Create("ada", new SearchPolicy()).Data!;
        var grace = SearchQuery.Create("grace", new SearchPolicy()).Data!;

        grace.QueryToken.Should().NotBe(ada.QueryToken);
    }

    [Fact]
    public void QueryToken_IsTheFullBase64UrlDigestOfTheNormalizedQuery()
    {
        // Recomputed independently rather than compared to a recorded literal,
        // because the property the mechanism rests on is that two hosts running
        // this transform agree - and a second host runs the transform, not a
        // fixture. The digest is taken over Normalized as it stands, with no
        // transform of its own, so the token identifies the text a dialect is
        // handed.
        var query = SearchQuery.Create("Ada  Lovelace", new SearchPolicy()).Data!;
        var expected = Convert
            .ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(query.Normalized)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        query.QueryToken.Should().Be(expected);
        query.QueryToken.Should().Be("F3-F31etEh1aqsgHakoFVKZzGC_gbPZe59n30JYflH0");
    }

    [Fact]
    public void QueryToken_CarriesNoPaddingAndNoUrlUnsafeCharacter()
    {
        var token = SearchQuery.Create("ada lovelace", new SearchPolicy()).Data!.QueryToken;

        token.Should().NotContain("=");
        token.Should().NotContain("+");
        token.Should().NotContain("/");
    }

    [Fact]
    public void QueryToken_DoesNotCarryTheQueryItself()
    {
        var query = SearchQuery.Create("lovelace", new SearchPolicy()).Data!;

        query.QueryToken.Should().NotContain("lovelace");
        query.QueryToken.Length.Should().Be(43);
    }

    [Fact]
    public void Create_AccentedText_IsFoldedAndBounded()
    {
        // Written as codepoint escapes so the fixture survives an encoding
        // round-trip byte-for-byte.
        var result = SearchQuery.Create("  \u00C9LODIE  ", new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.Normalized.Should().Be("\u00E9lodie");
    }

    [Fact]
    public void QueryToken_TwoQueriesSharingAToken_AlsoShareTheirMatchedText()
    {
        // The invariant the token exists for: it is the cursor's binding to its
        // query, so two queries may share one only by handing the dialect the
        // same text. The spellings this test relies on are the plain case pair
        // 'Istanbul', 'istanbul' and 'ISTANBUL': those are the ones that fold
        // together, so those are the ones the assertion below runs on and the
        // ones the non-vacuity guard counts. Remove them and this test passes
        // over an empty set.
        //
        // The U+0130 spelling and the i-plus-U+0307 spelling are the near miss,
        // kept because a reader expects them here. ToLowerInvariant is simple
        // per-codepoint case mapping and leaves U+0130 as it stands, so the
        // dotted-capital spelling and the combining-dot spelling fold to
        // different text, take different tokens, and the assertion skips them.
        var spellings = new[]
        {
            "Istanbul",
            "istanbul",
            "ISTANBUL",
            "\u0130stanbul",
            "i\u0307stanbul",
        };

        var queries = spellings
            .Select(s => SearchQuery.Create(s, new SearchPolicy()).Data!)
            .ToList();
        var shared = 0;

        for (var i = 0; i < queries.Count; i++)
        {
            for (var j = i + 1; j < queries.Count; j++)
            {
                if (queries[i].QueryToken != queries[j].QueryToken)
                    continue;

                shared++;
                queries[j].Normalized.Should().Be(
                    queries[i].Normalized,
                    "'{0}' and '{1}' share a token",
                    spellings[i],
                    spellings[j]);
            }
        }

        // Non-vacuous: spellings that differ only in case do share a token, so
        // the assertion above ran rather than passing over an empty set.
        shared.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QueryToken_IsStableAcrossCase()
    {
        var upper = SearchQuery.Create("ADA", new SearchPolicy()).Data!;
        var lower = SearchQuery.Create("ada", new SearchPolicy()).Data!;

        upper.QueryToken.Should().Be(lower.QueryToken);
        upper.Normalized.Should().Be("ada");
        lower.Normalized.Should().Be("ada");
    }

    [Fact]
    public async Task Create_ConcurrentCallers_EachGetItsOwnQuery()
    {
        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            var raw = i % 2 == 0 ? "  Ada  " : "GRACE";
            var query = SearchQuery.Create(raw, new SearchPolicy()).Data!;
            return (i, query.Normalized, query.QueryToken);
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (i, normalized, token) in results)
        {
            var expected = i % 2 == 0 ? "ada" : "grace";
            normalized.Should().Be(expected);
            token.Should().Be(SearchQuery.Create(expected, new SearchPolicy()).Data!.QueryToken);
        }
    }
}
