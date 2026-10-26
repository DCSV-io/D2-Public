// -----------------------------------------------------------------------
// <copyright file="ID2ForgeryPostureDeclaration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// The forgery posture a routed endpoint declares, read from endpoint
/// metadata by the forgery-protection middleware.
/// </summary>
/// <remarks>
/// A code generator that stamps a posture record onto a route registration
/// implements this interface on that record, so the middleware resolves the
/// posture without referencing the generated type. A non-safe request whose
/// endpoint has no implementation is treated as
/// <see cref="D2ForgeryPostures.REQUIRED"/>.
/// </remarks>
public interface ID2ForgeryPostureDeclaration
{
    /// <summary>
    /// Gets the declared posture: one of the values on
    /// <see cref="D2ForgeryPostures"/>. A value outside that set is treated
    /// as <see cref="D2ForgeryPostures.REQUIRED"/>.
    /// </summary>
    string Posture { get; }
}
