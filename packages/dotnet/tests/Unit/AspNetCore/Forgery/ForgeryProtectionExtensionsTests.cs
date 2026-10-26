// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Forgery;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// What <c>AddD2ForgeryProtection</c> registers, what its validator rejects,
/// and the argument guards of both extensions.
/// </summary>
public sealed class ForgeryProtectionExtensionsTests
{
    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void AddD2ForgeryProtection_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        IConfiguration configuration = new ConfigurationBuilder().Build();

        var act = () => services!.AddD2ForgeryProtection(configuration);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ForgeryProtection_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2ForgeryProtection(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseD2ForgeryProtection_NullApp_ThrowsArgumentNullException()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2ForgeryProtection();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2ForgeryProtection_RegistersTheOptionsSeam() =>
        ResolveAndDispose(Provider()).Should().NotBeNull();

    [Fact]
    public void AddD2ForgeryProtection_RegistersTheRequestStateSeam()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<D2ForgeryTokenState>()
            .Should().NotBeNull();
    }

    [Fact]
    public void AddD2ForgeryProtection_RegistersTheRequestStateScoped()
    {
        using var provider = Provider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var withinOneScope = first.ServiceProvider.GetRequiredService<D2ForgeryTokenState>();

        withinOneScope.Should().BeSameAs(
            first.ServiceProvider.GetRequiredService<D2ForgeryTokenState>());
        withinOneScope.Should().NotBeSameAs(
            second.ServiceProvider.GetRequiredService<D2ForgeryTokenState>());
    }

    [Fact]
    public void AddD2ForgeryProtection_RegistersNoTokenVerifier()
    {
        using var provider = Provider();

        provider.GetService<ID2ForgeryTokenVerifier>().Should().BeNull();
    }

    [Fact]
    public void AddD2ForgeryProtection_WithNoConfiguration_TakesTheDefaults()
    {
        var options = ResolveAndDispose(Provider());

        options.TokenCookieName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME);
        options.TokenHeaderName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_HEADER_NAME);
    }

    [Fact]
    public void AddD2ForgeryProtection_BindsTheSection()
    {
        var options = ResolveAndDispose(Provider(
            configuration: new Dictionary<string, string?>
            {
                [$"{D2ForgeryProtectionOptions.SECTION}:TokenCookieName"] = "__Host-bound",
                [$"{D2ForgeryProtectionOptions.SECTION}:TokenHeaderName"] = "X-Bound",
            }));

        options.TokenCookieName.Should().Be("__Host-bound");
        options.TokenHeaderName.Should().Be("X-Bound");
    }

    [Fact]
    public void AddD2ForgeryProtection_ConfigureCallback_RunsAfterTheBinding()
    {
        var options = ResolveAndDispose(Provider(
            configuration: new Dictionary<string, string?>
            {
                [$"{D2ForgeryProtectionOptions.SECTION}:TokenCookieName"] = "__Host-bound",
            },
            configure: o => o.TokenCookieName = "__Host-callback"));

        options.TokenCookieName.Should().Be("__Host-callback");
    }

    [Fact]
    public void AddD2ForgeryProtection_AcceptsAHostPrefixedCookieName() =>
        ResolveAndDispose(Provider(configure: o => o.TokenCookieName = "__Host-accepted"))
            .TokenCookieName.Should().Be("__Host-accepted");

    [Fact]
    public void AddD2ForgeryProtection_RefusesACookieNameWithoutTheHostPrefix()
    {
        var provider = Provider(configure: o => o.TokenCookieName = "d2_csrf");

        var act = () => ResolveAndDispose(provider);

        act.Should().Throw<OptionsValidationException>().WithMessage("*__Host-*");
    }

    [Theory]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    public void AddD2ForgeryProtection_RefusesAnEmptyCookieName(string name)
    {
        var provider = Provider(configure: o => o.TokenCookieName = name);

        var act = () => ResolveAndDispose(provider);

        act.Should().Throw<OptionsValidationException>().WithMessage("*TokenCookieName*");
    }

    [Theory]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    public void AddD2ForgeryProtection_RefusesAnEmptyHeaderName(string name)
    {
        var provider = Provider(configure: o => o.TokenHeaderName = name);

        var act = () => ResolveAndDispose(provider);

        act.Should().Throw<OptionsValidationException>().WithMessage("*TokenHeaderName*");
    }

    [Fact]
    public void AddD2ForgeryProtection_AcceptsANonDefaultHeaderName() =>
        ResolveAndDispose(Provider(configure: o => o.TokenHeaderName = "X-Accepted"))
            .TokenHeaderName.Should().Be("X-Accepted");

    private static ServiceProvider Provider(
        IDictionary<string, string?>? configuration = null,
        Action<D2ForgeryProtectionOptions>? configure = null)
    {
        var builder = new ConfigurationBuilder();

        if (configuration is not null)
            builder.AddInMemoryCollection(configuration);

        var services = new ServiceCollection();
        services.AddD2ForgeryProtection(builder.Build(), configure);

        return services.BuildServiceProvider();
    }

    private static D2ForgeryProtectionOptions ResolveAndDispose(ServiceProvider provider)
    {
        using (provider)
        {
            return provider.GetRequiredService<IOptions<D2ForgeryProtectionOptions>>().Value;
        }
    }
}
