// -----------------------------------------------------------------------
// <copyright file="IOutboxHostDbFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The application-layer context interface a consumer of this package declares,
/// and the identity the store and the staging seam are keyed on.
/// </summary>
internal interface IOutboxHostDbFixture
{
    /// <summary>Gets the staged messages.</summary>
    DbSet<OutboxMessageRow> OutboxMessages { get; }
}
