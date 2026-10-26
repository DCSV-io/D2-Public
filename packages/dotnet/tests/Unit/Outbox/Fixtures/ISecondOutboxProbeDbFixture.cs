// -----------------------------------------------------------------------
// <copyright file="ISecondOutboxProbeDbFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A second consumer's application-layer context interface, so a host composing
/// two modules over two databases is expressible and each module's seams resolve
/// separately.
/// </summary>
internal interface ISecondOutboxProbeDbFixture
{
    /// <summary>Gets the staged messages.</summary>
    DbSet<OutboxMessageRow> OutboxMessages { get; }
}
