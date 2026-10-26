// -----------------------------------------------------------------------
// <copyright file="DefaultEmailValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using System.Text.RegularExpressions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Default <see cref="IEmailValidator"/> that checks structure with a regex
/// enforcing the practical limits of RFC-5321 and RFC-5322: total length 1-254,
/// a local part of 1-64 ASCII alphanumeric characters plus <c>. _ % + -</c>,
/// an at-sign, and one or more domain labels separated by dots, with no leading
/// or trailing hyphen in a label.
/// </summary>
/// <remarks>
/// <para>
/// Normalization: the input is trimmed before the regex runs, and the returned
/// value is the trimmed input lowercased. The match is case-insensitive by
/// design, through <c>RegexOptions.IgnoreCase</c>.
/// </para>
/// <para>
/// <b>ReDoS posture: anchored and bounded, with no catastrophic backtracking.</b>
/// The pattern is anchored at both ends. The total-length lookahead is bounded. Every
/// character class is bounded: local part <c>{1,64}</c>, each domain label
/// <c>{0,61}</c>. The structure is linear in input length.
/// A 50 ms match timeout and the static-constructor JIT pre-warm are
/// kept as defense-in-depth against later regex edits that
/// could silently push the pattern into catastrophic backtracking.
/// </para>
/// <para>
/// The pattern string is exposed as <see cref="EMAIL_PATTERN"/> so a parity
/// test can assert byte-identity with the TypeScript pattern.
/// </para>
/// </remarks>
public sealed class DefaultEmailValidator : IEmailValidator
{
    /// <summary>
    /// The email validation regex pattern, which is the cross-language source of
    /// truth shared with the TypeScript <c>@dcsv-io/d2-validation</c> package. Any
    /// change here must be mirrored on the TypeScript side, because the two
    /// patterns must stay byte-identical.
    /// </summary>
    // The regex literal is too long to wrap.
    public const string EMAIL_PATTERN =
        @"^(?=.{1,254}$)[A-Z0-9._%+\-]{1,64}@[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?)+$";

    private static readonly TimeSpan sr_MatchTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly Regex sr_Email = new(
        EMAIL_PATTERN,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        sr_MatchTimeout);

    static DefaultEmailValidator()
    {
        // JIT pre-warm. The timeout stays even though the pattern is linear in
        // input length, so that later edits to the pattern cannot silently remove the
        // protection.
        PreWarm(sr_Email);
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? email)
    {
        if (email.Falsey())
            return Invalid();

        var trimmed = email!.Trim();

        bool isMatch;
        try
        {
            isMatch = sr_Email.IsMatch(trimmed);
        }
        catch (RegexMatchTimeoutException)
        {
            return Invalid();
        }

        if (!isMatch)
            return Invalid();

        return D2Result<string>.Ok(trimmed.ToLowerInvariant());
    }

    /// <summary>
    /// Runs a single throwaway match that forces the compiled matcher to be emitted and
    /// jitted. The match timeout covers that emit, so on a loaded machine this first
    /// match can exceed it; the pre-warm is an optimization, and letting that exception
    /// leave the static constructor would fail the type initializer and make every later
    /// use of the validator throw for the life of the process.
    /// </summary>
    /// <param name="regex">The regex to warm.</param>
    internal static void PreWarm(Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);

        try
        {
            _ = regex.IsMatch("a@b.co");
        }
        catch (RegexMatchTimeoutException)
        {
            // Pre-warm only; the next real call incurs the emit cost instead.
        }
    }

    private static D2Result<string> Invalid()
        => D2Result<string>.ValidationFailed(
            inputErrors: [new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
}
