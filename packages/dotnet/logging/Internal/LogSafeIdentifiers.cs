// -----------------------------------------------------------------------
// <copyright file="LogSafeIdentifiers.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Internal;

using System.Security.Cryptography;
using DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Turns session identifiers into stable, non-reversible correlation digests so
/// structured logs stay useful without containing the session identifiers themselves.
/// </summary>
/// <remarks>
/// <para>
/// A session id does not authenticate a request - the session cookie carries an encrypted
/// frame rather than the id - but it is the handle every record about one session shares.
/// A log line that contains it lets anyone with log-read access join that session across
/// every log and across the session cache, tie it to one person, and identify that
/// person's session to anything able to end it. Operators still need to answer "which
/// lines belong to one session", which a digest satisfies exactly as well as the raw
/// value.
/// </para>
/// <para>
/// The digest is a truncated SHA-256 over a fixed domain-separation prefix plus the 16 raw
/// identifier bytes. It is deterministic across processes and restarts (no random salt), so
/// correlation works across every service in a request path; it is not reversible, because a
/// v4/v7 GUID carries enough entropy that no session-id preimage search over that space is
/// feasible. The prefix keeps these digests from matching a digest of the same GUID taken
/// for some unrelated purpose.
/// </para>
/// </remarks>
internal static class LogSafeIdentifiers
{
    /// <summary>
    /// Log property name for the digest of <c>IRequestContext.SessionId</c>. The raw
    /// field name is deliberately not reused - a property called <c>SessionId</c> whose
    /// value is not the session id would mislead every operator who reads it.
    /// </summary>
    internal const string SESSION_ID_HASH = "SessionIdHash";

    /// <summary>
    /// Log property name for the digest of
    /// <c>IRequestContext.ImpersonationSessionId</c>.
    /// </summary>
    internal const string IMPERSONATION_SESSION_ID_HASH = "ImpersonationSessionIdHash";

    /// <summary>Digest length in hex characters (8 bytes / 64 bits of SHA-256).</summary>
    internal const int DIGEST_HEX_LENGTH = 16;

    private const int _DIGEST_BYTES = DIGEST_HEX_LENGTH / 2;

    /// <summary>Mirrors <c>ActorChainParser.MaxActDepth</c> - the parser's own DoS cap.</summary>
    private const int _MAX_ACT_DEPTH = 20;

    /// <summary>
    /// Computes the stable correlation digest for a session identifier.
    /// </summary>
    /// <param name="sessionId">Live session identifier - never emitted itself.</param>
    /// <returns>Lowercase hex digest of <see cref="DIGEST_HEX_LENGTH"/> characters.</returns>
    internal static string SessionDigest(Guid sessionId)
    {
        ReadOnlySpan<byte> domain = "d2.log.session.v1"u8;

        Span<byte> input = stackalloc byte[domain.Length + 16];
        domain.CopyTo(input);

        // Guid.TryWriteBytes only fails on a short destination, which the sizing above rules
        // out; the layout is endianness-stable for correlation because every process that
        // computes the digest runs this same code path on the same value.
        _ = sessionId.TryWriteBytes(input[domain.Length..]);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(input, digest);

        return Convert.ToHexStringLower(digest[.._DIGEST_BYTES]);
    }

    /// <summary>
    /// Projects an actor chain into its log-safe shape, replacing each entry's session id
    /// with a correlation digest.
    /// </summary>
    /// <remarks>
    /// <c>ActorEntry.SessionId</c> also has <c>[RedactData(SecretInformation)]</c>, but
    /// that marker only fires when Serilog destructures the entry STRUCTURALLY. Sequence
    /// elements can fall back to <c>ToString()</c> depending on how the property is bound,
    /// and a record's <c>ToString()</c> prints every member - including the session id. So
    /// the raw value is removed here, before it reaches a sink; the attribute stays as a
    /// second layer of protection for any other capture site.
    /// </remarks>
    /// <param name="chain">Actor chain from the request context.</param>
    /// <returns>Log-safe projection, one entry per input entry.</returns>
    internal static IReadOnlyList<LogSafeActorEntry> ToLogSafe(IReadOnlyList<ActorEntry> chain)
    {
        var projected = new List<LogSafeActorEntry>(chain.Count);

        foreach (var entry in chain)
            projected.Add(Project(entry, depth: 0));

        return projected;
    }

    private static LogSafeActorEntry Project(ActorEntry entry, int depth)
    {
        // The depth cap bounds recursion here even though the parser already
        // rejects deeper chains. A chain the cap stopped is marked, so an
        // operator can tell it from one that ended.
        var withinCap = depth < _MAX_ACT_DEPTH;

        return new LogSafeActorEntry(
            entry.Kind,
            entry.Subject,
            entry.ClientId,
            entry.ImpersonationKind,
            entry.SessionId is { } sessionId ? SessionDigest(sessionId) : null,
            entry.OrgId,
            entry.OrgName,
            entry.OrgType,
            entry.OrgRole,
            withinCap && entry.Act is { } act ? Project(act, depth + 1) : null,
            !withinCap && entry.Act is not null);
    }
}
