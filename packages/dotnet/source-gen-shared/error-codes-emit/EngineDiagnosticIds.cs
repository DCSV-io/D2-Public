// -----------------------------------------------------------------------
// <copyright file="EngineDiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

/// <summary>
/// String identifiers for the catalog-neutral diagnostics the unified
/// error-codes engine surfaces for any catalog (the <c>D2ERC*</c> family).
/// Distinct from each catalog's per-catalog validation
/// diagnostics (<c>D2EC*</c> generic / <c>D2AEC*</c> auth), so the ids
/// and READMEs of those diagnostics stay unchanged. Kept as plain string
/// constants so pure-logic emitters can reference them without referencing
/// <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class EngineDiagnosticIds
{
    /// <summary>
    /// A code does not start with the catalog's enforced full DomainPrefix
    /// (e.g. a non-<c>LIB_AUTH_</c> code in the auth gate catalog).
    /// </summary>
    public const string DomainPrefixViolation = "D2ERC001";

    /// <summary>
    /// An entry's <c>userMessageKey</c> does not inverse-resolve to a key in
    /// <c>contracts/messages/en-US.json</c>.
    /// </summary>
    public const string TkKeyNotFound = "D2ERC002";

    /// <summary>
    /// A <c>factoryShape</c> value is not emitted on the delegating per-domain
    /// path. The delegating <c>&lt;Domain&gt;Failures</c> emitter implements the
    /// universal <c>standard</c> shape (every domain catalog's entire set) +
    /// <c>none</c> (skip); any other (hand-malformed) value reports this
    /// diagnostic. The schema constrains <c>factoryShape</c> to those two
    /// values, so a conforming spec never triggers it.
    /// </summary>
    public const string UnsupportedFactoryShape = "D2ERC003";
}
