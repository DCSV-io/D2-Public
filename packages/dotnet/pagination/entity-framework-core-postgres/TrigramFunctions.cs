// -----------------------------------------------------------------------
// <copyright file="TrigramFunctions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;

using System.Reflection;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The provider function handles the trigram dialect builds its expressions
/// from, resolved once for the process rather than once per closed generic
/// dialect type.
/// </summary>
internal static class TrigramFunctions
{
    /// <summary>The case-insensitive pattern predicate, with an escape clause.</summary>
    public static readonly MethodInfo SR_ILike =
        typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException(
            "NpgsqlDbFunctionsExtensions.ILike(DbFunctions, string, string, string) "
            + "was not found.");

    /// <summary>The trigram similarity between two strings.</summary>
    public static readonly MethodInfo SR_Similarity =
        typeof(NpgsqlTrigramsDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlTrigramsDbFunctionsExtensions.TrigramsSimilarity),
            [typeof(DbFunctions), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException(
            "NpgsqlTrigramsDbFunctionsExtensions.TrigramsSimilarity(DbFunctions, string, "
            + "string) was not found.");
}
