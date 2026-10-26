// -----------------------------------------------------------------------
// <copyright file="OutboxMessageStatus.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

/// <summary>
/// The persistence discriminator for a staged message, derived from the
/// message's own type rather than deciding which transitions are legal.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Written with the caller's change and not yet delivered.</summary>
    Staged = 0,

    /// <summary>Handed to its dispatcher, which confirmed the delivery.</summary>
    Published = 1,

    /// <summary>
    /// Ran out of attempts. The payload and the last failure code are kept and
    /// an operator is alerted; nothing is deleted.
    /// </summary>
    Abandoned = 2,
}
