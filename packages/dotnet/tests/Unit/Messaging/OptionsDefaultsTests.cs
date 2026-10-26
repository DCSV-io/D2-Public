// -----------------------------------------------------------------------
// <copyright file="OptionsDefaultsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Pins the public defaults on every <c>*Options</c> type. These defaults are
/// part of the contract, so a change to one must be intentional.
/// </summary>
public sealed class OptionsDefaultsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4294967295)]
    public void PublisherOptions_UnboundedConfirmTimeout_FailsValidation(long milliseconds)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configurePublisher: o => o.ConfirmTimeout = TimeSpan.FromMilliseconds(milliseconds));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RabbitMqPublisherOptions>>();
        var resolve = () => options.Value;

        resolve.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4294967294)]
    public void PublisherOptions_BoundedConfirmTimeout_PassesValidation(long milliseconds)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configurePublisher: o => o.ConfirmTimeout = TimeSpan.FromMilliseconds(milliseconds));
        using var provider = services.BuildServiceProvider();

        var configured = provider.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        configured.ConfirmTimeout.Should().Be(TimeSpan.FromMilliseconds(milliseconds));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9999)]
    public void PublisherOptions_PositiveSubMillisecondTimeout_PassesValidation(long ticks)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configurePublisher: o => o.ConfirmTimeout = TimeSpan.FromTicks(ticks));
        using var provider = services.BuildServiceProvider();

        var configured = provider.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        configured.ConfirmTimeout.Should().Be(TimeSpan.FromTicks(ticks));
    }

    [Fact]
    public void RabbitMqConnectionOptions_Defaults()
    {
        var o = new RabbitMqConnectionOptions();
        o.RevealConnectionUri().Should().Be(string.Empty);
        o.AllowUnencryptedTransport.Should().BeFalse();
        o.ClientProvidedName.Should().Be("DcsvIo.D2.Messaging");
        o.ConsumerDispatchConcurrency.Should().BeGreaterThan(0);

        // The formula must follow Environment.ProcessorCount. The test fails if
        // a refactor replaces it with a hardcoded literal such as "16", which
        // would change the CPU scaling.
        var expected = (ushort)Math.Min(ushort.MaxValue, Environment.ProcessorCount);
        o.ConsumerDispatchConcurrency.Should().Be(expected);

        o.InitialReconnectDelay.Should().Be(TimeSpan.FromSeconds(1));
        o.MaxReconnectDelay.Should().Be(TimeSpan.FromSeconds(60));
        o.HealthCheckInterval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ChannelPoolOptions_Defaults()
    {
        var o = new ChannelPoolOptions();
        o.PublishPoolSize.Should().Be(4);
        o.AcquireTimeout.Should().Be(TimeSpan.FromSeconds(30));
        o.PublisherConfirmsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RabbitMqPublisherOptions_Defaults()
    {
        var o = new RabbitMqPublisherOptions();
        o.WaitForConfirm.Should().BeTrue();
        o.ConfirmTimeout.Should().Be(TimeSpan.FromSeconds(5));
        o.MaxAttempts.Should().Be(5);
        o.BaseRetryDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        o.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PublisherOptions_Defaults()
    {
        var o = new PublisherOptions();
        o.WaitForConfirm.Should().BeNull();
        o.ConfirmTimeout.Should().BeNull();
        o.RoutingKey.Should().BeNull();
        o.Exchange.Should().BeNull();
        o.MaxAttempts.Should().BeNull();
    }

    // Per-subscription defaults (queue pattern, prefetch, idempotency,
    // tiered-retry tiers and max attempts) are pinned by the spec entries
    // themselves in contracts/mq-subscriptions/mq-subscriptions.spec.json
    // and DcsvIo.D2.Messaging.SourceGen emits them into
    // MqSubscriptionDescriptor. The source generator test suite covers their
    // shape and validation. This file covers transport-level options only.

    [Fact]
    public void PublisherOptions_WaitForConfirmTrueWithConfirmsDisabled_FailsValidateOnStart()
    {
        // WaitForConfirm=true with PublisherConfirmsEnabled=false leaves the
        // channel with no protocol mechanism to confirm a publish, so every
        // "confirmed" publish becomes fire-and-forget with no error.
        // ValidateOnStart must fail at composition time, so that this
        // misconfiguration does not reach production.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configureChannelPool: o => o.PublisherConfirmsEnabled = false,
            configurePublisher: o => o.WaitForConfirm = true);
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        act.Should().Throw<OptionsValidationException>(
            "WaitForConfirm=true requires PublisherConfirmsEnabled=true");
    }

    [Fact]
    public void PublisherOptions_WaitForConfirmTrueWithConfirmsEnabled_PassesValidateOnStart()
    {
        // The inverse of the mismatch test: when both flags agree, or
        // WaitForConfirm is false, the ValidateOnStart predicate returns true
        // and the host starts.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configureChannelPool: o => o.PublisherConfirmsEnabled = true,
            configurePublisher: o => o.WaitForConfirm = true);
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        act.Should().NotThrow("matched flags must pass ValidateOnStart");
    }
}
