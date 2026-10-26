// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// The raw values of a generator's extension namespace and class prefix
/// properties, as MSBuild passes them. A property that is not set reads as the
/// empty string. The record is value-equatable, so the incremental pipeline
/// re-runs only when a value changes.
/// </summary>
/// <param name="Namespace">The value of the extension namespace property.</param>
/// <param name="ClassPrefix">The value of the extension class prefix property.</param>
internal sealed record ExtensionTargetOptions(string Namespace, string ClassPrefix);
