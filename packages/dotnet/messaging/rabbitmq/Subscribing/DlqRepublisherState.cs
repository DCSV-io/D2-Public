// -----------------------------------------------------------------------
// <copyright file="DlqRepublisherState.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

/// <summary>Republisher states retain every generation until resource completion.</summary>
internal abstract record DlqRepublisherState
{
    /// <summary>No transport has been allocated.</summary>
    internal sealed record Empty : DlqRepublisherState;

    /// <summary>Connection or channel acquisition is pending.</summary>
    /// <param name="Generation">The retained transport generation.</param>
    internal sealed record Opening(DlqTransportGeneration Generation) : DlqRepublisherState;

    /// <summary>A confirmed channel is available for serialized publications.</summary>
    /// <param name="Generation">The retained transport generation.</param>
    internal sealed record Ready(DlqTransportGeneration Generation) : DlqRepublisherState;

    /// <summary>Transport is terminated while client bookkeeping remains owned.</summary>
    /// <param name="Generation">The retained transport generation.</param>
    internal sealed record Retiring(DlqTransportGeneration Generation) : DlqRepublisherState;

    /// <summary>Admission is closed; repeated disposal shares the same bounded wait.</summary>
    /// <param name="Generation">Any generation whose resources remain owned.</param>
    /// <param name="BoundedWait">The close completion exposed to callers.</param>
    /// <param name="ResourceCompletion">The retained full cleanup completion.</param>
    internal sealed record Closed(
        DlqTransportGeneration? Generation,
        Task BoundedWait,
        Task ResourceCompletion) : DlqRepublisherState;
}
