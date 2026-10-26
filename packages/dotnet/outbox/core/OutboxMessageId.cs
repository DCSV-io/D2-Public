// -----------------------------------------------------------------------
// <copyright file="OutboxMessageId.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

/// <summary>
/// The identity of one staged message.
/// </summary>
/// <param name="Value">
/// The underlying identifier. Version 7, so insertion order and index locality
/// agree.
/// </param>
public readonly record struct OutboxMessageId(Guid Value)
{
    /// <summary>Mints a new identity.</summary>
    /// <returns>An identity nothing else has been given.</returns>
    public static OutboxMessageId New() => new(Guid.CreateVersion7());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
