// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Connection-level configuration for the RabbitMQ-backed messaging stack.
/// Bound via <c>services.Configure&lt;RabbitMqConnectionOptions&gt;(...)</c>
/// or set inside the <c>AddD2MessagingRabbitMq</c> composition root.
/// </summary>
/// <remarks>
/// <para>
/// The broker address carries the broker credential, so it is kept in a private
/// field: <see cref="ConnectionUri"/> sets it and <see cref="RevealConnectionUri"/>
/// is the only read. A serializer, a debugger display or a structured log reads
/// public properties, and none of them returns the address.
/// </para>
/// <para>
/// <c>AddD2MessagingRabbitMq</c> validates the address at composition and refuses
/// one whose scheme is not <c>amqps</c>, unless its host is loopback or
/// <see cref="AllowUnencryptedTransport"/> is <c>true</c>. The dead-letter
/// transport copies these options after validation, so the same rule covers it.
/// </para>
/// </remarks>
[RedactData(Reason = RedactReason.SecretInformation)]
public sealed class RabbitMqConnectionOptions
{
    /// <summary>The scheme of an AMQP connection over TLS.</summary>
    private const string _ENCRYPTED_SCHEME = "amqps";

    [RedactData(Reason = RedactReason.SecretInformation)]
    private string _connectionUri = string.Empty;

    /// <summary>
    /// Sets the AMQP connection URI
    /// (<c>amqps://user:pass@host:port/vhost</c>), which carries the broker
    /// credential. Required. It is read back only through
    /// <see cref="RevealConnectionUri"/>.
    /// </summary>
    /// <remarks>
    /// An <c>amqp://</c> address is admitted only for a loopback host or with
    /// <see cref="AllowUnencryptedTransport"/> set.
    /// </remarks>
    [RedactData(Reason = RedactReason.SecretInformation)]
    public string ConnectionUri
    {
        set => _connectionUri = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether a broker address whose scheme is
    /// not <c>amqps</c> may use a host that is not loopback. Defaults to
    /// <c>false</c>. Set it only where nothing can observe the network path to
    /// the broker, such as a local container network: the credential and every
    /// message body cross that path unencrypted.
    /// </summary>
    public bool AllowUnencryptedTransport { get; set; }

    /// <summary>
    /// Gets or sets the descriptive client-provided name reported to the
    /// broker. Defaults to <c>DcsvIo.D2.Messaging</c>; overriding helps
    /// distinguish replicas in management UI.
    /// </summary>
    public string ClientProvidedName { get; set; } = "DcsvIo.D2.Messaging";

    /// <summary>
    /// Gets or sets the dispatch concurrency for async consumers (number of
    /// in-flight handler invocations the dispatcher allows per channel).
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <remarks>
    /// Values greater than 1 remove the per-channel ordering guarantee - the
    /// broker dispatches messages in order, but the .NET client may invoke
    /// the <c>ReceivedAsync</c> callback for delivery N+1 before delivery N
    /// has finished. Handlers MUST be thread-safe and idempotent under this
    /// default. Set to <c>1</c> if strict in-order processing per queue is
    /// required (e.g. event-sourced projections).
    /// </remarks>
    public ushort ConsumerDispatchConcurrency { get; set; }
        = (ushort)Math.Min(ushort.MaxValue, Environment.ProcessorCount);

    /// <summary>
    /// Gets or sets the initial backoff between reconnection attempts.
    /// Doubles on each failure up to <see cref="MaxReconnectDelay"/>.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the cap on exponential backoff between reconnection
    /// attempts. The host retries forever, so a persistent outage shows as
    /// repeated reconnect-attempt-failed warnings and not as a crashed replica.
    /// Defaults to 60 seconds.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the interval at which the reconnect loop checks that the
    /// existing connection is open, and logs when it is not. RabbitMQ.Client
    /// 7.x's automatic recovery handles in-flight failures; this check logs a
    /// connection that stays down, for example after the recovery handshake
    /// failed. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets a value indicating whether a connection URI was set.</summary>
    internal bool HasConnectionUri => _connectionUri.Truthy();

    /// <summary>
    /// Returns the connection URI with the broker credential in it. This is the
    /// only read of the address, for building a connection to the broker.
    /// </summary>
    /// <returns>The URI <see cref="ConnectionUri"/> set, or an empty string.</returns>
    public string RevealConnectionUri() => _connectionUri;

    /// <summary>
    /// Reports whether the address may be used: its scheme is <c>amqps</c>, its
    /// host is loopback, or <see cref="AllowUnencryptedTransport"/> is set.
    /// </summary>
    /// <remarks>
    /// An address that was never set is admitted here, because it specifies no
    /// transport; starting the connection refuses it. An address that is not an absolute
    /// URI is refused, because nothing shows its transport is encrypted.
    /// </remarks>
    /// <returns><c>true</c> when the address may be used.</returns>
    internal bool IsTransportAdmitted()
    {
        if (!HasConnectionUri)
            return true;

        if (!Uri.TryCreate(_connectionUri, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Scheme, _ENCRYPTED_SCHEME, StringComparison.OrdinalIgnoreCase)
            || uri.IsLoopback
            || AllowUnencryptedTransport;
    }

    /// <summary>
    /// Copies the settings a separate connection to the same broker needs: the
    /// address, the client name and the transport choice.
    /// </summary>
    /// <returns>A new instance holding the copied settings.</returns>
    internal RabbitMqConnectionOptions CopyConnectionSettings() => new()
    {
        _connectionUri = _connectionUri,
        ClientProvidedName = ClientProvidedName,
        AllowUnencryptedTransport = AllowUnencryptedTransport,
    };
}
