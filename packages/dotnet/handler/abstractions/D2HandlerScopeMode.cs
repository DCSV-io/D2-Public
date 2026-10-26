// -----------------------------------------------------------------------
// <copyright file="D2HandlerScopeMode.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

/// <summary>
/// Scope mode stamped on generated handler Scope partials for analyzer binding.
/// </summary>
public enum D2HandlerScopeMode
{
    /// <summary>At-least-one scope required.</summary>
    Any = 0,

    /// <summary>Every declared scope required.</summary>
    All = 1,

    /// <summary>Infrastructure-exempt; no ScopeRequirement.</summary>
    InfraExempt = 2,
}
