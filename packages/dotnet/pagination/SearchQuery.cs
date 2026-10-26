// -----------------------------------------------------------------------
// <copyright file="SearchQuery.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// A validated, normalized query and the token that binds a cursor to it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Normalized"/> is the raw search text canonicalized. It is literal
/// text and is not interpreted as pattern syntax: it can contain pattern
/// metacharacters such as <c>%</c> and <c>_</c>, and this library does not escape
/// them. The bounds are measured against it, and a search dialect's filter and
/// score expressions receive it. Escaping is a property of a pattern-matching
/// dialect, so a pattern-matching dialect escapes those characters when it
/// builds its expression.
/// </para>
/// <para>
/// Canonicalizing includes the case fold, and the fold runs here rather than
/// on the token because the two must be one string. <see cref="QueryToken"/>
/// is the cursor's binding to its query, and it is a digest of
/// <see cref="Normalized"/> itself: a token can therefore only be shared by
/// two queries that give the dialect identical text. Folding the token alone
/// breaks that - two spellings that fold together produce one token and two
/// different patterns, so a cursor minted on one is accepted on the other and
/// a relevance page applies a score boundary taken from a different scoring
/// run.
/// </para>
/// <para>
/// The fold is not what makes matching case-insensitive. That is a property
/// of the dialect's operator, and the stored column it matches against is
/// stored unfolded. The fold is <c>ToLowerInvariant</c>, which is simple
/// per-codepoint case mapping rather than the full case mapping Unicode
/// defines: it is length-preserving, and it leaves U+0130 unchanged. A
/// database's own lowering is not that rule, so the two need not
/// agree outside the ASCII range. The fold has one narrow cost:
/// where the rules disagree about a character, the folded query can miss a row
/// the operator would have matched from the raw search text. That cost
/// follows from the token identifying exactly the text it binds a cursor to.
/// </para>
/// <para>
/// A forged token gives the forger only a page of their own query, so it is not
/// a secret and nothing treats it as one.
/// </para>
/// </remarks>
/// <param name="Normalized">
/// The trimmed, space-collapsed, case-folded query - the text a dialect
/// matches and scores on.
/// </param>
/// <param name="QueryToken">
/// The binding token: SHA-256 over the UTF-8 bytes of
/// <paramref name="Normalized"/>, rendered base64url without padding.
/// </param>
public sealed record SearchQuery(string Normalized, string QueryToken)
{
    /// <summary>
    /// Normalizes and bound-checks a raw caller query.
    /// </summary>
    /// <remarks>
    /// Normalization is trim, then collapse runs of inner whitespace to one
    /// space, then case-fold under the invariant culture. The upper bound is
    /// the character count of that result. The lower bound is the longest run
    /// of letters and digits in it, because that run decides whether a trigram
    /// index can serve the query at all.
    /// <para>
    /// The whitespace the collapse covers is <c>char.IsWhiteSpace</c>'s set. A
    /// provider leaf that stores a pre-collapsed column has to collapse that
    /// same set, or a row containing a character that only one side collapses
    /// cannot be found by its own text.
    /// </para>
    /// </remarks>
    /// <param name="raw">Raw search text; null, empty or whitespace means no query.</param>
    /// <param name="policy">The surface's query-length bounds.</param>
    /// <returns>
    /// The validated query; a successful result containing <see langword="null"/>
    /// when there was no query;
    /// <c>LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT</c> when the normalized text
    /// contains no run of letters and digits as long as the policy's minimum;
    /// <c>LIB_PAGINATION_SEARCH_QUERY_TOO_LONG</c> when it is longer than the
    /// policy's maximum.
    /// </returns>
    public static D2Result<SearchQuery?> Create(string? raw, SearchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (raw.Falsey())
            return D2Result<SearchQuery?>.Ok();

        var normalized = Normalize(raw!);

        if (normalized.Falsey())
            return D2Result<SearchQuery?>.Ok();

        if (LongestAlphanumericRun(normalized) < policy.MinQueryLength)
            return PaginationFailures<SearchQuery?>.SearchQueryTooShort();

        if (normalized.Length > policy.MaxQueryLength)
            return PaginationFailures<SearchQuery?>.SearchQueryTooLong();

        return D2Result<SearchQuery?>.Ok(new SearchQuery(normalized, ComputeToken(normalized)));
    }

    /// <summary>
    /// The longest unbroken run of letters and digits in the normalized query.
    /// </summary>
    /// <remarks>
    /// The minimum bound is measured on this run, because the run decides
    /// whether a trigram index can serve the query at all. A trigram index is
    /// reached through runs of letters and digits, and every other character
    /// ends a run - so a query of punctuation, however long, and a query whose
    /// letters never sit three together, both pass a character count and
    /// leave the database scanning every row the request can reach. The bound
    /// rejects those, which keeps a cheap request cheap to serve.
    /// </remarks>
    /// <param name="normalized">The normalized query text.</param>
    /// <returns>The length of the longest run.</returns>
    private static int LongestAlphanumericRun(string normalized)
    {
        var longest = 0;
        var run = 0;

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                run++;

                if (run > longest)
                    longest = run;

                continue;
            }

            run = 0;
        }

        return longest;
    }

    private static string Normalize(string raw)
    {
        var trimmed = raw.Trim();
        var sb = new StringBuilder(trimmed.Length);
        var inWhitespace = false;

        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c))
            {
                inWhitespace = true;
                continue;
            }

            if (inWhitespace && sb.Length > 0)
                sb.Append(' ');

            inWhitespace = false;
            sb.Append(c);
        }

        return sb.ToString().ToLowerInvariant();
    }

    private static string ComputeToken(string normalized)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return Convert.ToBase64String(digest)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
