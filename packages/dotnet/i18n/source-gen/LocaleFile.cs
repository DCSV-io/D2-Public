// -----------------------------------------------------------------------
// <copyright file="LocaleFile.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

/// <summary>
/// Value-equatable record used as the IIncrementalGenerator pipeline boundary.
/// Incremental caching requires equatable types at every step boundary.
/// Roslyn re-runs a step only when the value equality of its input changes,
/// so a non-equatable boundary type prevents caching.
/// </summary>
/// <param name="Locale">
/// The locale code derived from the JSON filename (e.g. <c>"en-US"</c>).
/// </param>
/// <param name="Content">The raw JSON content.</param>
internal sealed record LocaleFile(string Locale, string Content);
