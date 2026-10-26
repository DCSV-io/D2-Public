// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionApplicationBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// ASP.NET Core middleware extension installing the forgery check.
/// </summary>
public static class ForgeryProtectionApplicationBuilderExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the forgery check. Place it after <c>UseRouting()</c>, so
        /// the routed endpoint's
        /// <see cref="ID2ForgeryPostureDeclaration"/> is resolvable.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The check runs once per request, at the earliest place a pipeline
        /// installs it. The middleware marks the request on entry and a later
        /// installation passes it through, so a host that composes an
        /// aggregate pipeline helper installing this stage and then places the
        /// stage itself runs the composed one, wherever that helper put it.
        /// Where the composed placement sits ahead of the stage that resolves
        /// the session, the check reads
        /// <see cref="D2ForgeryTokenVerdict.NoSession"/> for every request and
        /// admits every required-posture mutation without the token half.
        /// Install the stage once, behind the stage that resolves the session.
        /// </para>
        /// <para>
        /// <see cref="ForgeryProtectionServiceCollectionExtensions.AddD2ForgeryProtection"/>
        /// must have run on the service collection. Without it the middleware
        /// throws on the first request rather than passing it through, because
        /// passing it through would leave the check open by default.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2ForgeryProtection()
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<ForgeryProtectionMiddleware>();
        }
    }
}
