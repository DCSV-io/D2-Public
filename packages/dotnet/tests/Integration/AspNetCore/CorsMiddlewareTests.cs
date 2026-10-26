// -----------------------------------------------------------------------
// <copyright file="CorsMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using System.Net.Http;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Headers.Http;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Hosting;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using global::Microsoft.Extensions.Options;
using Xunit;

public sealed class CorsMiddlewareTests
{
    [Fact]
    public async Task PreflightFromAllowedOrigin_ReturnsAccessControlAllowOrigin()
    {
        using var host = await BuildHostAsync(["https://app1.example.com"]);

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/echo");
        request.Headers.Add("Origin", "https://app1.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("https://app1.example.com");
    }

    [Fact]
    public async Task PreflightFromDisallowedOrigin_DoesNotIncludeAllowOriginHeader()
    {
        using var host = await BuildHostAsync(["https://app1.example.com"]);

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/echo");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task GetFromAllowedOrigin_ReturnsAccessControlAllowOrigin()
    {
        using var host = await BuildHostAsync(["https://app1.example.com"]);

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/echo");
        request.Headers.Add("Origin", "https://app1.example.com");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("https://app1.example.com");
    }

    [Fact]
    public async Task PreflightWithCorrelationIdRequestHeader_EchoedInAllowHeaders()
    {
        using var host = await BuildHostAsync(["https://app1.example.com"]);

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/echo");
        request.Headers.Add("Origin", "https://app1.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            HttpHeaders.CORRELATION_ID);

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Headers").Should().BeTrue();
        var allowed = response.Headers.GetValues("Access-Control-Allow-Headers")
            .First()
            .Split(',', StringSplitOptions.TrimEntries);
        allowed.Should().Contain(HttpHeaders.CORRELATION_ID);
    }

    [Fact]
    public async Task EmptyOriginsList_HostBuildFails_ValidateOnStart()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddD2Cors(new ConfigurationBuilder().Build());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Cors();
                    });
            });

        var act = async () => await hostBuilder.StartAsync();
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task AuthorizationInAllowedHeadersWithCredentials_HostBuildFails()
    {
        var act = async () => await StartWithOptionsAsync(o =>
        {
            o.Origins = ["https://app1.example.com"];
            o.AllowedHeaders = ["Content-Type", HttpHeaders.AUTHORIZATION];
            o.AllowCredentials = true;
        });

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainSingle()
            .Which.Should().Contain(HttpHeaders.AUTHORIZATION);
    }

    [Fact]
    public async Task AuthorizationInAllowedHeadersWithoutCredentials_HostBuildSucceeds()
    {
        using var host = await StartWithOptionsAsync(o =>
        {
            o.Origins = ["https://app1.example.com"];
            o.AllowedHeaders = ["Content-Type", HttpHeaders.AUTHORIZATION];
            o.AllowCredentials = false;
        });

        host.Services.GetRequiredService<IOptions<D2CorsOptions>>()
            .Value.AllowedHeaders.Should().Contain(HttpHeaders.AUTHORIZATION);
    }

    [Fact]
    public async Task DefaultAllowedHeadersWithCredentials_HostBuildSucceeds()
    {
        using var host = await StartWithOptionsAsync(
            o => o.Origins = ["https://app1.example.com"]);

        var resolved = host.Services
            .GetRequiredService<IOptions<D2CorsOptions>>()
            .Value;

        resolved.AllowCredentials.Should().BeTrue();
        resolved.AllowedHeaders.Should().NotContain(HttpHeaders.AUTHORIZATION);
    }

    [Fact]
    public async Task IndexedEnvVarBinding_TwoOriginsResolvedCorrectly()
    {
        using var host = await BuildHostAsync(
            origins: null,
            inMemoryCfg: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://a.example.com",
                ["D2_CORS_ORIGINS:1"] = "https://b.example.com",
            });

        var resolved = host.Services
            .GetRequiredService<IOptions<D2CorsOptions>>()
            .Value;
        resolved.Origins.Should().BeEquivalentTo(
            ["https://a.example.com", "https://b.example.com"]);
    }

    [Fact]
    public async Task StartupRejectsWildcardHeaders_CredentialsAreEnabledAsync()
    {
        var start = async () =>
        {
            using var host = await StartWithOptionsAsync(options =>
            {
                options.Origins = ["https://app1.example.com"];
                options.AllowedHeaders = ["*"];
                options.AllowCredentials = true;
            });
        };

        await start.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task RejectsCredentialedAuthorizationPreflight_WildcardHeaderConfiguredAsync()
    {
        IHost host;

        try
        {
            host = await StartWithOptionsAsync(options =>
            {
                options.Origins = ["https://app1.example.com"];
                options.AllowedHeaders = ["*"];
                options.AllowedMethods = ["POST"];
                options.AllowCredentials = true;
            });
        }
        catch (OptionsValidationException exception)
        {
            exception.Failures.Should().ContainSingle().Which.Should().Contain("AllowedHeaders");
            return;
        }

        using (host)
        using (var client = host.GetTestClient())
        using (var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/echo"))
        {
            request.Headers.Add("Origin", "https://app1.example.com");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", HttpHeaders.AUTHORIZATION);

            using var response = await client.SendAsync(request);
            var allowed = response.Headers.GetValues("Access-Control-Allow-Headers")
                .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries));

            var credentialedAuthorization =
                response.Headers.Contains("Access-Control-Allow-Credentials")
                && allowed.Contains(HttpHeaders.AUTHORIZATION, StringComparer.OrdinalIgnoreCase);

            credentialedAuthorization.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(true, "Content-Type", false)]
    [InlineData(false, "*", true)]
    [InlineData(false, "Authorization", true)]
    public async Task PreflightEnforcesAuthorizationAllowance_WithConfiguredCredentialsAsync(
        bool credentials,
        string allowedHeader,
        bool authorizationAllowed)
    {
        using var host = await StartWithOptionsAsync(options =>
        {
            options.Origins = ["https://app1.example.com"];
            options.AllowedHeaders = [allowedHeader];
            options.AllowedMethods = ["POST"];
            options.AllowCredentials = credentials;
        });

        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/echo");
        request.Headers.Add("Origin", "https://app1.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", HttpHeaders.AUTHORIZATION);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().Equal("https://app1.example.com");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().Be(credentials);

        var allowed = response.Headers.GetValues("Access-Control-Allow-Headers")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries));

        allowed.Contains(HttpHeaders.AUTHORIZATION, StringComparer.OrdinalIgnoreCase)
            .Should().Be(authorizationAllowed);
    }

    private static async Task<IHost> StartWithOptionsAsync(
        Action<D2CorsOptions> configure)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddD2Cors(
                            new ConfigurationBuilder().Build(),
                            configure);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Cors();
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static async Task<IHost> BuildHostAsync(
        IReadOnlyList<string>? origins,
        IDictionary<string, string?>? inMemoryCfg = null)
    {
        return await AspNetCoreTestHostBuilder.BuildAsync(
            configuration: inMemoryCfg,
            extraServices: services =>
            {
                var cfg = inMemoryCfg is { Count: > 0 }
                    ? new ConfigurationBuilder().AddInMemoryCollection(inMemoryCfg).Build()
                    : new ConfigurationBuilder().Build();

                services.AddD2Cors(cfg, opts =>
                {
                    if (origins is { Count: > 0 })
                        opts.Origins = origins;
                });
            },
            extraConfigure: app => app.UseD2Cors());
    }
}
