// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetSelection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// The target <see cref="ExtensionTargetSelector.Select"/> chose for one
/// compilation, with the trimmed property values it chose from.
/// </summary>
/// <param name="Kind">Where the output goes, or why there is none.</param>
/// <param name="Namespace">The trimmed extension namespace value; empty when
/// the property is not set or the target is public.</param>
/// <param name="ClassPrefix">The trimmed extension class prefix value; empty
/// when the property is not set or the target is public.</param>
/// <param name="NamespaceInvalid">Whether the namespace value is set and is not
/// a dot-separated sequence of C# identifiers.</param>
/// <param name="ClassPrefixInvalid">Whether the namespace value is set and the
/// class prefix value is absent or not a C# identifier.</param>
internal sealed record ExtensionTargetSelection(
    ExtensionTargetKind Kind,
    string Namespace,
    string ClassPrefix,
    bool NamespaceInvalid,
    bool ClassPrefixInvalid);
