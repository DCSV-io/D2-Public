// -----------------------------------------------------------------------
// <copyright file="KeepOnAnonymizationAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Records that a property holding personal data is kept on purpose when the
/// subject is erased, and why.
/// </summary>
/// <remarks>
/// <para>
/// The engine does not read this attribute and its behavior does not change.
/// The attribute marks a column as retained on purpose, so a completeness check
/// over the model can tell it from a column whose declaration was forgotten.
/// Without it the two look identical to every reader.
/// </para>
/// <para>
/// It is not a substitute for <see cref="AnonymizableAttribute"/>. A property
/// carrying both declares a rule the engine applies and a reason to keep it,
/// which is a contradiction. A completeness check over the model is the place
/// to catch it.
/// </para>
/// </remarks>
/// <param name="reason">
/// Why the value survives the erasure - the mechanism that already removes the
/// personal content, or the reason the value is not personal once the subject is
/// gone. Must be non-empty.
/// </param>
/// <exception cref="ArgumentException">
/// Thrown when <paramref name="reason"/> is null, empty or whitespace. The
/// attribute carries developer-authored literals only, so an empty reason is a
/// configuration bug and fails where it is declared.
/// </exception>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KeepOnAnonymizationAttribute(string reason) : Attribute
{
    /// <summary>Gets why this property's value survives an erasure.</summary>
    public string Reason { get; } = reason.Falsey()
        ? throw new ArgumentException(
            "KeepOnAnonymization requires a non-empty reason. A retained personal "
            + "column with no reason is indistinguishable from a forgotten one, "
            + "which is the whole failure this marker exists to stop.",
            nameof(reason))
        : reason;
}
