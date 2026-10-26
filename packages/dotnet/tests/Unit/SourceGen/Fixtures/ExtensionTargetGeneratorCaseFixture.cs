// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetGeneratorCaseFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

/// <summary>
/// One generator that has a public target and an extension target, with the
/// inputs that make it emit cleanly and the names it is expected to use.
/// </summary>
/// <param name="CreateGenerator">Creates a fresh generator instance.</param>
/// <param name="PublicAssemblyName">The assembly name of the public target.</param>
/// <param name="NamespaceProperty">The extension namespace property.</param>
/// <param name="ClassPrefixProperty">The extension class prefix property.</param>
/// <param name="AdditionalFiles">Spec files that produce output with no diagnostic.</param>
/// <param name="Sources">Source files the compilation needs, if any.</param>
/// <param name="PublicHintNames">The files the public target writes.</param>
/// <param name="BaseClassNames">The public class names the prefix goes before;
/// each extension file is the prefixed name followed by <c>.g.cs</c>.</param>
/// <param name="InvalidNamespaceId">The id of the invalid-namespace error.</param>
/// <param name="InvalidClassPrefixId">The id of the invalid-prefix error.</param>
/// <param name="ClassPrefixWithoutNamespaceId">The id of the prefix-only warning.</param>
internal sealed record ExtensionTargetGeneratorCaseFixture(
    Func<IIncrementalGenerator> CreateGenerator,
    string PublicAssemblyName,
    string NamespaceProperty,
    string ClassPrefixProperty,
    IReadOnlyList<(string Path, string Content)> AdditionalFiles,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> PublicHintNames,
    IReadOnlyList<string> BaseClassNames,
    string InvalidNamespaceId,
    string InvalidClassPrefixId,
    string ClassPrefixWithoutNamespaceId);
