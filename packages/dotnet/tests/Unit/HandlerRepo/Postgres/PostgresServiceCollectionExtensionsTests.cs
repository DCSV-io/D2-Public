// -----------------------------------------------------------------------
// <copyright file="PostgresServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Handler.Repo.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Dependency injection registration tests. The effect of <c>TryAdd</c>
/// depends on registration order. Two of these tests pin the order cases: a
/// custom classifier registered before <c>AddD2Postgres</c> is kept, and a
/// custom classifier registered after it resolves instead of the Postgres one,
/// because the last registration wins.
/// </summary>
public sealed class PostgresServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Postgres_RegistersPostgresClassifierAsImplementation()
    {
        var services = new ServiceCollection();

        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<PostgresDbExceptionClassifier>();
    }

    [Fact]
    public void AddD2Postgres_CalledTwice_DoesNotDuplicateRegistration()
    {
        var services = new ServiceCollection();

        services.AddD2Postgres();
        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();

        // GetServices returns every registration, and there is exactly one.
        var all = sp.GetServices<IDbExceptionClassifier>();

        all.Should().ContainSingle();
    }

    [Fact]
    public void AddD2Postgres_CustomClassifierRegisteredFirst_CustomWins()
    {
        // Documented behavior: TryAddSingleton finds an existing registration
        // and does nothing, so a custom implementation registered before
        // AddD2Postgres resolves.
        var services = new ServiceCollection();
        services.AddSingleton<IDbExceptionClassifier, CustomClassifier>();

        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<CustomClassifier>();
    }

    [Fact]
    public void AddD2Postgres_CustomClassifierRegisteredAfter_BclLastWinsResolvesCustom()
    {
        // BCL ServiceProvider semantics: when several plain
        // AddSingleton<I, X>() registrations exist for one service type,
        // GetRequiredService<I>() returns the last registered implementation.
        // AddD2Postgres uses TryAddSingleton, and the later AddSingleton call
        // appends a separate entry, so a custom IDbExceptionClassifier
        // registered after AddD2Postgres resolves.
        //
        // The earlier Postgres entry stays in the descriptor list and is
        // constructed if anything enumerates IEnumerable<IDbExceptionClassifier>.
        // The docs therefore advise registering the custom classifier before
        // the call, where TryAdd does nothing, or using keyed services for
        // several classifiers. Both leave no unused entry in the graph.
        var services = new ServiceCollection();

        services.AddD2Postgres();
        services.AddSingleton<IDbExceptionClassifier, CustomClassifier>();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<CustomClassifier>(
            "BCL ServiceProvider resolves the LAST-registered singleton - "
            + "the custom registered AFTER wins (with the orphaned Postgres "
            + "entry as the tradeoff the docs warn against).");
    }

    [Fact]
    public void AddD2Postgres_Resolved_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddD2Postgres();
        using var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<IDbExceptionClassifier>();
        var second = sp.GetRequiredService<IDbExceptionClassifier>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2Postgres_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddD2Postgres();

        returned.Should().BeSameAs(services);
    }

    private sealed class CustomClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => DbFailureKind.UniqueViolation;
    }
}
