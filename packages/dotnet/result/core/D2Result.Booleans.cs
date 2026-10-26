// -----------------------------------------------------------------------
// <copyright file="D2Result.Booleans.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

using System.Net;
using System.Text.Json.Serialization;

/// <summary>
/// Hand-rolled boolean discriminators on <see cref="D2Result"/> that are not
/// derived from a single error code - the success / status-based
/// (<see cref="IsOk"/> / <see cref="IsCreated"/>) and the concept-named
/// composite (<see cref="IsPartialOrMissing"/> / <see cref="IsTransientRetryable"/>)
/// helpers. The 1:1 per-error-code discriminators (<c>IsNotFound</c> /
/// <c>IsConflict</c> / ...) are generated onto the same partial class from the
/// error-code spec - see <c>D2Result.Booleans.g.cs</c>. Prefer these over manual
/// <c>ErrorCode == ErrorCodes.X</c> comparisons or <c>StatusCode ==
/// HttpStatusCode.X</c> checks - they read better at the call site.
/// </summary>
/// <remarks>
/// All discriminators are derived from <see cref="Success"/> / <see cref="StatusCode"/>
/// / <see cref="ErrorCode"/> and carry <see cref="JsonIgnoreAttribute"/> - they are
/// in-process call-site helpers, not part of the D2Result Shape B wire envelope
/// (which is enumerated in <see cref="D2ResultEnvelopeFieldNames"/>). Without
/// <c>[JsonIgnore]</c> they would add fields such as
/// <c>{"isOk": true, "isNotFound": false, ...}</c> to the wire output, which a
/// receiver would have to filter out.
/// </remarks>
public partial class D2Result
{
    /// <summary>
    /// Gets a value indicating whether this result represents a successful (Ok) outcome.
    /// </summary>
    [JsonIgnore]
    public bool IsOk => Success;

    /// <summary>
    /// Gets a value indicating whether this result represents a Created outcome (HTTP 201).
    /// </summary>
    [JsonIgnore]
    public bool IsCreated => StatusCode == HttpStatusCode.Created;

    /// <summary>
    /// Gets a value indicating whether this result is a partial / missing query
    /// outcome - either <see cref="IsNotFound"/> or <see cref="IsSomeFound"/>. Useful
    /// for cache-fallback flows where "we found some" or "we found none" both warrant
    /// a downstream lookup, while other failures (Forbidden, etc.) do not.
    /// </summary>
    [JsonIgnore]
    public bool IsPartialOrMissing => IsNotFound || IsSomeFound;

    /// <summary>
    /// Gets a value indicating whether this result represents a transient retryable
    /// failure - <see cref="IsServiceUnavailable"/> or <see cref="IsRateLimited"/>.
    /// <para>
    /// <see cref="IsUnhandledException"/> is excluded on purpose. An unknown exception means
    /// unknown system state - retrying could mask bugs or run side effects twice.
    /// Retry helpers consult this property.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public bool IsTransientRetryable => IsServiceUnavailable || IsRateLimited;
}
