// -----------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;

/// <summary>
/// Extension methods for <see cref="string"/> covering boundary checks
/// (<c>Truthy</c>, <c>Falsey</c> and <c>ToNullIfEmpty</c>), display-friendly
/// cleaning (<c>CleanStr</c> and <c>CleanDisplayStr</c>), <see cref="D2Result"/>-returning
/// validation helpers for emails and phone numbers, a hash-friendly
/// <c>GetNormalizedStrForHashing</c> helper for string-array inputs, and
/// <c>NormalizeForHash</c> for single-value cross-script hash canonicalization.
/// </summary>
/// <remarks>
/// The validation helpers (<c>TryParseEmail</c>, <c>TryParsePhoneNumber</c>)
/// return <c>D2Result&lt;string&gt;</c> with TK-keyed messages, so they compose
/// with the smart-constructor pattern in domain layers. Callers chain with
/// <c>D2Result.Bind</c> or <c>BubbleFail</c> instead of try/catch.
/// </remarks>
public static partial class StringExtensions
{
    /// <param name="str">The string being checked or cleaned.</param>
    extension(string? str)
    {
        /// <summary>
        /// Returns true when the string is non-null, non-empty, and contains at
        /// least one non-whitespace character.
        /// </summary>
        public bool Truthy() => !str.Falsey();

        /// <summary>
        /// Returns true when the string is null, empty, or contains only
        /// whitespace.
        /// </summary>
        public bool Falsey() => string.IsNullOrWhiteSpace(str);

        /// <summary>
        /// Returns null when the string is null, empty or whitespace; otherwise
        /// returns the trimmed string. Use it at boundaries (DB rows, proto
        /// mapping, user input) to convert empty strings to null before they
        /// reach domain models.
        /// </summary>
        public string? ToNullIfEmpty()
        {
            if (str.Falsey())
                return null;

            return str!.Trim();
        }

        /// <summary>
        /// Trims leading and trailing whitespace and collapses each run of
        /// internal whitespace (spaces, tabs, newlines and other whitespace
        /// characters) into a single space. Returns null if the string is empty
        /// after cleaning.
        /// </summary>
        public string? CleanStr()
        {
            var trimmed = str?.Trim();

            if (trimmed.Falsey())
                return null;

            return WhitespaceRegex().Replace(trimmed!, " ");
        }

        /// <summary>
        /// Strips characters not allowed in display names (such as HTML tags,
        /// markdown syntax, brackets, quotes and backticks) and then applies
        /// <see cref="CleanStr"/>. Returns null if the string is empty after
        /// cleaning.
        /// </summary>
        /// <remarks>
        /// Allowed characters: letters from any Unicode script, digits, spaces,
        /// hyphens, apostrophes, periods, commas.
        /// </remarks>
        public string? CleanDisplayStr()
        {
            if (str.Falsey())
                return null;

            var stripped = DisplayNameInvalidRegex().Replace(str!, string.Empty);
            return stripped.CleanStr();
        }

        /// <summary>
        /// Produces the canonical hash-input form of a single string:
        /// case-fold to uppercase, NFD-decompose, then keep only Unicode
        /// Letter and Decimal-digit code points (any script) plus ASCII
        /// spaces. Inputs that differ only by diacritics, case or punctuation
        /// produce byte-identical output, so a SHA-256 of the result
        /// is a stable cross-script correlation digest.
        /// Returns <see cref="string.Empty"/> when the input is
        /// null, empty, or whitespace-only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Stage-2-only contract.</b> This method does not trim or
        /// collapse internal whitespace. It case-folds, NFD-decomposes,
        /// and keeps only Letter, Decimal-digit and ASCII-space code points.
        /// Leading, trailing, and internal spaces survive as-is. Callers
        /// that require whitespace normalization should apply a stage-1
        /// cleaner (e.g. <see cref="CleanStr"/>) before calling this
        /// method. Whitespace-only input (spaces, tabs, newlines)
        /// returns <see cref="string.Empty"/>, because the
        /// <c>Falsey()</c> guard catches it before any processing occurs.
        /// </para>
        /// <para>
        /// Punctuation, symbols, emoji, control characters, and Unicode
        /// format characters (BiDi overrides, zero-width joiners) are
        /// stripped. Multi-space runs are preserved unchanged and are not
        /// collapsed to a single space.
        /// </para>
        /// </remarks>
        public string NormalizeForHash()
        {
            if (str.Falsey())
                return string.Empty;

            // Stage 2a: case-fold. This changes nothing in caseless scripts.
            var upper = str!.ToUpperInvariant();

            // Stage 2b: NFD decompose so Latin-derived diacritics split
            // into a base character and a combining mark; the category
            // filter below then drops the combining marks.
            var nfd = upper.Normalize(NormalizationForm.FormD);

            // Stage 2c: Unicode-category filter that keeps only Letter,
            // Decimal-digit and ASCII space characters. Iterate by Rune so
            // surrogate pairs are handled correctly (char.IsLetter is
            // surrogate-unsafe).
            var sb = new StringBuilder(nfd.Length);
            foreach (var rune in nfd.EnumerateRunes())
            {
                if (rune.Value == ' ')
                {
                    sb.Append(' ');
                    continue;
                }

                if (Rune.IsLetter(rune) || Rune.IsDigit(rune))
                    sb.Append(rune.ToString());
            }

            return sb.ToString();
        }
    }

    /// <param name="email">The email address to clean and validate.</param>
    extension(string? email)
    {
        /// <summary>
        /// Trims, collapses whitespace, lowercases, and validates the basic
        /// structure of an email address. Returns a <see cref="D2Result{TData}"/>
        /// so callers can chain with the rest of the result pipeline
        /// (<c>BubbleFail</c>, <c>Bind</c>) instead of try/catch.
        /// </summary>
        /// <returns>
        /// <see cref="D2Result{TData}"/> wrapping the normalized email on
        /// success, or a validation-failure result whose messages contain
        /// <see cref="TK.Common.Validation.EMAIL_INVALID"/> when the input is
        /// null, empty or whitespace, or does not match <c>local@domain.tld</c>.
        /// </returns>
        public D2Result<string> TryParseEmail()
        {
            var cleaned = email.CleanStr()?.ToLowerInvariant();
            if (cleaned.Falsey() || !EmailRegex().IsMatch(cleaned!))
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.EMAIL_INVALID]);
            }

            return D2Result<string>.Ok(cleaned!);
        }
    }

    /// <param name="phoneNumber">The phone number to clean and validate.</param>
    extension(string? phoneNumber)
    {
        /// <summary>
        /// Strips every non-digit character and validates that the remainder is
        /// 7-15 digits long (the E.164 length range). Returns a <see cref="D2Result{TData}"/>
        /// so callers can chain with the rest of the result pipeline.
        /// </summary>
        /// <returns>
        /// <see cref="D2Result{TData}"/> wrapping the digit-only phone number
        /// on success, or a validation-failure result whose messages contain
        /// <see cref="TK.Common.Validation.PHONE_INVALID"/> when the input is
        /// null, empty or contains no digits, or its digit count falls outside
        /// 7-15.
        /// </returns>
        public D2Result<string> TryParsePhoneNumber()
        {
            if (phoneNumber.Falsey())
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.PHONE_INVALID]);
            }

            var cleaned = NonDigitsRegex().Replace(phoneNumber!, string.Empty);

            if (cleaned.Falsey() || cleaned.Length is < 7 or > 15)
            {
                return D2Result<string>.ValidationFailed(
                    messages: [TK.Common.Validation.PHONE_INVALID]);
            }

            return D2Result<string>.Ok(cleaned);
        }
    }

    /// <param name="parts">
    /// The individual values to normalize for hashing.
    /// </param>
    extension(string?[] parts)
    {
        /// <summary>
        /// Cleans and lowercases each part, then joins them with a pipe
        /// (<c>|</c>) character. Empty and null parts become empty segments,
        /// which keeps each part at its position across input arrays.
        /// </summary>
        ///
        /// <example>
        /// <c>[ " Test One ", "   ", "TEST3" ]</c> ->
        /// <c>"test one||test3"</c>.
        /// </example>
        public string GetNormalizedStrForHashing()
        {
            return string.Join(
                "|",
                parts.Select(x => x.CleanStr()?.ToLowerInvariant() ?? string.Empty));
        }
    }

    /// <summary>
    /// Matches a basic <c>local@domain.tld</c> email shape: a local part, then two
    /// or more domain labels joined by single dots. Backtracking bucket: linear
    /// with the input length, for any input length. Each quantified class
    /// excludes the character that ends it (<c>@</c> ends the local part,
    /// <c>.</c> ends each domain label), so no two quantifiers can match the same
    /// character and a failed match gives back each character at most once. The
    /// pattern has no <c>matchTimeout</c>, because linear match time needs no
    /// length bound. Callers that store the address bound its length before the
    /// match for storage, not for match time.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$", RegexOptions.None)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Matches one or more whitespace characters. A single greedy quantifier
    /// with no pattern after it means no backtracking, so no
    /// <c>matchTimeout</c> is needed.
    /// </summary>
    [GeneratedRegex(@"\s+", RegexOptions.None)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Matches characters not allowed in display names. Allowed: letters from
    /// any Unicode script, digits, spaces, hyphens, apostrophes, periods,
    /// commas. A single char-class match with no quantifier means no
    /// backtracking, so no <c>matchTimeout</c> is needed.
    /// </summary>
    [GeneratedRegex(@"[^\p{L}\p{N}\s\-'.,]", RegexOptions.None)]
    private static partial Regex DisplayNameInvalidRegex();

    /// <summary>
    /// Matches any non-digit character. A single char-class match with no
    /// quantifier means no backtracking, so no <c>matchTimeout</c> is needed.
    /// </summary>
    [GeneratedRegex(@"[^\d]", RegexOptions.None)]
    private static partial Regex NonDigitsRegex();
}
