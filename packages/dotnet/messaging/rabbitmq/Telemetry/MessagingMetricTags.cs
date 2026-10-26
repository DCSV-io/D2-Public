// -----------------------------------------------------------------------
// <copyright file="MessagingMetricTags.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Telemetry;

/// <summary>
/// The tag key and the closed set of tag values the messaging counters emit.
/// Each value is declared once as a constant, so no site repeats a literal.
/// </summary>
internal static class MessagingMetricTags
{
    /// <summary>Tag key that identifies the failure class on an error counter.</summary>
    public const string OUTCOME = "outcome";

    /// <summary>
    /// The broker returned the publish because it matched no queue, and it was
    /// still matching none when the retries ran out.
    /// </summary>
    public const string PUBLISH_FAIL_UNROUTABLE = "publish_fail_unroutable";

    /// <summary>
    /// The publish failed for a transport reason: the broker was unreachable,
    /// the channel went down, or the confirm never arrived.
    /// </summary>
    public const string PUBLISH_FAIL_TRANSPORT = "publish_fail_transport";

    /// <summary>
    /// The dead-letter republish was returned because the dead-letter exchange
    /// matched no queue.
    /// </summary>
    public const string REPUBLISH_FAIL_UNROUTABLE = "republish_fail_unroutable";

    /// <summary>
    /// The dead-letter republish failed for a transport reason rather than a
    /// routing one.
    /// </summary>
    public const string REPUBLISH_FAIL_TRANSPORT = "republish_fail_transport";
}
