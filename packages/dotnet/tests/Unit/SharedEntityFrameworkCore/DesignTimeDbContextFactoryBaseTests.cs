// -----------------------------------------------------------------------
// <copyright file="DesignTimeDbContextFactoryBaseTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Unit tests for <see cref="DesignTimeDbContextFactoryBase{TContext}"/>:
/// env-var-absent throws <see cref="InvalidOperationException"/>;
/// env-var-present returns a concrete <c>TContext</c> instance.
/// Uses a local probe subclass whose environment-variable key is new for every
/// test, so no other test or process reads or writes the variable a case sets;
/// the case that sets one removes it again.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DesignTimeDbContextFactoryBaseTests
{
    // Connection string used when the env var is set (no real DB is opened -
    // CreateDbContext only builds options; it does not open a connection).
    private const string _CONN_STR = "Host=localhost;Database=dt_probe;Username=u;Password=p";

    // =========================================================================
    // Env-var absent -> InvalidOperationException
    // =========================================================================

    [Fact]
    public void CreateDbContext_EnvVarAbsent_ThrowsInvalidOperationException()
    {
        // A fresh key nobody has set, so the variable is absent without a write.
        var factory = new ProbeDbContextFactory(UniqueEnvVar());

        var act = () => factory.CreateDbContext([]);

        act.Should().ThrowExactly<InvalidOperationException>(
            "CreateDbContext must throw when the required connection-string env var is absent");
    }

    // =========================================================================
    // Env-var present -> concrete TContext returned
    // =========================================================================

    [Fact]
    public void CreateDbContext_EnvVarPresent_ReturnsConcreteContext()
    {
        var envVar = UniqueEnvVar();
        Environment.SetEnvironmentVariable(envVar, _CONN_STR);

        try
        {
            var factory = new ProbeDbContextFactory(envVar);

            using var ctx = factory.CreateDbContext([]);

            ctx.Should().NotBeNull(
                "a valid connection string must produce a concrete context");
            ctx.Should().BeOfType<ProbeDbContext>(
                "the factory must return exactly the TContext subclass it was parameterized with");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    private static string UniqueEnvVar() => $"D2_TEST_DTFACTORY_CONNSTR_{Guid.NewGuid():N}";

    // =========================================================================
    // Probe types - self-contained; no migration assembly required
    // =========================================================================

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : DbContext(options);

    private sealed class ProbeDbContextFactory(string envVar)
        : DesignTimeDbContextFactoryBase<ProbeDbContext>
    {
        protected override string ConnectionStringEnvVar => envVar;

        protected override string MigrationsAssemblyName =>
            typeof(ProbeDbContextFactory).Assembly.GetName().Name!;

        protected override ProbeDbContext CreateContext(DbContextOptions<ProbeDbContext> options)
            => new(options);
    }
}
