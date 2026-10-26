// -----------------------------------------------------------------------
// <copyright file="RabbitMqSubscriberOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

/// <summary>Checks subscriber duration defaults and composition validation.</summary>
public sealed class RabbitMqSubscriberOptionsTests
{
    [Fact]
    public void Defaults_SharedFixture_MatchesNormalizedDurations()
    {
        using var fixture = DlqLifetimeContractFixture.Load();
        var defaults = fixture.RootElement.GetProperty("defaults");
        var options = new RabbitMqSubscriberOptions();
        options.DlqRepublishTimeout.TotalMilliseconds.Should()
            .Be(defaults.GetProperty("republishMs").GetInt32());
        options.DlqCleanupTimeout.TotalMilliseconds.Should().Be(defaults.GetProperty("cleanupMs").GetInt32());
    }

    [Fact]
    public void Defaults_WithoutOverrides_MatchLifetimeContract()
    {
        var options = new RabbitMqSubscriberOptions();
        options.DlqRepublishTimeout.Should().Be(Duration.FromSeconds(5));
        options.DlqCleanupTimeout.Should().Be(Duration.FromSeconds(1));
        var nullable = new RabbitMqSubscriberOptions(null, null);
        nullable.Should().BeEquivalentTo(options);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2147483647)]
    public void Resolve_WholeMillisecondsAtBounds_Accepts(long milliseconds)
    {
        var duration = Duration.FromMilliseconds(milliseconds);
        using var provider = new ServiceCollection()
            .AddD2MessagingRabbitMq(configureSubscriber: options =>
            {
                options.DlqRepublishTimeout = duration;
                options.DlqCleanupTimeout = duration;
            })
            .BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<RabbitMqSubscriberOptions>>().Value;
        resolved.DlqRepublishTimeout.Should().Be(duration);
        resolved.DlqCleanupTimeout.Should().Be(duration);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(2147483648, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(2147483648, true)]
    public void Resolve_InvalidMilliseconds_Rejects(long milliseconds, bool cleanup)
    {
        using var provider = new ServiceCollection()
            .AddD2MessagingRabbitMq(configureSubscriber: options =>
            {
                if (cleanup) options.DlqCleanupTimeout = Duration.FromMilliseconds(milliseconds);
                else options.DlqRepublishTimeout = Duration.FromMilliseconds(milliseconds);
            })
            .BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqSubscriberOptions>>();
        var resolve = () => options.Value;
        resolve.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_FractionalMillisecond_Rejects(bool cleanup)
    {
        using var provider = new ServiceCollection()
            .AddD2MessagingRabbitMq(configureSubscriber: options =>
            {
                if (cleanup) options.DlqCleanupTimeout = Duration.FromNanoseconds(1_000_001);
                else options.DlqRepublishTimeout = Duration.FromNanoseconds(1_000_001);
            })
            .BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqSubscriberOptions>>();
        var resolve = () => options.Value;
        resolve.Should().Throw<OptionsValidationException>();
    }
}
