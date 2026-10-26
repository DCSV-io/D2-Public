// -----------------------------------------------------------------------
// <copyright file="IAmbientRequestScopeAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Framework-free port that resolves the <see cref="IServiceProvider"/> of the
/// inbound request scope currently executing on the ambient (async-local)
/// execution context. Depending on this port, rather than on a concrete ambient
/// mechanism, keeps an outbound forwarding library free of any web or hosting
/// framework reference.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a port.</b> An outbound forwarding credential must reach the
/// request-scoped forwarded-JWT holder (<see cref="IForwardedJwtAccessor"/>) on
/// each outbound RPC, but a gRPC <c>CallCredentials</c> is a per-channel singleton
/// built outside the DI request-scope ambient flow. The only way one long-lived
/// channel can correctly forward each concurrent request's own token is to
/// re-derive the current request's scope per call from an ambient seam. The
/// established ambient seam in this codebase is <c>IHttpContextAccessor</c>
/// (backed by an <c>AsyncLocal&lt;&gt;</c>), which lives in the AspNetCore
/// framework. Rather than bring that framework into an otherwise framework-free
/// outbound library, the credential depends on this thin port, and a transport
/// library that already references the framework supplies the
/// <c>IHttpContextAccessor</c>-backed adapter.
/// </para>
/// <para>
/// <b>Concurrency contract.</b> An implementation must read the ambient scope of
/// the request executing on the current async flow - never a captured or cached
/// value. Two concurrent inbound requests sharing one outbound channel each
/// observe their own scope (and thus their own holder, and thus their own
/// token). An implementation that captures the scope at construction would
/// forward the first request's token to every later request (a cross-user
/// credential leak) - the exact failure this port exists to prevent.
/// </para>
/// <para>
/// <b>Absent scope.</b> <see cref="Current"/> returns <see langword="null"/> when
/// no ambient request scope is on the execution context - for example a genuinely
/// system-initiated call with no inbound request. A forwarding credential must
/// treat that as a hard failure (no token to forward), not as a send with no
/// header.
/// </para>
/// </remarks>
public interface IAmbientRequestScopeAccessor
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> of the inbound request scope on the
    /// current ambient execution context, or <see langword="null"/> when no such
    /// scope is present (no inbound request is in flight on this async flow).
    /// </summary>
    IServiceProvider? Current { get; }
}
