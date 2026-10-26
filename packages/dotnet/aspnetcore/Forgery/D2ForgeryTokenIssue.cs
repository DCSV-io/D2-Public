// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenIssue.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A freshly sealed forgery token and the expiry of the cookie that carries it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExpiresAt"/> is a <see cref="DateTimeOffset"/> because it is
/// assigned straight to
/// <see cref="Microsoft.AspNetCore.Http.CookieOptions.Expires"/>, which
/// declares that type. A host holding a NodaTime instant converts at this
/// boundary and nowhere further in.
/// </para>
/// <para>
/// <see cref="Value"/> holds live token material, so it is marked for
/// redaction and <see cref="ToString"/> renders
/// <see cref="REDACTION_PLACEHOLDER"/> in its place. A structured-log
/// destructure or an interpolation of this record prints the placeholder too.
/// </para>
/// <para>
/// The token stays on a property rather than moving behind a reveal method,
/// which is the form a credential crossing a cookie boundary otherwise takes.
/// A forgery token is not a credential: it authenticates nobody on its own, it
/// is bound to one already-resolved session, and it is delivered on purpose to
/// page script in a cookie that is not <c>HttpOnly</c>. Its protection is that
/// a cross-site document cannot read it, which no rendering of this record
/// changes; a serialization of the record exposes the token to the same
/// page the cookie already delivered it to. The redaction attribute and the
/// <see cref="ToString"/> override stay as defense in depth for the log
/// pipeline.
/// </para>
/// </remarks>
/// <param name="Value">The sealed token, as it goes on the wire.</param>
/// <param name="ExpiresAt">
/// The absolute expiry of the session the token is bound to, or <c>null</c>
/// for a session cookie.
/// </param>
public sealed record D2ForgeryTokenIssue(
    [property: RedactData(Reason = RedactReason.SecretInformation)] string Value,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// The constant every string conversion of this record renders in place of
    /// the token.
    /// </summary>
    public const string REDACTION_PLACEHOLDER = "[redacted-forgery-token]";

    /// <inheritdoc />
    public override string ToString() =>
        $"{nameof(D2ForgeryTokenIssue)} {{ {nameof(Value)} = {REDACTION_PLACEHOLDER}, "
        + $"{nameof(ExpiresAt)} = {ExpiresAt} }}";
}
