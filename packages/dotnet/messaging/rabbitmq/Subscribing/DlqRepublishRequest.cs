// -----------------------------------------------------------------------
// <copyright file="DlqRepublishRequest.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

/// <summary>Preserves original bytes and properties for dead-letter publication.</summary>
/// <param name="DlxName">The existing dead-letter exchange.</param>
/// <param name="Body">The original wire body.</param>
/// <param name="Headers">Copied headers including failure metadata.</param>
/// <param name="MessageId">The original AMQP message-id property.</param>
/// <param name="ContentType">The original content type.</param>
/// <param name="ContentEncoding">The original content encoding.</param>
/// <param name="CorrelationId">The original correlation identifier.</param>
internal sealed record DlqRepublishRequest(
    string DlxName,
    ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, object?> Headers,
    string? MessageId,
    string? ContentType,
    string? ContentEncoding,
    string? CorrelationId);
