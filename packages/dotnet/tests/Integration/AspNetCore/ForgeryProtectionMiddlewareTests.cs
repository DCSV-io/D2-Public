// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// The classification and disposition tables of the forgery check, driven
/// through a composed pipeline: which request classes require which half, how
/// the fetch-metadata half treats each header state, and how the token half
/// treats each verdict.
/// </summary>
public sealed class ForgeryProtectionMiddlewareTests
{
    private const string _BOUND_TOKEN = "bound-token";
    private const string _OTHER_TOKEN = "some-other-token";

    /// <summary>
    /// Gets the empty <c>Sec-Fetch-Site</c> row. <see cref="string.Empty"/> is
    /// not a constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string?, string?, bool> EmptySecFetchSiteRow =>
        new() { { string.Empty, null, false } };

    [Fact]
    public async Task SafeMethod_OnRequiredRoute_PassesWithNoMetadataHeaders()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Get,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Fact]
    public async Task BearerRequest_OnRequiredRoute_PassesDespiteCrossSiteAndForgedToken()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            AlwaysForged());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE,
            tokenHeader: _OTHER_TOKEN,
            authorization: "Bearer some-token");

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Fact]
    public async Task ExemptRoute_CrossSiteRequestWithNoToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            AlwaysForged());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.EXEMPT_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Theory]

    // No session resolved, so no token can be bound to one. Every posture that
    // runs the fetch-metadata half still runs it, and only the token half is
    // skipped.
    [InlineData(ForgeryProtectionTestHostBuilder.REQUIRED_PATH, "same-origin", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.REQUIRED_PATH, "cross-site", false)]
    [InlineData(ForgeryProtectionTestHostBuilder.REQUIRED_PATH, null, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNDECLARED_PATH, "same-origin", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNDECLARED_PATH, "cross-site", false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNDECLARED_PATH, null, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH, "same-origin", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH, "cross-site", false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH, null, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.METADATA_PATH, "same-origin", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.METADATA_PATH, "cross-site", false)]
    [InlineData(ForgeryProtectionTestHostBuilder.METADATA_PATH, null, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.EXEMPT_PATH, "same-origin", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.EXEMPT_PATH, "cross-site", true)]
    [InlineData(ForgeryProtectionTestHostBuilder.EXEMPT_PATH, null, true)]
    public async Task NoSessionResolved_EachPosture_TakesTheFetchMetadataDisposition(
        string path,
        string? secFetchSite,
        bool admitted)
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.NoSession));

        var response = await SendAsync(host, HttpMethod.Post, path, secFetchSite: secFetchSite);

        response.StatusCode.Should().Be(
            admitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
    }

    [Theory]

    // A session resolved but the request presents no token, so the token half
    // decides on every posture that runs it, whatever the fetch-metadata half
    // made of a same-origin request.
    [InlineData(ForgeryProtectionTestHostBuilder.REQUIRED_PATH, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNDECLARED_PATH, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH, false)]
    [InlineData(ForgeryProtectionTestHostBuilder.METADATA_PATH, true)]
    [InlineData(ForgeryProtectionTestHostBuilder.EXEMPT_PATH, true)]
    public async Task SessionResolved_EachPosture_TakesTheTokenDisposition(
        string path,
        bool admitted)
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            path,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN);

        response.StatusCode.Should().Be(
            admitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequiredRoute_NoSessionSameOriginNoToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.NoSession));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Fact]
    public async Task BlankAuthorizationHeader_OnRequiredRoute_DoesNotExemptTheRequest()
    {
        // A bearer request is exempt from both halves, so this same request with
        // a real Authorization value reaches the endpoint - the companion test
        // BearerRequest_OnRequiredRoute_PassesDespiteCrossSiteAndForgedToken
        // above pins that. A blank value carries no credential and must not
        // qualify for the exemption.
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(AlwaysForged());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE,
            authorization: " ");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequiredRoute_SameOriginBoundToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Theory]
    [InlineData(D2ForgeryTokenVerdict.Absent)]
    [InlineData(D2ForgeryTokenVerdict.Stale)]
    [InlineData(D2ForgeryTokenVerdict.Forged)]
    [InlineData(D2ForgeryTokenVerdict.Unverifiable)]
    public async Task RequiredRoute_SameOriginWithUnboundToken_Refuses(
        D2ForgeryTokenVerdict verdict)
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(_ => verdict));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _OTHER_TOKEN);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequiredRoute_ReadsTheTokenFromTheHeaderNotTheCookie()
    {
        var verifier = BoundOnly();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenCookie: _BOUND_TOKEN);

        // The browser attaches the cookie to a cross-site request by itself, so
        // a token half that read the cookie would admit a request it must refuse.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MetadataRoute_ReadsTheTokenFromTheCookie()
    {
        var verifier = BoundOnly();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);

        await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.METADATA_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenCookie: _BOUND_TOKEN);

        verifier.PresentedTokens.Should().Equal(_BOUND_TOKEN);
    }

    [Fact]
    public async Task MetadataRoute_SameOriginWithNoToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.Absent));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.METADATA_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Theory]

    // Sec-Fetch-Site decides on its own when it is present.
    [InlineData("same-origin", null, true)]
    [InlineData("none", null, true)]
    [InlineData("same-site", null, false)]
    [InlineData("cross-site", null, false)]
    [InlineData("SAME-ORIGIN", null, false)]
    [InlineData("weird", null, false)]
    [MemberData(nameof(EmptySecFetchSiteRow))]

    // Absent, so Origin is compared against the request's own origin. Scheme
    // and host compare case-insensitively and the scheme's default port
    // normalizes away; every other difference is a mismatch.
    [InlineData(null, "https://localhost", true)]
    [InlineData(null, "https://LOCALHOST", true)]
    [InlineData(null, "HTTPS://localhost", true)]
    [InlineData(null, "https://localhost:443", true)]
    [InlineData(null, "http://localhost", false)]
    [InlineData(null, "http://localhost:80", false)]
    [InlineData(null, "https://localhost:8443", false)]
    [InlineData(null, "https://localhost:", false)]
    [InlineData(null, "https://", false)]
    [InlineData(null, "//localhost", false)]
    [InlineData(null, "https://localhost/path", false)]
    [InlineData(null, "https://evil.example", false)]
    [InlineData(null, "null", false)]
    [InlineData(null, null, false)]
    public async Task MetadataRoute_HeaderState_TakesItsDisposition(
        string? secFetchSite,
        string? origin,
        bool admitted)
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.Absent));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.METADATA_PATH,
            secFetchSite: secFetchSite,
            origin: origin);

        response.StatusCode.Should().Be(
            admitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UndeclaredRoute_CookieBorneWithSession_TakesTheRequiredDisposition()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var refused = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.UNDECLARED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _OTHER_TOKEN);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UndeclaredRoute_CookieBorneWithBoundToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.UNDECLARED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Fact]
    public async Task UnmappedPath_CookieBorneWithSession_RefusesBeforeRouting404()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.UNMAPPED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownPostureValue_WithUnboundToken_TakesTheRequiredDisposition()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _OTHER_TOKEN);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownPostureValue_WithBoundToken_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(BoundOnly());

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.UNKNOWN_POSTURE_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        await ShouldHaveReachedEndpointAsync(response);
    }

    [Fact]
    public async Task TwoRegistrationsInOnePipeline_CheckTheRequestOnce()
    {
        var verifier = BoundOnly();
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                services.AddD2ForgeryProtection(new ConfigurationBuilder().Build());
                services.AddSingleton<ID2ForgeryTokenVerifier>(verifier);
            },
            extraConfigure: app =>
            {
                app.UseD2ForgeryProtection();
                app.UseD2ForgeryProtection();
            },
            extraEndpoints: endpoints =>
                endpoints.MapPost(
                    ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                    () => Results.Text(ForgeryProtectionTestHostBuilder.ENDPOINT_BODY)));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        await ShouldHaveReachedEndpointAsync(response);
        verifier.PresentedTokens.Should().Equal(_BOUND_TOKEN);
    }

    [Fact]
    public async Task TwoRegistrationsInOnePipeline_TheEarliestRuns()
    {
        // A stage installed between the registrations reports whether the request
        // was already checked when it ran. A host placing its own stage behind a
        // composed one gets the composed placement, not its own.
        var verifier = BoundOnly();
        var checkedBeforeTheSecondRegistration = false;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                services.AddD2ForgeryProtection(new ConfigurationBuilder().Build());
                services.AddSingleton<ID2ForgeryTokenVerifier>(verifier);
            },
            extraConfigure: app =>
            {
                app.UseD2ForgeryProtection();
                app.Use(async (context, next) =>
                {
                    checkedBeforeTheSecondRegistration = context.RequestServices
                        .GetRequiredService<D2ForgeryTokenState>().Checked;

                    await next(context);
                });
                app.UseD2ForgeryProtection();
            },
            extraEndpoints: endpoints =>
                endpoints.MapPost(
                    ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                    () => Results.Text(ForgeryProtectionTestHostBuilder.ENDPOINT_BODY)));

        var response = await SendAsync(
            host,
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        await ShouldHaveReachedEndpointAsync(response);
        checkedBeforeTheSecondRegistration.Should().BeTrue();
    }

    [Fact]
    public async Task StateNotRegistered_Throws()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            BoundOnly(),
            registerForgeryProtection: false);
        var client = host.GetTestClient();
        var request = ForgeryProtectionTestHostBuilder.Request(
            HttpMethod.Post,
            ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
            secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
            tokenHeader: _BOUND_TOKEN);

        var send = async () => await client.SendAsync(request);

        await send.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AddD2ForgeryProtection*");
    }

    private static ForgeryTokenVerifierFixture BoundOnly() =>
        new(static presented => presented == _BOUND_TOKEN
            ? D2ForgeryTokenVerdict.Bound
            : D2ForgeryTokenVerdict.Absent);

    private static ForgeryTokenVerifierFixture AlwaysForged() =>
        new(static _ => D2ForgeryTokenVerdict.Forged);

    private static async Task ShouldHaveReachedEndpointAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync())
            .Should().Be(ForgeryProtectionTestHostBuilder.ENDPOINT_BODY);
    }

    private static Task<HttpResponseMessage> SendAsync(
        IHost host,
        HttpMethod method,
        string path,
        string? secFetchSite = null,
        string? origin = null,
        string? tokenHeader = null,
        string? tokenCookie = null,
        string? authorization = null) =>
        host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                method,
                path,
                secFetchSite,
                origin,
                tokenHeader,
                tokenCookie,
                authorization));
}
