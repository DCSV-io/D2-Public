// -----------------------------------------------------------------------
// <copyright file="SearchRankField.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;

/// <summary>
/// One rank field and its weight.
/// </summary>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
/// <param name="Field">The text column this field ranks on.</param>
/// <param name="Weight">Relative weight; a higher weight outranks a lower one.</param>
public sealed record SearchRankField<TRow>(Expression<Func<TRow, string>> Field, double Weight);
