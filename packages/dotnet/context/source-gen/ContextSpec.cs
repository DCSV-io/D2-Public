// -----------------------------------------------------------------------
// <copyright file="ContextSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of an interface spec file (for example <c>IAuthContext.spec.json</c> or
/// <c>IRequestContext.spec.json</c>). The generator emits a read-only interface
/// from this spec. <see cref="MutableEmitter"/> emits the mutable concrete class from the
/// combined properties of every spec parsed in the same generator run:
/// non-derived properties become settable properties and derived ones become
/// computed getters (MutableEmitter.cs).
/// </summary>
/// <param name="Name">The interface name (for example <c>"IAuthContext"</c>).</param>
/// <param name="Namespace">The .NET namespace the interface lives in.</param>
/// <param name="Description">Free-form description rendered as XML doc on the interface.</param>
/// <param name="Extends">
/// Fully-qualified base interface name, or null when the interface has no base.
/// </param>
/// <param name="Sections">Ordered list of property sections.</param>
internal sealed record ContextSpec(
    string Name,
    string Namespace,
    string? Description,
    string? Extends,
    ImmutableArray<Section> Sections);
