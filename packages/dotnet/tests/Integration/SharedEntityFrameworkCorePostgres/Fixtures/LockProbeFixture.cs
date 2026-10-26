// -----------------------------------------------------------------------
// <copyright file="LockProbeFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;

using System;

/// <summary>One row the lock helpers take a lock on.</summary>
internal sealed class LockProbeFixture
{
    /// <summary>Gets or sets the key the row lock is taken on.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets what the winning caller wrote.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value the model ignores, so the identifier resolution has
    /// an unmapped property to reject.
    /// </summary>
    public Guid Unmapped { get; set; }
}
