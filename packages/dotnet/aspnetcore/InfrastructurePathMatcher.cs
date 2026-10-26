// -----------------------------------------------------------------------
// <copyright file="InfrastructurePathMatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Public helper that decides whether a given <see cref="PathString"/>
/// matches any of a configured set of infrastructure-path prefixes
/// (<c>/health</c>, <c>/alive</c>, <c>/metrics</c>, <c>/.well-known</c> by
/// default - see
/// <see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>).
/// </summary>
/// <remarks>
/// <para>
/// Single source of truth for infrastructure-path matching across the D2
/// shared-lib stack: it decides whether a request path is an infrastructure
/// path, and returns <c>true</c> or <c>false</c>.
/// </para>
/// <para>
/// Uses the AspNetCore-canonical
/// <see cref="PathString.StartsWithSegments(PathString)"/> overload, which is
/// case-insensitive by default and matches on full segment boundaries - so
/// <c>/healthz</c> does NOT match prefix <c>/health</c>; <c>/health/db</c>
/// does. Empty <see cref="PathString"/>, null configured list, and per-entry
/// null / empty / whitespace prefixes are all defensive no-ops (matcher
/// returns <c>false</c> rather than throwing) so the helper is safe with
/// hand-built lists.
/// </para>
/// </remarks>
public static class InfrastructurePathMatcher
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="path"/> starts with any of
    /// the segment prefixes in <paramref name="infrastructurePaths"/>.
    /// </summary>
    /// <param name="path">The request path to test.</param>
    /// <param name="infrastructurePaths">
    /// Configured prefix list. <c>null</c> / empty list / per-entry
    /// null / empty / whitespace are skipped defensively (invalid lists SHOULD
    /// be rejected at host build via options validation, but the
    /// runtime guard keeps this helper safe with hand-built lists).
    /// </param>
    /// <returns>
    /// <c>true</c> when <paramref name="path"/> matches any prefix; otherwise
    /// <c>false</c>. Returns <c>false</c> for an empty <see cref="PathString"/>
    /// or a null / empty configured list.
    /// </returns>
    public static bool IsInfrastructurePath(
        PathString path,
        IReadOnlyList<string>? infrastructurePaths)
    {
        if (infrastructurePaths is null || infrastructurePaths.Count == 0)
            return false;

        if (!path.HasValue)
            return false;

        foreach (var prefix in infrastructurePaths)
        {
            if (prefix.Falsey())
                continue;

            if (path.StartsWithSegments(new PathString(prefix)))
                return true;
        }

        return false;
    }
}
