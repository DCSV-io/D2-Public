// -----------------------------------------------------------------------
// <copyright file="MalformedActorChainException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;

/// <summary>
/// Thrown by <see cref="ActorChainParser"/> when the JWT <c>act</c> claim is
/// structurally malformed: invalid JSON, exceeds the maximum delegation
/// depth, missing a required claim per RFC 8693 section 2.1, or violates D2
/// conventions (e.g. an impersonation entry missing <c>d2_session_id</c>).
/// </summary>
/// <remarks>
/// <para>
/// This exception MUST be converted to <c>D2Result.Unauthorized</c> (HTTP 401),
/// because a malformed actor chain is a signed token with a bad payload and should
/// never reach a handler. If it propagates through
/// <c>BaseHandler.RunCorePipelineAsync</c> it surfaces as
/// <c>UnhandledException</c> (500), which is the wrong status.
/// </para>
/// <para>
/// The exception message includes the specific malformation reason, so logs and
/// alerts can identify a token issuer that does not conform.
/// </para>
/// </remarks>
public sealed class MalformedActorChainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MalformedActorChainException"/> class.
    /// </summary>
    /// <param name="message">
    /// Specific reason the chain is malformed (e.g. "depth 21 exceeds
    /// maximum 20", "impersonation entry at depth 2 missing d2_session_id").
    /// </param>
    public MalformedActorChainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MalformedActorChainException"/> class.
    /// </summary>
    /// <param name="message">Specific reason the chain is malformed.</param>
    /// <param name="innerException">
    /// The underlying cause (e.g. a <see cref="System.Text.Json.JsonException"/>
    /// for invalid JSON).
    /// </param>
    public MalformedActorChainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
