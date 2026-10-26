// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;

using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.AspNetCore.Forgery;
using DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.Routing;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using global::Microsoft.Extensions.Logging;

/// <summary>
/// Builds a test host whose pipeline runs the forgery check after routing,
/// with one endpoint per posture the middleware classifies on.
/// </summary>
/// <remarks>
/// Each endpoint responds with <see cref="ENDPOINT_BODY"/>, so a response body
/// carrying it is the proof the request reached past the check and a refusal
/// body is the proof it did not.
/// </remarks>
internal static class ForgeryProtectionTestHostBuilder
{
    /// <summary>An endpoint declaring the <c>required</c> posture.</summary>
    internal const string REQUIRED_PATH = "/forgery/required";

    /// <summary>An endpoint declaring the <c>metadata</c> posture.</summary>
    internal const string METADATA_PATH = "/forgery/metadata";

    /// <summary>An endpoint declaring the <c>exempt</c> posture.</summary>
    internal const string EXEMPT_PATH = "/forgery/exempt";

    /// <summary>An endpoint carrying no posture declaration at all.</summary>
    internal const string UNDECLARED_PATH = "/forgery/undeclared";

    /// <summary>An endpoint declaring a posture outside the closed set.</summary>
    internal const string UNKNOWN_POSTURE_PATH = "/forgery/unknown-posture";

    /// <summary>A path no endpoint is mapped at.</summary>
    internal const string UNMAPPED_PATH = "/forgery/nowhere";

    /// <summary>The body every mapped endpoint responds with.</summary>
    internal const string ENDPOINT_BODY = "reached";

    /// <summary>A posture value outside the closed set.</summary>
    internal const string UNKNOWN_POSTURE = "banana";

    /// <summary>The origin every request in these tests is addressed to.</summary>
    internal const string ORIGIN = "https://localhost";

    /// <summary>
    /// Builds one request, setting only the headers the caller supplies, so a
    /// test states the header state it is asserting a disposition for.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path under <see cref="ORIGIN"/>.</param>
    /// <param name="secFetchSite">The fetch-metadata header value, or null to omit it.</param>
    /// <param name="origin">The origin header value, or null to omit it.</param>
    /// <param name="tokenHeader">The token header value, or null to omit it.</param>
    /// <param name="tokenCookie">The token cookie value, or null to omit it.</param>
    /// <param name="authorization">The authorization header value, or null to omit it.</param>
    /// <param name="tokenHeaderName">The token header name to write.</param>
    /// <param name="tokenCookieName">The token cookie name to write.</param>
    /// <returns>The request.</returns>
    internal static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        string? secFetchSite = null,
        string? origin = null,
        string? tokenHeader = null,
        string? tokenCookie = null,
        string? authorization = null,
        string tokenHeaderName = D2ForgeryProtectionOptions.DEFAULT_TOKEN_HEADER_NAME,
        string tokenCookieName = D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME)
    {
        var request = new HttpRequestMessage(method, ORIGIN + path);

        if (secFetchSite is not null)
        {
            request.Headers.TryAddWithoutValidation(
                D2ForgeryHeaderNames.SEC_FETCH_SITE, secFetchSite);
        }

        if (origin is not null)
            request.Headers.TryAddWithoutValidation(D2ForgeryHeaderNames.ORIGIN, origin);

        if (tokenHeader is not null)
            request.Headers.TryAddWithoutValidation(tokenHeaderName, tokenHeader);

        if (tokenCookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", $"{tokenCookieName}={tokenCookie}");

        if (authorization is not null)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        return request;
    }

    /// <summary>
    /// Builds and starts a host running the forgery check.
    /// </summary>
    /// <param name="verifier">
    /// The token verifier to register, or null to register none.
    /// </param>
    /// <param name="registerForgeryProtection">
    /// False leaves <c>AddD2ForgeryProtection</c> uncalled, so the middleware
    /// finds no request-scoped state.
    /// </param>
    /// <param name="registerProblemDetails">
    /// False leaves the host with no problem-details pipeline, so the
    /// middleware writes the refusal body itself.
    /// </param>
    /// <param name="configure">Optional forgery-options customizer.</param>
    /// <param name="loggerProvider">Optional log-capturing provider.</param>
    /// <returns>The started host - the caller disposes it.</returns>
    internal static Task<IHost> BuildAsync(
        ID2ForgeryTokenVerifier? verifier = null,
        bool registerForgeryProtection = true,
        bool registerProblemDetails = true,
        Action<D2ForgeryProtectionOptions>? configure = null,
        ILoggerProvider? loggerProvider = null) =>
        AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                if (loggerProvider is not null)
                {
                    services.AddLogging(logging =>
                    {
                        // The forgery check writes one Debug line, so a capture
                        // that stopped at Information would see nothing of it.
                        logging.SetMinimumLevel(LogLevel.Debug);
                        logging.AddProvider(loggerProvider);
                    });
                }

                if (registerProblemDetails)
                    services.AddD2ProblemDetails();

                if (registerForgeryProtection)
                {
                    services.AddD2ForgeryProtection(
                        new ConfigurationBuilder().Build(),
                        configure);
                }

                if (verifier is not null)
                    services.AddSingleton(verifier);
            },
            extraConfigure: app => app.UseD2ForgeryProtection(),
            extraEndpoints: MapPostureEndpoints);

    private static void MapPostureEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapAllMethods(endpoints, REQUIRED_PATH, D2ForgeryPostures.REQUIRED);
        MapAllMethods(endpoints, METADATA_PATH, D2ForgeryPostures.METADATA);
        MapAllMethods(endpoints, EXEMPT_PATH, D2ForgeryPostures.EXEMPT);
        MapAllMethods(endpoints, UNKNOWN_POSTURE_PATH, UNKNOWN_POSTURE);
        MapAllMethods(endpoints, UNDECLARED_PATH, posture: null);
    }

    private static void MapAllMethods(
        IEndpointRouteBuilder endpoints,
        string path,
        string? posture)
    {
        var builder = endpoints.MapMethods(
            path,
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
            () => Results.Text(ENDPOINT_BODY));

        if (posture is not null)
            builder.WithMetadata(new ForgeryPostureDeclarationFixture(posture));
    }
}
