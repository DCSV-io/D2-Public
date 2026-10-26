// -----------------------------------------------------------------------
// <copyright file="OutboxRelayPostgresServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;
using Xunit;
using IClock = DcsvIo.D2.Time.IClock;

/// <summary>
/// The relay's own registration: what one call leaves running, and whether the
/// database it was given is the database the relay it builds works over. A relay
/// that reached another database's dispatchers would deliver another module's
/// messages.
/// </summary>
public sealed class OutboxRelayPostgresServiceCollectionExtensionsTests
{
    private const string _CONNECTION =
        "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p";

    private const string _OTHER_CONNECTION =
        "Host=other-probe.invalid;Database=d2;Username=u;Password=p";

    [Fact]
    public void AddD2OutboxRelayPostgres_MakesTheRelayResolvable()
    {
        using var provider = Composed();

        provider.GetRequiredService<IEnumerable<IHostedService>>()
            .Should().ContainSingle(s => s is OutboxRelay<IOutboxProbeDbFixture>);
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_RegistersOneHostedService()
    {
        using var provider = Composed();

        provider.GetRequiredService<IEnumerable<IHostedService>>().Should().HaveCount(1);
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_EachRelayIndexesOnlyItsOwnDatabasesDispatchers()
    {
        // The relay resolves its dispatchers by the type parameter. A
        // registration that took the other module's would build a relay that
        // delivers the wrong module's messages - and both relays are present
        // here, so the assertion fails if either one takes the other's
        // dispatcher.
        using var provider = TwoModules();
        var hosted = provider.GetRequiredService<IEnumerable<IHostedService>>().ToList();

        var mine = hosted.OfType<OutboxRelay<IOutboxProbeDbFixture>>().Single();
        var theirs = hosted.OfType<OutboxRelay<ISecondOutboxProbeDbFixture>>().Single();

        mine.BuildIndex().Keys.Should()
            .BeEquivalentTo([nameof(FakeOutboxKind.ThingHappened)]);
        theirs.BuildIndex().Keys.Should()
            .BeEquivalentTo([nameof(FakeOutboxKind.OtherThingHappened)]);
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_TwoOutboxesOnOneCollection_RegisterOneRelayEach()
    {
        using var provider = TwoModules();
        var hosted = provider.GetRequiredService<IEnumerable<IHostedService>>().ToList();

        hosted.Should().ContainSingle(s => s is OutboxRelay<IOutboxProbeDbFixture>);
        hosted.Should().ContainSingle(s => s is OutboxRelay<ISecondOutboxProbeDbFixture>);
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_CalledTwiceForOneDatabase_RegistersOneRelay()
    {
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IEnumerable<IHostedService>>().Should().HaveCount(1);
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_WithNoStoreForThatDatabase_FailsHostStart()
    {
        // Unguarded, the host starts and the relay stalls: every tick resolves
        // no store, and the catch that keeps a tick from crashing the host
        // swallows it.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        var act = () => ResolveHostedServices(services);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(IOutboxProbeDbFixture));
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_WithTheStoreForAnotherDatabase_FailsHostStart()
    {
        // A store registered for a different database stalls the relay exactly
        // as no store does, and reads as a working composition at a glance.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddDbContext<SecondOutboxProbeDbContextFixture>(
            o => o.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        var act = () => ResolveHostedServices(services);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(IOutboxProbeDbFixture));
    }

    [Fact]
    public void OutboxRelay_AsksWhetherItsOwnDatabasesStoreIsRegistered()
    {
        // The guard checks its own database's store type. Checking any other
        // closed store type would admit the mis-paired composition the
        // FailsHostStart cases above refuse.
        var probe = new FakeServiceProviderIsService(answer: true);

        _ = NewRelay(probe);

        probe.Asked.Should().Equal([typeof(IOutboxStore<IOutboxProbeDbFixture>)]);
    }

    [Fact]
    public void OutboxRelay_WhenItsStoreIsNotRegistered_Throws()
    {
        var act = () => NewRelay(new FakeServiceProviderIsService(answer: false));

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(IOutboxProbeDbFixture));
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_WithNoServices_Throws()
    {
        var act = () => ((IServiceCollection)null!)
            .AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2OutboxRelayPostgres_ReturnsTheSameCollectionForChaining()
    {
        var services = Services();

        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>().Should().BeSameAs(services);
    }

    private static OutboxRelay<IOutboxProbeDbFixture> NewRelay(
        FakeServiceProviderIsService storePresence)
        => new(
            new FakeSystemWorkScopeFactory(new EmptyServiceProviderStub()),
            [],
            new TestClock(Instant.FromUtc(2026, 8, 17, 14, 0)),
            new StaticOptionsMonitorFixture<OutboxOptionsFor<IOutboxProbeDbFixture>>(
                new OutboxOptionsFor<IOutboxProbeDbFixture>
                {
                    RelayAdvisoryLockKey = 4242,
                    RelayConnectionString = _CONNECTION,
                }),
            new TestLogger<OutboxRelay<IOutboxProbeDbFixture>>(),
            storePresence);

    private static List<IHostedService> ResolveHostedServices(IServiceCollection services)
    {
        // The provider is built and disposed inside this call so nothing outside
        // it holds a disposed reference. Resolving the hosted services is what a
        // host does at start, which is where the refusal is meant to occur.
        using var provider = services.BuildServiceProvider(validateScopes: true);

        return [.. provider.GetRequiredService<IEnumerable<IHostedService>>()];
    }

    private static ServiceProvider TwoModules()
    {
        // Two modules, two databases, one container. Everything below is
        // registered twice on purpose.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        services.AddDbContext<SecondOutboxProbeDbContextFixture>(
            o => o.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddD2Outbox<ISecondOutboxProbeDbFixture>(o =>
        {
            o.RelayAdvisoryLockKey = 9191;
            o.RelayConnectionString = _OTHER_CONNECTION;
        });
        services.AddD2OutboxDispatcher<ISecondOutboxProbeDbFixture, OtherSampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>();
        services.AddD2OutboxRelayPostgres<ISecondOutboxProbeDbFixture>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider Composed()
    {
        // The store half is registered here because the relay half requires it.
        // Without it this helper builds the mis-paired composition the
        // FailsHostStart cases pin, which is not what the rest of the file
        // asserts over.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();
        services.AddD2OutboxRelayPostgres<IOutboxProbeDbFixture>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new TestClock(Instant.FromUtc(2026, 8, 17, 14, 0)));
        services.AddSingleton<ISystemWorkScopeFactory>(
            new FakeSystemWorkScopeFactory(new EmptyServiceProviderStub()));
        services.AddDbContext<OutboxProbeDbContextFixture>(
            o => o.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddD2Outbox<IOutboxProbeDbFixture>(o =>
        {
            o.RelayAdvisoryLockKey = 4242;
            o.RelayConnectionString = _CONNECTION;
        });

        return services;
    }
}
