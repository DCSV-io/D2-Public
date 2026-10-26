// -----------------------------------------------------------------------
// <copyright file="SubscriberAssemblyScanTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Unit.Messaging.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests of the assembly scan that takes a catalog: which catalog the
/// <c>[MqSub]</c> constants are looked up in, and the exception message the
/// scan throws when the supplied catalog lacks one.
/// </summary>
public sealed class SubscriberAssemblyScanTests
{
    private const string _QUEUE = "fixture-scan-queue";

    [Fact]
    public void AddD2SubscribersFromAssembly_WithACatalogCarryingTheConstant_RegistersTheHandler()
    {
        var services = new ServiceCollection();
        var descriptor = MessagingCatalogFixture.Subscription(
            MessagingCatalogFixture.SCAN_SUBSCRIPTION,
            typeof(SubscriberScanFixtureEvent),
            _QUEUE);
        var catalog = MessagingCatalogFixture.ForSubscriptions(
            (MessagingCatalogFixture.SCAN_SUBSCRIPTION, descriptor));

        services.AddD2SubscribersFromAssembly(
            catalog, typeof(SubscriberScanFixtureHandler).Assembly);

        using var provider = services.BuildServiceProvider();
        var registration = provider.GetServices<ISubscriberRegistration>()
            .Single(r => r.HandlerType == typeof(SubscriberScanFixtureHandler));
        registration.MessageType.Should().Be<SubscriberScanFixtureEvent>();
        registration.Descriptor.Should().BeSameAs(descriptor);
        registration.ResolvedQueueName.Should().Be(_QUEUE);
        services.Should().Contain(d =>
            d.ServiceType == typeof(SubscriberScanFixtureHandler)
            && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void AddD2SubscribersFromAssembly_RegistersTheSubscriberRegistryOverTheScan()
    {
        var services = new ServiceCollection();
        var catalog = MessagingCatalogFixture.ForSubscriptions(
            (MessagingCatalogFixture.SCAN_SUBSCRIPTION,
             MessagingCatalogFixture.Subscription(
                MessagingCatalogFixture.SCAN_SUBSCRIPTION,
                typeof(SubscriberScanFixtureEvent),
                _QUEUE)));

        services.AddD2SubscribersFromAssembly(
            catalog, typeof(SubscriberScanFixtureHandler).Assembly);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<SubscriberRegistry>().All
            .Should().ContainSingle(r => r.HandlerType == typeof(SubscriberScanFixtureHandler));
    }

    [Fact]
    public void AddD2SubscribersFromAssembly_WithACatalogLackingTheConstant_ThrowsNamingBoth()
    {
        var services = new ServiceCollection();
        var catalog = MessagingCatalogFixture.ForSubscriptions();

        var act = () => services.AddD2SubscribersFromAssembly(
            catalog, typeof(SubscriberScanFixtureHandler).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                "*" + typeof(SubscriberScanFixtureHandler).FullName + "*"
                + MessagingCatalogFixture.SCAN_SUBSCRIPTION + "*MessagingCatalog*");
    }

    [Fact]
    public void AddD2SubscribersFromAssembly_WithTheDefaultCatalog_ThrowsForTheFixtureConstant()
    {
        // The parameterless overload reads MessagingCatalog.Default, which has
        // only the framework's own entries. The same scan that resolves against
        // a supplied catalog therefore throws against the default catalog.
        var services = new ServiceCollection();

        var act = () => services.AddD2SubscribersFromAssembly(
            MessagingCatalog.Default, typeof(SubscriberScanFixtureHandler).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*" + MessagingCatalogFixture.SCAN_SUBSCRIPTION + "*");
    }

    [Fact]
    public void AddD2SubscribersFromAssembly_OnANullCatalog_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2SubscribersFromAssembly(
            (MessagingCatalog)null!, typeof(SubscriberScanFixtureHandler).Assembly);

        act.Should().Throw<ArgumentNullException>();
    }
}
