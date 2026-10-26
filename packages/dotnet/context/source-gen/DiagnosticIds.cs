// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="ContextGenerator"/>.
/// The IDs are in a separate class from <see cref="DiagnosticDescriptors"/> so
/// that pure-logic code can use them without referencing
/// <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Context spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2CTX001";

    /// <summary>Spec property references a type outside the closed type vocabulary.</summary>
    public const string UnknownType = "D2CTX002";

    /// <summary>
    /// Two properties in the same interface or a parent interface declare the same name.
    /// </summary>
    public const string PropertyNameCollision = "D2CTX003";

    /// <summary>
    /// <c>extends</c> field references an interface namespace that was not provided
    /// to the generator (for example because of a typo, or because the auth-context
    /// spec is missing from the request-context project's AdditionalFiles).
    /// </summary>
    public const string UnresolvableExtends = "D2CTX004";

    /// <summary>
    /// Property declared <c>derived</c> with a rule name the emitter does not implement.
    /// </summary>
    public const string UnknownDerivedRule = "D2CTX005";

    /// <summary>
    /// No context spec file found among <c>AdditionalFiles</c> for a target assembly.
    /// </summary>
    public const string MissingSpecFile = "D2CTX006";

    /// <summary>
    /// Property declared <c>claimForm</c> with a value the emitter does not implement for
    /// the property's type.
    /// Falling through without a diagnostic would emit the default reading. For a set,
    /// that is the RFC 6749 scope parse, which drops elements it cannot use. The field
    /// exists to prevent that narrowing.
    /// </summary>
    public const string UnknownClaimForm = "D2CTX007";
}
