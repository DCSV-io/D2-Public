// -----------------------------------------------------------------------
// <copyright file="D2GeneratedHandlerScopeAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

/// <summary>
/// Marks a generated handler Scope partial so the analyzer can bind without a
/// fragile DefaultOptions AST walk. The emitter stamps this attribute on
/// every Scope partial.
/// </summary>
/// <param name="mode">Scope mode for this handler.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) : Attribute
{
    /// <summary>Gets the generated scope mode.</summary>
    public D2HandlerScopeMode Mode { get; } = mode;
}
