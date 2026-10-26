// -----------------------------------------------------------------------
// <copyright file="SessionLivenessCacheKey.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions.Sessions;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// The single declaration of the session-liveness cache key - the string that
/// the component that writes/removes the liveness sentinel and every component
/// that reacts to the resulting cache-invalidation broadcast must agree on.
/// </summary>
/// <remarks>
/// <para>
/// Shape: <c>{prefix}{sessionId:N}</c> - the prefix verbatim, then the session
/// id as 32 lowercase hex chars with no hyphens and no braces
/// (<see cref="System.Guid"/> format specifier <c>N</c>).
/// </para>
/// <para>
/// <strong>Why this type exists</strong>: the writer, the liveness reader, the
/// telemetry observer, and a token-exchange cache live in different
/// assemblies and all agree on this one string. Composing and parsing both
/// live here, so a change to either half cannot be made on one side only.
/// </para>
/// <para>
/// The prefix stays a caller-supplied configuration value rather than a
/// hard-coded constant, but every default is sourced from
/// <see cref="DEFAULT_PREFIX"/> - hosts that override it must override it for
/// every component in the same process.
/// </para>
/// <para>
/// <strong>The key contains the session id verbatim.</strong> The id is a lookup
/// handle, not a credential. The session cookie contains an encrypted frame with
/// a fresh nonce per seal, sealed under its own encryption key domain, and no
/// request path accepts a bare session id as an authenticator, so a key read out
/// of the store or off the invalidation channel replays nothing.
/// </para>
/// <para>
/// Reading a key still discloses something. A reader of the store or of the
/// invalidation channel learns which sessions exist and when each one ends, and
/// can match an id against the same id anywhere else it appears. A reader that
/// can also write to the store can end one specific person's session rather than
/// every session at once. The logging package keeps the id out of log lines for
/// those reasons and emits a correlation digest in its place.
/// </para>
/// <para>
/// The key contains the id rather than a digest because readers need the id
/// back: <see cref="TryParse"/> recovers it, and a cache that keeps a reverse
/// index by session id needs an invalidation to identify the session whose
/// entries it removes.
/// </para>
/// </remarks>
public static class SessionLivenessCacheKey
{
    /// <summary>
    /// The default key prefix. Every options type that exposes an overridable
    /// session-liveness prefix defaults to this value, so the components of an
    /// unconfigured host use the same key with no configuration at all.
    /// </summary>
    public const string DEFAULT_PREFIX = "session:";

    /// <summary>
    /// Length of a <see cref="System.Guid"/> rendered with the <c>N</c> format
    /// specifier - 32 hex chars, no hyphens.
    /// </summary>
    private const int _N_FORMAT_LENGTH = 32;

    /// <summary>
    /// Composes the cache key for <paramref name="sessionId"/>. This is the
    /// only sanctioned way to build the key - never interpolate the shape by
    /// hand.
    /// </summary>
    /// <param name="prefix">
    /// The configured key prefix (see <see cref="DEFAULT_PREFIX"/>).
    /// </param>
    /// <param name="sessionId">The session id.</param>
    /// <returns>The fully-qualified session-liveness cache key.</returns>
    public static string Compose(string prefix, Guid sessionId)
    {
        prefix.ThrowIfFalsey();
        return string.Concat(prefix, sessionId.ToString("N"));
    }

    /// <summary>
    /// Returns whether <paramref name="key"/> is addressed to the
    /// session-liveness namespace - a cheap filter for backplane subscribers
    /// that receive every invalidation key published by every component.
    /// </summary>
    /// <param name="key">
    /// The received invalidation key. Nullable: a backplane payload arrives
    /// from another process, so a null returns <c>false</c> rather
    /// than an exception thrown inside a subscriber callback.
    /// </param>
    /// <param name="prefix">
    /// The configured key prefix (see <see cref="DEFAULT_PREFIX"/>).
    /// </param>
    /// <returns>
    /// <c>true</c> when the key has the session-liveness prefix; this says
    /// nothing about whether the remainder is a well-formed session id - use
    /// <see cref="TryParse"/> for that.
    /// </returns>
    public static bool HasPrefix(string? key, string prefix)
    {
        prefix.ThrowIfFalsey();
        return key is not null && key.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Recovers the session id from a key produced by <see cref="Compose"/>.
    /// This is the only sanctioned way to read the key back.
    /// </summary>
    /// <param name="key">
    /// The received invalidation key; a null returns <c>false</c>.
    /// </param>
    /// <param name="prefix">
    /// The configured key prefix (see <see cref="DEFAULT_PREFIX"/>).
    /// </param>
    /// <param name="sessionId">
    /// The parsed session id on success; <see cref="Guid.Empty"/> otherwise.
    /// </param>
    /// <returns>
    /// <c>true</c> when the key has the prefix and the remainder is a
    /// well-formed <c>N</c>-rendered id. The rendering is matched exactly, so a
    /// hyphenated or brace-wrapped id is rejected rather than silently
    /// accepted - a mismatched producer fails loudly at the parse instead of
    /// going unnoticed.
    /// </returns>
    public static bool TryParse(string? key, string prefix, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        if (!HasPrefix(key, prefix))
            return false;

        // Length-check before the parse so an oversized hostile key is rejected
        // without handing a huge span to the parser.
        var idSpan = key.AsSpan(prefix.Length);

        if (idSpan.Length != _N_FORMAT_LENGTH)
            return false;

        return Guid.TryParseExact(idSpan, "N", out sessionId);
    }
}
