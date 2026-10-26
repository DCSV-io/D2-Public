// -----------------------------------------------------------------------
// <copyright file="SystemRequestContextBootstrap.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// The in-host system-worker establishment boundary. It establishes a fresh
/// <see cref="RequestOrigin.System"/> context on a worker scope's
/// <see cref="MutableRequestContext"/>, for a background service with no inbound user
/// request. The identity is the host's own service id, and the call-path starts with
/// a single <see cref="CallPathKind.System"/> entry. A System context has least
/// privilege: it holds the host identity for audit and telemetry and grants no
/// signing authority.
/// </summary>
public static class SystemRequestContextBootstrap
{
    /// <param name="scopedServices">The worker's per-iteration DI scope.</param>
    extension(IServiceProvider scopedServices)
    {
        /// <summary>
        /// Sets <see cref="RequestOrigin.System"/>, the host's own identity and a
        /// fresh single-entry System call-path on the scope's
        /// <see cref="MutableRequestContext"/>. The scope must register a
        /// <see cref="MutableRequestContext"/>. When the scope has a logging container
        /// registered, the method also logs the established call-path's entry count at
        /// Debug level, as the cross-process interceptor's establishment log does. A
        /// scope built without a logging container (for example a minimal test
        /// <see cref="IServiceProvider"/>) establishes the plane without logging and
        /// does not throw.
        /// </summary>
        /// <param name="hostServiceId">The host's own workload service id.</param>
        /// <param name="clock">The clock used to timestamp the System call-path entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="scopedServices"/> or <paramref name="clock"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="hostServiceId"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the scope does not register a <see cref="MutableRequestContext"/>.
        /// </exception>
        public void EstablishSystemContext(string hostServiceId, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(scopedServices);
            ArgumentNullException.ThrowIfNull(clock);
            hostServiceId.ThrowIfFalsey();

            var ctx = scopedServices.GetRequiredService<MutableRequestContext>();
            var now = clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.Origin = RequestOrigin.System;
            ctx.ImmediateCaller = hostServiceId;
            ctx.CallPath = CallPathOps.Append(null, hostServiceId, CallPathKind.System, now);

            // Best effort: a scope without a registered logging container (for example
            // a minimal test IServiceProvider) still establishes the plane, and only
            // the trace is skipped. The logger uses a string category and not
            // ILogger<T>, because this class is static. The category equals what
            // ILogger<SystemRequestContextBootstrap> would emit, as the cross-process
            // interceptor's establishment log does.
            var logger = scopedServices.GetService<ILoggerFactory>()?
                .CreateLogger("DcsvIo.D2.Context.Abstractions.SystemRequestContextBootstrap");
            logger?.SystemContextEstablished(ctx.CallPath.Count, hostServiceId);
        }
    }
}
