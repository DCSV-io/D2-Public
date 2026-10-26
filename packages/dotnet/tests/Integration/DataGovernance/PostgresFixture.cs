// -----------------------------------------------------------------------
// <copyright file="PostgresFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Threading.Tasks;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Shared Testcontainers PostgreSQL fixture. One container per xUnit collection.
/// <para>
/// Test classes that share a schema (advisory locks, index probes) use the shared
/// <see cref="ConnectionString"/>. Test classes that each call
/// <c>EnsureCreatedAsync</c> on a distinct <c>DbContext</c> type MUST call
/// <see cref="CreateIsolatedDatabaseAsync"/> in <c>InitializeAsync</c> to obtain a
/// per-class database, preventing schema-collision 42P01 errors caused by
/// <c>EnsureCreated</c> being a no-op on an already-existing database. Such a
/// class passes the same connection string to
/// <see cref="AssertNoConnectionOpenAsync"/> from <c>DisposeAsync</c>.
/// </para>
/// </summary>
[MustDisposeResource(false)]
public sealed class PostgresFixture : IAsyncLifetime
{
    // TEST-INFRA: up to 3 startup attempts, 5 s backoff - guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    // How many times the open-connection check reads the server's count before
    // it reports a connection still open, and the pause between reads.
    private const int _OPEN_CHECK_ATTEMPTS = 50;
    private const int _OPEN_CHECK_BACKOFF_MS = 20;

    private PostgreSqlContainer _container = BuildContainer();

    /// <summary>Gets the Npgsql connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        for (var attempt = 1; attempt <= _STARTUP_ATTEMPTS; attempt++)
        {
            try
            {
                await _container.StartAsync();
                return;
            }
            catch (Exception) when (attempt < _STARTUP_ATTEMPTS)
            {
                await _container.DisposeAsync();
                await Task.Delay(_STARTUP_BACKOFF_MS);
                _container = BuildContainer();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Provisions a new, uniquely-named database on the running container and returns
    /// its connection string. Each call with a distinct <paramref name="label"/> creates
    /// an isolated database, so concurrent test classes that each call
    /// <c>EnsureCreatedAsync</c> on their own <c>DbContext</c> build their full schema
    /// in separate databases with zero cross-class interference.
    /// </summary>
    /// <param name="label">
    /// A short human-readable label (e.g., the test class name). Combined with a
    /// short random suffix to form the database name. Must contain only ASCII letters,
    /// digits, and underscores; any other character is replaced with <c>_</c>.
    /// The combined name is truncated to 63 characters (Postgres identifier limit).
    /// </param>
    /// <returns>
    /// A Npgsql connection string that targets the newly created isolated database,
    /// with pooling off.
    /// </returns>
    public async Task<string> CreateIsolatedDatabaseAsync(string label)
    {
        var safe = MakeSafeDatabaseName(label);
        var dbName = safe + "_" + Guid.NewGuid().ToString("N")[..8];

        // Trim to 63 chars - Postgres max identifier length.
        if (dbName.Length > 63)
            dbName = dbName[..63];

        var adminCs = _container.GetConnectionString();

        await using var conn = new NpgsqlConnection(adminCs);
        await conn.OpenAsync();

        // CREATE DATABASE cannot run inside a transaction; NpgsqlConnection defaults to
        // AutoCommit mode so no explicit transaction management is needed here.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE \"" + dbName + "\"";
        await cmd.ExecuteNonQueryAsync();

        // Swap the database name in the container connection string. Each call
        // names a new database, so a pooled string would open a new pool per call,
        // and a pool keeps its idle connections for minutes after the test ends.
        // Across a run those idle connections exceed the server's connection
        // limit, so pooling is off and a dispose closes the connection.
        var builder = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Database = dbName,
            Pooling = false,
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Throws when the server still reports a connection to a database from
    /// <see cref="CreateIsolatedDatabaseAsync"/>. Run when a test ends, it fails
    /// the test that left a connection, context or data source undisposed,
    /// rather than a later test that can no longer connect.
    /// </summary>
    /// <param name="connectionString">
    /// The connection string <see cref="CreateIsolatedDatabaseAsync"/> returned.
    /// </param>
    /// <returns>A task that completes when no connection to the database is open.</returns>
    /// <exception cref="InvalidOperationException">
    /// A connection to the database is still open.
    /// </exception>
    public async Task AssertNoConnectionOpenAsync(string connectionString)
    {
        var dbName = new NpgsqlConnectionStringBuilder(connectionString).Database;

        // The server ends a backend shortly after its client disconnects, so the
        // count is read again until it reaches zero or the attempts run out.
        var open = await CountConnectionsAsync(dbName);

        for (var attempt = 1; open > 0 && attempt < _OPEN_CHECK_ATTEMPTS; attempt++)
        {
            await Task.Delay(_OPEN_CHECK_BACKOFF_MS);
            open = await CountConnectionsAsync(dbName);
        }

        if (open > 0)
        {
            throw new InvalidOperationException(
                $"{open} connection(s) to the isolated database {dbName} are still open "
                + "after the test ended; a connection, context or data source it opened "
                + "was not disposed.");
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static PostgreSqlContainer BuildContainer() =>
        new PostgreSqlBuilder("postgres:18.3-trixie")
            .Build();

    private static string MakeSafeDatabaseName(string label)
    {
        if (label.Falsey())
            return "isolated";

        var chars = new char[label.Length];

        for (var i = 0; i < label.Length; i++)
        {
            var c = label[i];
            chars[i] = char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }

        return new string(chars);
    }

    private async Task<long> CountConnectionsAsync(string? dbName)
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname = @db";
        cmd.Parameters.AddWithValue("db", dbName ?? string.Empty);

        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }
}
