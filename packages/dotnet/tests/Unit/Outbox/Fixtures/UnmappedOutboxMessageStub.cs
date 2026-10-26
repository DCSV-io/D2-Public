// -----------------------------------------------------------------------
// <copyright file="UnmappedOutboxMessageStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;

/// <summary>
/// A fourth state, standing in for one a later change adds without giving the
/// mapper an arm for it. It exists so the mapper's default arm can be reached:
/// the shipped leaves are sealed and exhaustive, so nothing else can reach it.
/// </summary>
internal sealed record UnmappedOutboxMessageStub : OutboxMessage
{
    /// <inheritdoc/>
    public override OutboxMessageStatus Status => OutboxMessageStatus.Staged;
}
