// -----------------------------------------------------------------------
// <copyright file="OutboxServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;
using IClock = DcsvIo.D2.Time.IClock;

/// <summary>
/// The database-agnostic half (options and dispatchers) and the storage half
/// (store and staging seam) of the registration, and what each one leaves
/// resolvable. A seam a consumer cannot resolve fails at the host's first
/// request rather than at its composition.
/// </summary>
public sealed class OutboxServiceCollectionExtensionsTests
{
    private const string _CONNECTION =
        "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p";

    private const string _OTHER_CONNECTION =
        "Host=other-probe.invalid;Database=d2;Username=u;Password=p";

    [Fact]
    public void AddD2Outbox_MakesTheOptionsResolvable()
    {
        using var provider = Composed();

        provider.GetRequiredService<IOptionsMonitor<OutboxOptionsFor<IOutboxProbeDbFixture>>>()
            .CurrentValue.Should().NotBeNull();
    }

    [Fact]
    public void AddD2Outbox_AppliesTheConfiguredLockKey()
    {
        using var provider = Composed();

        provider.GetRequiredService<IOptionsMonitor<OutboxOptionsFor<IOutboxProbeDbFixture>>>()
            .CurrentValue.RelayAdvisoryLockKey.Should().Be(4242);
    }

    [Fact]
    public void AddD2Outbox_AppliesTheConfiguredConnectionString()
    {
        // The relay cannot read this off the context it was registered over, so a
        // registration that dropped it would leave every tick failing to
        // authenticate, silently, while messages stay staged.
        using var provider = Composed();

        provider.GetRequiredService<IOptionsMonitor<OutboxOptionsFor<IOutboxProbeDbFixture>>>()
            .CurrentValue.RelayConnectionString.Should().Be(_CONNECTION);
    }

    [Fact]
    public void AddD2Outbox_WithNoServices_Throws()
    {
        var act = () => ((IServiceCollection)null!).AddD2Outbox<IOutboxProbeDbFixture>(_ => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Outbox_WithNoConfigureDelegate_Throws()
    {
        var act = () => new ServiceCollection().AddD2Outbox<IOutboxProbeDbFixture>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Outbox_ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        services.AddD2Outbox<IOutboxProbeDbFixture>(_ => { }).Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2OutboxDispatcher_MakesTheBindingResolvable()
    {
        using var provider = Composed();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AddD2OutboxDispatcher_BindsTheDispatcherItWasGiven()
    {
        using var provider = Composed();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Select(b => b.Dispatcher.GetType())
            .Should().BeEquivalentTo([typeof(SampleOutboxDispatcher)]);
    }

    [Fact]
    public void AddD2OutboxDispatcher_MakesTheDispatcherItselfResolvable()
    {
        // The binding takes the dispatcher through its constructor, so the
        // dispatcher has to be a service in its own right for the container to
        // build one.
        using var provider = Composed();

        provider.GetRequiredService<SampleOutboxDispatcher>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2OutboxDispatcher_TwiceForOneType_RegistersItOnce()
    {
        // The registration deduplicates on the database and implementation pair,
        // so a consumer that registers defensively does not end up with a
        // duplicate kind that makes the relay's index fail to build.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AddD2OutboxDispatcher_TwoDifferentTypes_RegistersBoth()
    {
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, OtherSampleOutboxDispatcher>();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Should().HaveCount(2);
    }

    [Fact]
    public void AddD2OutboxDispatcher_OneTypeForTwoDatabases_BindsItToEach()
    {
        // Deduplication is on the database and dispatcher pair, not on the
        // dispatcher alone. Keyed on the dispatcher alone, the second call would
        // add nothing and that database's relay would find no transport for the
        // kind, so every message it staged would be abandoned at the retry limit.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxDispatcher<ISecondOutboxProbeDbFixture, SampleOutboxDispatcher>();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Should().ContainSingle();
        provider.GetServices<IOutboxDispatcherBinding<ISecondOutboxProbeDbFixture>>()
            .Should().ContainSingle();
    }

    [Fact]
    public void AddD2OutboxDispatcher_WithTheDispatcherAlreadyRegistered_KeepsThatRegistration()
    {
        // The dispatcher goes in with TryAdd, so a host that has already
        // registered its own configured instance keeps it and the binding
        // uses that one rather than a second the container built.
        var chosen = new SampleOutboxDispatcher();
        var services = Services();
        services.AddSingleton(chosen);
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Single().Dispatcher.Should().BeSameAs(chosen);
    }

    [Fact]
    public void AddD2OutboxDispatcher_OneTypeForTwoDatabases_SharesOneInstance()
    {
        // One singleton registration of the dispatcher type serves both
        // bindings. A dispatcher that held per-database state would be wrong
        // under this shape, so the type is a transport and not a place to keep
        // any.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxDispatcher<ISecondOutboxProbeDbFixture, SampleOutboxDispatcher>();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Single().Dispatcher
            .Should().BeSameAs(
                provider.GetServices<IOutboxDispatcherBinding<ISecondOutboxProbeDbFixture>>()
                    .Single().Dispatcher);
    }

    [Fact]
    public void AddD2OutboxDispatcher_WithNoServices_Throws()
    {
        var act = () => ((IServiceCollection)null!)
            .AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2OutboxDispatcher_ReturnsTheSameCollectionForChaining()
    {
        var services = Services();

        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>()
            .Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_MakesTheStoreResolvable()
    {
        using var provider = Composed();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxStore<IOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_MakesTheStagingSeamResolvable()
    {
        using var provider = Composed();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMessageOutbox<IOutboxProbeDbFixture>>()
            .Should()
            .BeOfType<EfMessageOutbox<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_RegistersNoHostedService()
    {
        // The store half binds no provider. Registering the relay here would put
        // a PostgreSQL advisory lock behind every consumer that wanted only the
        // row and the staging seam. The provider leaf keeps that lock out of this
        // package.
        using var provider = Composed();

        provider.GetRequiredService<IEnumerable<IHostedService>>().Should().BeEmpty();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_WithNoServices_Throws()
    {
        var act = () => ((IServiceCollection)null!)
            .AddD2OutboxEntityFrameworkCore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_ReturnsTheSameCollectionForChaining()
    {
        var services = Services();

        services
            .AddD2OutboxEntityFrameworkCore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>()
            .Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_LeavesAConsumerSeamAlone()
    {
        // The store is registered with TryAdd for this database, so a consumer
        // that wants its own keeps it rather than having the package's silently
        // win.
        var services = Services();
        services.AddScoped<IOutboxStore<IOutboxProbeDbFixture>, FakeOutboxStore>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxStore<IOutboxProbeDbFixture>>()
            .Should().BeOfType<FakeOutboxStore>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_WithTheContextBoundToAnotherDatabase_Throws()
    {
        // One concrete context can implement two application-layer interfaces,
        // and the TDbContext : DbContext, TDb constraint admits both. Accepted,
        // the pair yields two stores and two relays over the one message table,
        // each holding its own advisory lock, so neither serializes against the
        // other and either can claim a row whose kind it has no dispatcher for.
        var services = new ServiceCollection();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        var act = () => services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_WithTheContextBoundToAnotherDatabase_NamesBoth()
    {
        // The refusal has to say which two interfaces collided over which
        // context, because the call that throws identifies only one of them.
        var services = new ServiceCollection();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        var act = () => services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TwoInterfaceOutboxProbeDbContextFixture*")
            .WithMessage("*IOutboxProbeDbFixture*")
            .WithMessage("*ISecondOutboxProbeDbFixture*");
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_WithTheDatabaseBoundToAnotherContext_Throws()
    {
        // Two concrete contexts implementing one interface compile; unrefused,
        // the second context's seams would no-op under TryAdd.
        var services = new ServiceCollection();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        var act = () => services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_WithTheDatabaseBoundToAnotherContext_NamesAllThree()
    {
        // The interface and both contexts, because the call that throws identifies
        // the interface and the second context only, and the first context is
        // what the host has to go and look at.
        var services = new ServiceCollection();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        var act = () => services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        // The namespace qualifier on the first context is required:
        // TwoInterfaceOutboxProbeDbContextFixture ends with the first context's
        // simple name, so an unqualified match would pass on the second name
        // alone.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IOutboxProbeDbFixture*")
            .WithMessage("*Fixtures.OutboxProbeDbContextFixture*")
            .WithMessage("*Fixtures.TwoInterfaceOutboxProbeDbContextFixture*");
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_AfterARefusedCall_RecordsNothing()
    {
        // The refused context stays free to register under the interface it
        // does belong to.
        var services = Services();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        var refused = () => services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();
        refused.Should().Throw<InvalidOperationException>();

        services.AddDbContext<TwoInterfaceOutboxProbeDbContextFixture>(
            o => o.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxStore<ISecondOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<
                ISecondOutboxProbeDbFixture, TwoInterfaceOutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_TwiceForOneDatabase_RegistersOneStore()
    {
        // The same pair twice is a consumer registering defensively, not the
        // collision, so it stays idempotent.
        var services = Services();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

        services.Count(d => d.ServiceType == typeof(IOutboxStore<IOutboxProbeDbFixture>))
            .Should().Be(1);
    }

    [Fact]
    public void AddD2OutboxEntityFrameworkCore_ForTwoContextsUnderTwoDatabases_RegistersBoth()
    {
        // The admitting case beside the refusal. The refusal is about one
        // context under two interfaces; two contexts under two interfaces is
        // the ordinary two-module host and has to keep working.
        var services = Services();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();
        services.AddDbContext<SecondOutboxProbeDbContextFixture>(
            o => o.UseInMemoryDatabase(Guid.CreateVersion7().ToString()));
        services.AddD2OutboxEntityFrameworkCore<
            ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxStore<IOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>>();
        scope.ServiceProvider.GetRequiredService<IOutboxStore<ISecondOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<
                ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void TwoOutboxesOnOneCollection_EachStagesOntoItsOwnContext()
    {
        using var provider = TwoModules();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMessageOutbox<IOutboxProbeDbFixture>>()
            .Should()
            .BeOfType<EfMessageOutbox<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>>();
        scope.ServiceProvider.GetRequiredService<IMessageOutbox<ISecondOutboxProbeDbFixture>>()
            .Should()
            .BeOfType<EfMessageOutbox<
                ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void TwoOutboxesOnOneCollection_EachReadsThroughItsOwnStore()
    {
        using var provider = TwoModules();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxStore<IOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>>();
        scope.ServiceProvider.GetRequiredService<IOutboxStore<ISecondOutboxProbeDbFixture>>()
            .Should().BeOfType<EfOutboxStore<
                ISecondOutboxProbeDbFixture, SecondOutboxProbeDbContextFixture>>();
    }

    [Fact]
    public void TwoOutboxesOnOneCollection_EachKeepsItsOwnLockKey()
    {
        using var provider = TwoModules();

        Options<IOutboxProbeDbFixture>(provider).RelayAdvisoryLockKey.Should().Be(4242);
        Options<ISecondOutboxProbeDbFixture>(provider).RelayAdvisoryLockKey.Should().Be(9191);
    }

    [Fact]
    public void TwoOutboxesOnOneCollection_EachKeepsItsOwnConnectionString()
    {
        using var provider = TwoModules();

        Options<IOutboxProbeDbFixture>(provider).RelayConnectionString.Should().Be(_CONNECTION);
        Options<ISecondOutboxProbeDbFixture>(provider).RelayConnectionString
            .Should().Be(_OTHER_CONNECTION);
    }

    [Fact]
    public void TwoOutboxesOnOneCollection_EachDatabaseGetsItsOwnDispatcher()
    {
        using var provider = TwoModules();

        provider.GetServices<IOutboxDispatcherBinding<IOutboxProbeDbFixture>>()
            .Should().ContainSingle(b => b.Dispatcher is SampleOutboxDispatcher);
        provider.GetServices<IOutboxDispatcherBinding<ISecondOutboxProbeDbFixture>>()
            .Should().ContainSingle(b => b.Dispatcher is OtherSampleOutboxDispatcher);
    }

    private static OutboxOptions Options<TDb>(IServiceProvider provider)
        => provider.GetRequiredService<IOptionsMonitor<OutboxOptionsFor<TDb>>>().CurrentValue;

    private static ServiceProvider TwoModules()
    {
        // The shape a host with two modules composes: two databases, one
        // container. Everything below is registered twice on purpose.
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

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

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider Composed()
    {
        var services = Services();
        services.AddD2OutboxDispatcher<IOutboxProbeDbFixture, SampleOutboxDispatcher>();
        services.AddD2OutboxEntityFrameworkCore<
            IOutboxProbeDbFixture, OutboxProbeDbContextFixture>();

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
