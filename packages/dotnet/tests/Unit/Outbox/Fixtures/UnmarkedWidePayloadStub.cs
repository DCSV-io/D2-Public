// -----------------------------------------------------------------------
// <copyright file="UnmarkedWidePayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload with no marker anywhere, over every shape the walk distinguishes:
/// a nullable value, an enum, a framework generic, an array and a reference
/// back to its own type.
/// </summary>
internal sealed class UnmarkedWidePayloadStub
{
    /// <summary>Gets or sets a nullable value.</summary>
    public int? Count { get; set; }

    /// <summary>Gets or sets an enum value.</summary>
    public DayOfWeek Day { get; set; }

    /// <summary>Gets or sets a framework generic.</summary>
    public Dictionary<string, Guid> Index { get; set; } = [];

    /// <summary>Gets or sets an array.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Gets or sets a reference back to this type.</summary>
    public UnmarkedWidePayloadStub? Next { get; set; }
}
