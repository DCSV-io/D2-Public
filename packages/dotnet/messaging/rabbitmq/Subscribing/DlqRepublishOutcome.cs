// -----------------------------------------------------------------------
// <copyright file="DlqRepublishOutcome.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

/// <summary>The terminal result of an owned dead-letter operation.</summary>
internal enum DlqRepublishOutcome
{
    /// <summary>The broker confirmed mandatory publication.</summary>
    Confirmed,

    /// <summary>The broker returned the unroutable publication.</summary>
    Unroutable,

    /// <summary>Transport failed before confirmed publication.</summary>
    TransportFailure,

    /// <summary>The original operation deadline elapsed.</summary>
    DeadlineExceeded,

    /// <summary>The subscription stopped admitting work.</summary>
    Stopping,

    /// <summary>A retired generation still owns unfinished cleanup.</summary>
    CleanupPending,
}
