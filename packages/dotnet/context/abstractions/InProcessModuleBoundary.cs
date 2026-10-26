// -----------------------------------------------------------------------
// <copyright file="InProcessModuleBoundary.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// The in-host establishment boundary. It establishes the in-process-module plane
/// on the current scoped request-context before an in-host leaf call (the
/// generated <c>I&lt;Module&gt;Api</c> seam). It sets
/// <see cref="RequestOrigin.InProcessModule"/>, records the calling module id as the
/// telemetry-grade <see cref="IRequestContext.ImmediateCaller"/>, and appends a
/// <see cref="CallPathKind.ModuleHop"/> entry. The validated request-context is
/// passed directly, with no serialization and no wire, so there is no spoofing
/// surface; the whole process is trusted.
/// </summary>
public static class InProcessModuleBoundary
{
    /// <param name="context">The current scoped request-context.</param>
    extension(IRequestContext context)
    {
        /// <summary>
        /// Establishes the in-process-module plane on the current scoped context before
        /// delegating to an in-host leaf: <see cref="RequestOrigin.InProcessModule"/>,
        /// <see cref="IRequestContext.ImmediateCaller"/> = the calling module id
        /// (telemetry-grade), and a <see cref="CallPathKind.ModuleHop"/> appended for
        /// <paramref name="targetModuleId"/>. Only a mutable context is changed, so a
        /// read-only test double is left untouched.
        /// </summary>
        /// <param name="callingModuleId">The id of the module/host making the
        /// in-process call.</param>
        /// <param name="targetModuleId">The id of the in-host module being entered
        /// (the hop's own identity).</param>
        /// <param name="clock">The clock used to timestamp the appended hop.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="clock"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="callingModuleId"/> or <paramref name="targetModuleId"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <remarks>
        /// <strong>The hop is recorded on the call path, not in a log (intentional
        /// difference)</strong>: unlike the
        /// HTTP inbound, System and cross-process establishment boundaries, this extension's
        /// only receiver is <see cref="IRequestContext"/>, so no
        /// <see cref="IServiceProvider"/> or other DI handle is available to resolve a
        /// logger from. Adding one would break the signature of this shipped
        /// <see langword="public"/> API: a new required parameter breaks every existing
        /// call site, and an optional parameter that existing call sites do not pass
        /// would never log. The appended <see cref="CallPathKind.ModuleHop"/> entry on
        /// <see cref="IRequestContext.CallPath"/> records the hop instead.
        /// </remarks>
        public void EstablishInProcessModule(
            string callingModuleId, string targetModuleId, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(clock);
            callingModuleId.ThrowIfFalsey();
            targetModuleId.ThrowIfFalsey();

            if (context is not MutableRequestContext ctx)
                return;

            var now = clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.Origin = RequestOrigin.InProcessModule;
            ctx.ImmediateCaller = callingModuleId;

            ctx.CallPath = CallPathOps.Append(
                ctx.CallPath, targetModuleId, CallPathKind.ModuleHop, now);
        }
    }
}
