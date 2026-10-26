// -----------------------------------------------------------------------
// <copyright file="SessionCredentialNeverLoggedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging;

using System.Globalization;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Logging.Internal;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

/// <summary>
/// End-to-end guard that a raw session id never reaches a log sink.
/// </summary>
/// <remarks>
/// <para>
/// A session id does not authenticate a request - the session cookie carries an encrypted
/// frame rather than the id - but it is the handle every record about one session shares.
/// A log line that contains it lets anyone with log-read access join that session across
/// every log and across the session cache, tie it to one person, and identify that
/// person's session to anything able to end it. These tests assert that the raw
/// identifier appears NOWHERE in the emitted event, in any textual form, rather than
/// merely that some particular property name is absent.
/// </para>
/// <para>
/// Fails without the fix: before the enricher switched to
/// <see cref="LogSafeIdentifiers.SessionDigest"/>, the request-completion event carried
/// <c>SessionId</c> verbatim, and <c>ActorEntry.SessionId</c> destructured verbatim inside
/// <c>ActorChain</c>.
/// </para>
/// </remarks>
[Collection("LogLoggerStaticState")]
public sealed class SessionCredentialNeverLoggedTests
{
    private static readonly Guid sr_sessionId =
        Guid.Parse("7f3a1c2e-9b41-4d6a-8e05-2c7d1f4b6a90");

    private static readonly Guid sr_impersonationSessionId =
        Guid.Parse("1d9e4b70-3a25-4f88-9c31-6e0a7b2d5c14");

    private static readonly Guid sr_actorSessionId =
        Guid.Parse("5c2b8d16-7e40-4a93-b6f2-0d3a9c81e75b");

    [Fact]
    public async Task RequestEvent_CarriesNoRawSessionIdInAnyTextualForm()
    {
        var stub = BuildStub();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services => services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;

        await host.GetTestClient().GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();

        var rendered = RenderEverything(requestEvent);

        // Every spelling a reader of the logs could grep for and correlate on.
        var sessionIds = new[] { sr_sessionId, sr_impersonationSessionId, sr_actorSessionId };

        foreach (var sessionId in sessionIds)
        {
            foreach (var format in new[] { "N", "D", "B", "P", "X" })
            {
                rendered.Should().NotContain(
                    sessionId.ToString(format, CultureInfo.InvariantCulture),
                    $"a raw session id in '{format}' form names the session itself");
            }
        }
    }

    [Fact]
    public async Task RequestEvent_CarriesStableCorrelationDigestsInstead()
    {
        var stub = BuildStub();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services => services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;

        await host.GetTestClient().GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();

        // Operators keep a correlation key, which is a digest and not the id itself.
        requestEvent.Properties.Should().ContainKey(LogSafeIdentifiers.SESSION_ID_HASH);
        requestEvent.Properties[LogSafeIdentifiers.SESSION_ID_HASH].ToString()
            .Should().Contain(LogSafeIdentifiers.SessionDigest(sr_sessionId));

        requestEvent.Properties.Should()
            .ContainKey(LogSafeIdentifiers.IMPERSONATION_SESSION_ID_HASH);
        requestEvent.Properties[LogSafeIdentifiers.IMPERSONATION_SESSION_ID_HASH].ToString()
            .Should().Contain(LogSafeIdentifiers.SessionDigest(sr_impersonationSessionId));
    }

    [Fact]
    public async Task ActorChain_EmitsDigestPerEntry_AndKeepsTheRestReadable()
    {
        var stub = BuildStub();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services => services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;

        await host.GetTestClient().GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.ActorChain));

        var chain = requestEvent.Properties[nameof(IRequestContext.ActorChain)].ToString();

        // Serilog binds these sequence elements via ToString(), so a record carrying the raw
        // id would print it in full. The projection replaces the id with a digest,
        // and the remaining fields stay legible for "who acted as whom".
        chain.Should().Contain(LogSafeIdentifiers.SessionDigest(sr_actorSessionId));
        chain.Should().Contain("agent-1");
        chain.Should().Contain("Impersonation");
    }

    private static string RenderEverything(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        logEvent.RenderMessage(writer, CultureInfo.InvariantCulture);

        foreach (var property in logEvent.Properties)
        {
            writer.Write(property.Key);
            writer.Write('=');
            writer.Write(property.Value.ToString());
            writer.Write(';');
        }

        return writer.ToString();
    }

    private static LogEvent? FindRequestEvent(InMemorySink sink) =>
        sink.Events.FirstOrDefault(e =>
            e.Properties.TryGetValue("SourceContext", out var sc)
            && sc.ToString().Contains(
                "Serilog.AspNetCore.RequestLoggingMiddleware",
                StringComparison.Ordinal));

    private static StubRequestContext BuildStub() => new()
    {
        TraceId = "trace-sec1",
        RequestId = "req-sec1",
        RequestPath = "/api/echo",
        IsAuthenticated = true,
        SessionId = sr_sessionId,
        IsImpersonating = true,
        ImpersonationKind = ImpersonationKind.Consent,
        ImpersonationSessionId = sr_impersonationSessionId,
        ActorChain =
        [
            new ActorEntry(
                ActorKind.Impersonation,
                "agent-1",
                ImpersonationKind: ImpersonationKind.Consent,
                SessionId: sr_actorSessionId),
        ],
    };
}
