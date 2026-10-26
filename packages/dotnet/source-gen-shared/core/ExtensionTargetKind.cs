// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// Where a generator with a fixed public target and an optional extension
/// target writes its output for one compilation.
/// </summary>
internal enum ExtensionTargetKind
{
    /// <summary>No output and no diagnostic: the compilation is not the public
    /// target and sets no extension property.</summary>
    None = 0,

    /// <summary>The public output, under the generator's own namespace and
    /// class names. The extension properties are ignored.</summary>
    Public = 1,

    /// <summary>The extension output, under the namespace and class prefix the
    /// extension properties set.</summary>
    Extension = 2,

    /// <summary>No output, with an error diagnostic: the namespace property is
    /// set and the namespace or the class prefix is not valid.</summary>
    Rejected = 3,

    /// <summary>No output, with a warning diagnostic: the class prefix property
    /// is set and the namespace property is not.</summary>
    ClassPrefixWithoutNamespace = 4,
}
