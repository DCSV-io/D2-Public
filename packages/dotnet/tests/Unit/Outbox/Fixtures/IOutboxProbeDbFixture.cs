// -----------------------------------------------------------------------
// <copyright file="IOutboxProbeDbFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A consumer's application-layer context interface, standing in for the real
/// ones a service declares. The outbox is keyed on it: every seam these tests
/// register or resolve is identified by this type rather than by a string.
/// </summary>
internal interface IOutboxProbeDbFixture
{
    /// <summary>Gets the staged messages.</summary>
    DbSet<OutboxMessageRow> OutboxMessages { get; }
}
