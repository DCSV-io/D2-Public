// -----------------------------------------------------------------------
// <copyright file="SampleOutboxIntegrationKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox.Fixtures;

/// <summary>
/// A consumer's own closed kind set, standing in for the enums real consumers
/// declare. Its member name is what reaches the <c>kind</c> column.
/// </summary>
internal enum SampleOutboxIntegrationKind
{
    /// <summary>The sample kind.</summary>
    ThingHappened = 0,
}
