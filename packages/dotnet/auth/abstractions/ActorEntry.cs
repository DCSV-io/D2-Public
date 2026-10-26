// -----------------------------------------------------------------------
// <copyright file="ActorEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

using System.Text;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// One link in the RFC 8693 actor chain. Identifies a service or user that
/// participated in producing the current token via token exchange / impersonation.
/// </summary>
/// <param name="Kind">
/// Whether this actor is a service identity or a user impersonation step. The
/// <see cref="ImpersonationKind"/>, <see cref="SessionId"/>, and the
/// <c>OrgId</c>, <c>OrgName</c>, <c>OrgType</c> and <c>OrgRole</c> fields below
/// are only meaningful when <paramref name="Kind"/>
/// is <see cref="ActorKind.Impersonation"/>.
/// </param>
/// <param name="Subject">
/// The actor's identifier (the <c>act.sub</c> claim). For <see cref="ActorKind.Service"/>
/// this is the service's OAuth <c>client_id</c> per RFC 6749 section 4.4. For
/// <see cref="ActorKind.Impersonation"/> this is the agent (impersonator) user id.
/// </param>
/// <param name="ClientId">
/// Optional service client identifier when <paramref name="Kind"/> is
/// <see cref="ActorKind.Service"/>. Often equals <paramref name="Subject"/> for service
/// actors but may differ if the deployed service identifier differs from the registered
/// OAuth client identifier.
/// </param>
/// <param name="ImpersonationKind">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: which flavor
/// of impersonation - <see cref="Auth.Abstractions.ImpersonationKind.Consent"/>
/// (authorized by the impersonated user) or
/// <see cref="Auth.Abstractions.ImpersonationKind.Force"/> (no consent, and not surfaced
/// to the impersonated user). Sourced from the actor's <c>act.d2_kind</c> claim. Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="SessionId">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the impersonation
/// session identifier (<c>act.d2_session_id</c> claim). Distinct from the user's own
/// session id on <c>IAuthContext.SessionId</c>. Null for <see cref="ActorKind.Service"/>
/// entries.
/// <para>
/// <b>Never emit raw.</b> A session id does not authenticate a request - the session
/// cookie holds an encrypted frame rather than the id - but it is the handle every
/// record about one session shares, so a reader of the logs can join that session
/// across them, tie it to one person, and identify that person's session to anything
/// able to end it. It has <c>[RedactData(SecretInformation)]</c>, so structural
/// destructuring (<c>{@x}</c>) masks it, and the record's <see cref="ToString"/>
/// prints it as <c>[REDACTED: SecretInformation]</c>, so a plain <c>{x}</c> capture,
/// an interpolation or a printed actor chain masks it too. Reading the property
/// returns the value; where cross-line correlation is needed, log a non-reversible
/// digest instead of the value.
/// </para>
/// </param>
/// <param name="OrgId">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's
/// (impersonator's) own organization id (<c>act.d2_org_id</c> claim). Useful for audit
/// - "Alice from <i>Customer Support</i> impersonated Bob" - and for additional authz
/// rules that depend on the agent's home org. Null for <see cref="ActorKind.Service"/>
/// entries.
/// </param>
/// <param name="OrgName">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's own
/// organization display name (<c>act.d2_org_name</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="OrgType">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's own
/// organization type (<c>act.d2_org_type</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="OrgRole">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's
/// role in their own organization (<c>act.d2_org_role</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="Act">
/// Optional nested actor entry - RFC 8693 section 2.1 allows <c>act</c> claims to nest
/// (delegation-of-delegation chains). When present, this represents the actor that
/// minted the token <i>this</i> entry's actor consumed before re-delegating onward.
/// </param>
/// <remarks>
/// On the wire (JWT and AMQP headers) the <c>act</c> chain is the RFC 8693 nested-object
/// shape: <c>{ "sub": "...", "act": { "sub": "...", "act": { ... } } }</c>. In code, the
/// outermost chain is exposed on <c>IAuthContext.ActorChain</c> as
/// <c>IReadOnlyList&lt;ActorEntry&gt;</c> so it can be enumerated directly; each entry's
/// <see cref="Act"/> field walks deeper if needed.
/// </remarks>
public sealed record ActorEntry(
    ActorKind Kind,
    string Subject,
    string? ClientId = null,
    ImpersonationKind? ImpersonationKind = null,
    [property: RedactData(Reason = RedactReason.SecretInformation)]
    Guid? SessionId = null,
    Guid? OrgId = null,
    string? OrgName = null,
    OrgType? OrgType = null,
    Role? OrgRole = null,
    ActorEntry? Act = null)
{
    /// <summary>The text <see cref="ToString"/> prints in place of a session id.</summary>
    private const string _SESSION_ID_MASK = "[REDACTED: SecretInformation]";

    /// <summary>
    /// Prints every member as the compiler-written record printer does, with
    /// <see cref="SessionId"/> masked when it holds a value.
    /// </summary>
    /// <param name="builder">The text being built.</param>
    /// <returns><see langword="true"/>, because members were printed.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Kind = ").Append(Kind);
        builder.Append(", Subject = ").Append(Subject);
        builder.Append(", ClientId = ").Append(ClientId);
        builder.Append(", ImpersonationKind = ").Append(ImpersonationKind);
        builder.Append(", SessionId = ").Append(SessionId is null ? null : _SESSION_ID_MASK);
        builder.Append(", OrgId = ").Append(OrgId);
        builder.Append(", OrgName = ").Append(OrgName);
        builder.Append(", OrgType = ").Append(OrgType);
        builder.Append(", OrgRole = ").Append(OrgRole);
        builder.Append(", Act = ").Append(Act);

        return true;
    }
}
