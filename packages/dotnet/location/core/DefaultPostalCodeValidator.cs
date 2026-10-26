// -----------------------------------------------------------------------
// <copyright file="DefaultPostalCodeValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location;

using System.Text.RegularExpressions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Default <see cref="IPostalCodeValidator"/> - global-range shape check
/// only (3-10 alphanumeric characters; internal spaces and hyphens
/// allowed; alphanumeric at both ends). Country-blind by design;
/// per-country strict validation requires an override registered via
/// <c>services.Replace(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>ReDoS posture.</b> The pattern is anchored at both ends,
/// the character class is bounded, and the inner repetition is
/// bounded (<c>{1,8}</c>). Worst-case time is linear in input length, and
/// the pattern has no catastrophic backtracking. The code DELIBERATELY
/// treats it as a linear pattern with <b>defense-in-depth</b> anyway: the
/// <c>matchTimeout = 50ms</c> and the JIT pre-warm in the static
/// constructor are retained as a guard-rail against edits to the regex
/// (relaxing the class or removing an anchor could silently make the
/// pattern super-linear).
/// </para>
/// </remarks>
public sealed class DefaultPostalCodeValidator : IPostalCodeValidator
{
    private static readonly TimeSpan sr_MatchTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly Regex sr_GlobalShape = new(
        @"^[A-Z0-9](?:[A-Z0-9 \-]{1,8}[A-Z0-9])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        sr_MatchTimeout);

    static DefaultPostalCodeValidator()
    {
        // JIT pre-warm, so the first real call does not pay the regex compile.
        PreWarm(sr_GlobalShape);
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null)
    {
        if (postalCode.Falsey())
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        var trimmed = postalCode!.Trim();

        bool isMatch;
        try
        {
            isMatch = sr_GlobalShape.IsMatch(trimmed);
        }
        catch (RegexMatchTimeoutException)
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        if (!isMatch)
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        return D2Result<string>.Ok(trimmed);
    }

    /// <summary>
    /// Runs a throwaway match, which forces the compiled matcher to be emitted and
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
            _ = regex.IsMatch("90210");
        }
        catch (RegexMatchTimeoutException)
        {
            // Pre-warm only; the next real call pays the emit cost instead.
        }
    }
}
