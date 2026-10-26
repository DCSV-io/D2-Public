// -----------------------------------------------------------------------
// <copyright file="OutboxOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using System.ComponentModel.DataAnnotations;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Configuration;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// The settings of one outbox relay. Every default is a concrete value in this
/// class and not a reference to a default defined elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RetryLimit"/>, <see cref="RetryBackoffBase"/> and
/// <see cref="RetryBackoffCeiling"/> together decide how long a broker outage is
/// survived, and they conflict: a bounded retry has to abandon a
/// poisoned message, and an outage has to delay delivery rather than end it.
/// The defaults resolve that at about sixty-five minutes: the early waits
/// double from two seconds until they reach the ceiling, and every later wait
/// equals the ceiling. Lower the limit or the ceiling and an
/// hour-long outage starts abandoning messages.
/// </para>
/// <para>
/// No jitter is added to the curve. One relay holds the advisory lock, so there
/// is no herd of simultaneous retries to spread, and jitter would only make the
/// wait harder to reason about from a row.
/// </para>
/// <para>
/// <b>Every bound here is an annotation rather than a comment, so
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> on
/// <see cref="OutboxOptionsFor{TDb}"/> rejects a misconfigured relay at
/// boot.</b> The failures they catch are all silent otherwise: a
/// <see cref="BatchSize"/> of zero makes the claim fail on every tick, a
/// non-positive <see cref="RelayTick"/> makes the timer throw once and stops the
/// relay, a <see cref="RetryLimit"/> below one abandons every message
/// before it is tried, and the <c>required</c> members
/// <see cref="RelayAdvisoryLockKey"/> and <see cref="RelayConnectionString"/> are
/// a compile-time construct the configuration binder does not apply - binding
/// this section without <c>AddD2Outbox</c> gives a null connection string and
/// one warning per tick while the messages stay staged.
/// </para>
/// </remarks>
public class OutboxOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SECTION = "LIB_OUTBOX";

    /// <summary>
    /// Gets or sets the advisory-lock key of the relay. It has no default,
    /// because the lock catalog that defines the key is outside this package;
    /// the value is a generated lock constant.
    /// </summary>
    [DeniedValues(0L)]
    public required long RelayAdvisoryLockKey { get; set; }

    /// <summary>
    /// Gets or sets the connection string the relay takes its advisory lock on.
    /// The same database the outbox table lives in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The relay cannot read this off the context it was registered over</b>,
    /// so it is an option rather than something derived. A session
    /// advisory lock needs its own physical connection, and the connection
    /// string a live provider hands back has had its password removed once the
    /// connection was opened - so a lock opened from it fails authentication on
    /// every tick, silently, while the messages pile up staged. Passing it
    /// explicitly is also what every other advisory-lock holder here does.
    /// </para>
    /// <para>
    /// It contains a password, so it is marked for redaction. The value stays in
    /// process - it is read by the relay and handed to Npgsql, and never crosses
    /// a wire, a cookie or a log line of its own - which is the case the
    /// attribute form covers.
    /// </para>
    /// </remarks>
    [RedactData(Reason = RedactReason.SecretInformation)]
    [Required(AllowEmptyStrings = false)]
    public required string RelayConnectionString { get; set; }

    /// <summary>Gets or sets how often the relay looks for due messages.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan RelayTick { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets how many messages one tick claims.</summary>
    [Range(1, int.MaxValue)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the total number of attempts a message is allowed. The waits
    /// are one fewer, because this counts attempts rather than waits.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int RetryLimit { get; set; } = 70;

    /// <summary>
    /// Gets or sets the first wait, doubling per attempt already made. The early
    /// retries are for a brief interruption - a reconnect, a leader election -
    /// and should be quick.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.001", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the longest wait the curve reaches. Past a minute a longer
    /// wait gains nothing and only delays recovery once the broker returns.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.001", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan RetryBackoffCeiling { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how long a published row is kept after the dispatcher
    /// confirmed it.
    /// </summary>
    /// <remarks>
    /// Inside this window an operator finds out from the table, and not from
    /// the broker, whether and when a message was announced. A week covers that
    /// question and keeps the table from growing without bound; the row contains
    /// a payload, so keeping it forever is a decision rather than an omission.
    /// No value means never, because the window exists to bound the table.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:00:01", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan PublishedRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets how long an abandoned row is kept after the last attempt
    /// failed.
    /// </summary>
    /// <remarks>
    /// An abandoned message is escalation rather than loss: the row keeps its
    /// payload and its last failure code so an operator can see what was not
    /// delivered and re-stage it. The window is therefore much longer than the
    /// published one, and it is still a window - a row nobody acted on in three
    /// months is not going to be re-staged, and it is holding a payload while it
    /// waits.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:00:01", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan AbandonedRetention { get; set; } = TimeSpan.FromDays(90);
}
