// -----------------------------------------------------------------------
// <copyright file="FailingStatementInterceptorFake.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Replaces every command whose text contains a given fragment with a statement
/// the server rejects, so one statement fails on a live connection without a
/// transaction being involved.
/// </summary>
/// <remarks>
/// The replacement divides by zero, which PostgreSQL rejects with SQLSTATE
/// 22012. Outside a transaction that rejection leaves the session usable.
/// </remarks>
internal sealed class FailingStatementInterceptorFake : DbCommandInterceptor
{
    private const string _REFUSED_SQL = "SELECT 1/0";

    private readonly string r_fragment;
    private int _replaced;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailingStatementInterceptorFake"/> class.
    /// </summary>
    /// <param name="fragment">The text that marks a command to replace.</param>
    internal FailingStatementInterceptorFake(string fragment)
    {
        r_fragment = fragment;
    }

    /// <summary>Gets how many commands this interceptor replaced.</summary>
    internal int Replaced => Volatile.Read(ref _replaced);

    /// <inheritdoc/>
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Replace(command);

        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Replace(command);

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Replace(command);

        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Replace(command);

        return ValueTask.FromResult(result);
    }

    private void Replace(DbCommand command)
    {
        if (!command.CommandText.Contains(r_fragment, StringComparison.Ordinal))
            return;

        command.CommandText = _REFUSED_SQL;
        command.Parameters.Clear();
        Interlocked.Increment(ref _replaced);
    }
}
