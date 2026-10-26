// -----------------------------------------------------------------------
// <copyright file="OutboxPayloadSerializer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The one place a staged payload's text is produced.
/// </summary>
/// <remarks>
/// <b>These options are a persisted contract, not an implementation detail.</b>
/// Rows written by one release have to stay readable by the next release,
/// and a public-API report does not show a change to these options. A
/// golden-file test pins the bytes of a fixed payload for that reason: changing
/// the options fails that test, which states the cause, instead of corrupting a
/// backlog that nobody is watching.
/// </remarks>
public static class OutboxPayloadSerializer
{
    /// <summary>
    /// Gets the options every staged payload is written with. Camel-case
    /// property names, null properties omitted, no indentation.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Reads a payload back into the type its dispatcher knows it to be.
    /// </summary>
    /// <typeparam name="TPayload">The concrete payload type.</typeparam>
    /// <param name="payload">The stored text.</param>
    /// <returns>
    /// The payload, or null when the text does not describe one - which a
    /// dispatcher reads as a failure rather than as an empty message.
    /// </returns>
    /// <exception cref="JsonException">
    /// Thrown when the stored text is not valid JSON at all.
    /// </exception>
    public static TPayload? Deserialize<TPayload>(string payload)
        where TPayload : class
        => JsonSerializer.Deserialize<TPayload>(payload, Options);
}
