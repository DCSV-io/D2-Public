// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionRefusalTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// The refusal path: one forgery code whatever the reason, a re-issued token
/// cookie when a session resolved, the operator record, and the fail-closed
/// behavior of a host that registered no verifier.
/// </summary>
public sealed class ForgeryProtectionRefusalTests
{
    private const string _BOUND_TOKEN = "bound-token";
    private const string _OTHER_TOKEN = "some-other-token";
    private const string _FRESH_TOKEN = "freshly-sealed-token";

    [Fact]
    public async Task Refusal_WithAProblemDetailsPipeline_CarriesTheForgeryCode()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(AlwaysStale());

        var response = await RefuseAsync(host);

        await ShouldBeForgeryProblemAsync(response);
    }

    [Fact]
    public async Task Refusal_WithNoProblemDetailsPipeline_CarriesTheForgeryCode()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            AlwaysStale(),
            registerProblemDetails: false);

        var response = await RefuseAsync(host);

        await ShouldBeForgeryProblemAsync(response);
    }

    [Fact]
    public async Task Refusal_WithAResolvedSession_ReissuesTheTokenCookie()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(Reissuing());

        var response = await RefuseAsync(host);

        response.Headers.GetValues("Set-Cookie").Single().Should().StartWith(
            $"{D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME}={_FRESH_TOKEN}");
    }

    [Fact]
    public async Task Refusal_TokenCookie_CarriesTheScriptReadableHostPrefixedAttributes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(Reissuing());

        var response = await RefuseAsync(host);

        var attributes = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        attributes.Should().Contain("path=/");
        attributes.Should().Contain("secure");
        attributes.Should().Contain("samesite=lax");
        attributes.Should().NotContain("httponly");
    }

    [Fact]
    public async Task Refusal_WhenTheIssuerAnswersNull_WritesNoCookie()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(AlwaysStale());

        var response = await RefuseAsync(host);

        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Refusal_WhenTheIssuerAnswersNull_RecordsThatNoTokenWasIssued()
    {
        using var logs = new RecordingLoggerProviderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            AlwaysStale(),
            loggerProvider: logs);

        await RefuseAsync(host);

        logs.Lines.Should().ContainSingle(line => line.EventId == 9571);
    }

    [Fact]
    public async Task Admission_WithNoResolvedSession_RecordsThatTheTokenHalfWasSkipped()
    {
        // The arm records the pass-through: a host whose pipeline places this
        // stage ahead of session resolution reads NoSession for every request,
        // and nothing else tells that deployment apart from a healthy one.
        using var logs = new RecordingLoggerProviderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.NoSession),
            loggerProvider: logs);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        logs.Lines.Should().ContainSingle(line => line.EventId == 9572);
    }

    [Fact]
    public async Task Admission_WithAResolvedSessionAndABoundToken_RecordsNoSkip()
    {
        // The other side of the same arm: a request that carried the token half
        // must not be counted as one that skipped it.
        using var logs = new RecordingLoggerProviderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(static _ => D2ForgeryTokenVerdict.Bound),
            loggerProvider: logs);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        logs.Lines.Should().NotContain(line => line.EventId == 9572);
    }

    [Fact]
    public async Task Refusal_WithNoResolvedSession_AsksTheIssuerForNothing()
    {
        var verifier = new ForgeryTokenVerifierFixture(
            static _ => D2ForgeryTokenVerdict.NoSession);
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);

        // A metadata-posture route refuses on its headers alone, so the request
        // is refused with the verdict still NoSession.
        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.METADATA_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        verifier.IssueCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Refusal_LogLine_CarriesTheRouteAndReasonAndNoPresentedValue()
    {
        using var logs = new RecordingLoggerProviderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            AlwaysStale(),
            loggerProvider: logs);

        await RefuseAsync(host);

        var refusal = logs.Lines.Single(line => line.EventId == 9570).Message;
        refusal.Should().Contain(ForgeryProtectionTestHostBuilder.REQUIRED_PATH);
        refusal.Should().Contain(D2ForgeryRefusalReasons.TOKEN_STALE);
        refusal.Should().NotContain(_OTHER_TOKEN);
    }

    [Fact]
    public async Task Refusal_RecordsAMeasurementTaggedWithItsReason()
    {
        using var meter = new ForgeryRefusalMeterRecorderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(AlwaysStale());

        await RefuseAsync(host);

        meter.Reasons.Should().Contain(D2ForgeryRefusalReasons.TOKEN_STALE);
    }

    [Fact]
    public async Task NoVerifierRegistered_CookieBorneRequiredRequest_Refuses()
    {
        using var logs = new RecordingLoggerProviderFixture();
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            verifier: null,
            loggerProvider: logs);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                tokenHeader: _BOUND_TOKEN));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        logs.Lines.Single(line => line.EventId == 9570).Message
            .Should().Contain(D2ForgeryRefusalReasons.VERIFIER_ABSENT);
    }

    [Fact]
    public async Task NoVerifierRegistered_BearerRequest_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier: null);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE,
                authorization: "Bearer some-token"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoVerifierRegistered_MetadataRouteSameOrigin_Passes()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier: null);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.METADATA_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoVerifierRegistered_MetadataRouteCrossSite_Refuses()
    {
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier: null);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.METADATA_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OversizedToken_ReachesTheVerifierWholeAndRefuses()
    {
        var oversized = new string('t', 64 * 1024);
        var verifier = new ForgeryTokenVerifierFixture(
            static _ => D2ForgeryTokenVerdict.Forged);
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                tokenHeader: oversized));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        verifier.PresentedTokens.Should().Equal(oversized);
    }

    [Fact]
    public async Task TokenCookieValueCarryingAnEqualsSign_ReachesTheVerifierWhole()
    {
        var verifier = new ForgeryTokenVerifierFixture(
            static _ => D2ForgeryTokenVerdict.Absent);
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);

        await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.METADATA_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                tokenCookie: "left=right"));

        verifier.PresentedTokens.Should().Equal("left=right");
    }

    [Fact]
    public async Task ConfiguredNames_AreTheNamesReadAndWritten()
    {
        const string cookieName = "__Host-alt_csrf";
        const string headerName = "X-Alt-Csrf-Token";
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(
            new ForgeryTokenVerifierFixture(
                static _ => D2ForgeryTokenVerdict.Stale,
                static () => new D2ForgeryTokenIssue(_FRESH_TOKEN, null)),
            configure: options =>
            {
                options.TokenCookieName = cookieName;
                options.TokenHeaderName = headerName;
            });

        var response = await host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                tokenHeader: _OTHER_TOKEN,
                tokenHeaderName: headerName));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.GetValues("Set-Cookie").Single()
            .Should().StartWith($"{cookieName}={_FRESH_TOKEN}");
    }

    [Fact]
    public async Task ConcurrentRequestsOnOneVerifier_EachTakeTheirOwnVerdict()
    {
        // One shared verifier instance and one shared pipeline, with a token
        // per request that decides that request's response - so a verdict written
        // into another request's state would produce the wrong status.
        var verifier = new ForgeryTokenVerifierFixture(
            static presented => presented is not null && presented.EndsWith('0')
                ? D2ForgeryTokenVerdict.Bound
                : D2ForgeryTokenVerdict.Stale);
        using var host = await ForgeryProtectionTestHostBuilder.BuildAsync(verifier);
        var client = host.GetTestClient();

        var sends = Enumerable.Range(0, 16)
            .Select(i => client.SendAsync(
                ForgeryProtectionTestHostBuilder.Request(
                    HttpMethod.Post,
                    ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                    secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                    tokenHeader: $"token-{i % 2}")))
            .ToArray();

        var responses = await Task.WhenAll(sends);

        for (var i = 0; i < responses.Length; i++)
        {
            responses[i].StatusCode.Should().Be(
                i % 2 == 0 ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                because: $"request {i} presented token-{i % 2}");
        }
    }

    private static ForgeryTokenVerifierFixture AlwaysStale() =>
        new(static _ => D2ForgeryTokenVerdict.Stale);

    private static ForgeryTokenVerifierFixture Reissuing() =>
        new(
            static _ => D2ForgeryTokenVerdict.Stale,
            static () => new D2ForgeryTokenIssue(_FRESH_TOKEN, null));

    private static Task<HttpResponseMessage> RefuseAsync(IHost host) =>
        host.GetTestClient().SendAsync(
            ForgeryProtectionTestHostBuilder.Request(
                HttpMethod.Post,
                ForgeryProtectionTestHostBuilder.REQUIRED_PATH,
                secFetchSite: D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN,
                tokenHeader: _OTHER_TOKEN));

    private static async Task ShouldBeForgeryProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be(D2ProblemDetailsKeys.CONTENT_TYPE);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]!.GetValue<string>()
            .Should().Be(ErrorCodes.GEN_FORGERY_REFUSED);
        body["status"]!.GetValue<int>().Should().Be((int)HttpStatusCode.Forbidden);
    }
}
