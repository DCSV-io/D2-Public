// -----------------------------------------------------------------------
// <copyright file="PgClassedAdvisoryLock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore;

using System;
using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Two-integer PostgreSQL advisory locks taken on a <see cref="DbContext"/>'s own
/// connection.
/// </summary>
/// <remarks>
/// <para>
/// The statements are PostgreSQL, and this type ships from the provider-neutral
/// package anyway. It needs no driver type - the SQL crosses as text on the EF
/// context and the refusal is read from <see cref="DbException.SqlState"/>.
/// Application-layer types can use it from this package, because the layering gate
/// forbids them from reaching the Npgsql closure the PostgreSQL package contains.
/// </para>
/// <para>
/// PostgreSQL tags <c>pg_advisory_lock(class, key)</c> differently from
/// <c>pg_advisory_lock(bigint)</c>, so this keyspace is disjoint from the one
/// <c>PgAdvisoryLock</c> uses. Register the class in the advisory-locks
/// catalog and pass the generated constant rather than a literal.
/// </para>
/// <para>
/// The waiting form holds the lock on the session, so it outlives the
/// transactions the work opens and the context's connection stays held
/// until the work returns. The budgeted form holds it on the transaction it
/// opens, so the engine releases it as that transaction ends.
/// </para>
/// <para>
/// A non-relational provider runs the work directly. The serialization is
/// asserted against a real database, the only place it can be asserted.
/// </para>
/// </remarks>
public static class PgClassedAdvisoryLock
{
    /// <summary>
    /// <c>lock_not_available</c> - what the engine reports for both a refused
    /// <c>NOWAIT</c> and a wait that reached <c>lock_timeout</c>.
    /// </summary>
    internal const string LOCK_NOT_AVAILABLE = "55P03";

    /// <summary>
    /// Takes the lock for the session, so it outlives any transaction opened
    /// under it and is released by name.
    /// </summary>
    private const string _LOCK_SQL = "SELECT pg_advisory_lock({0}, {1})";

    /// <summary>Releases what <see cref="_LOCK_SQL"/> took.</summary>
    private const string _UNLOCK_SQL = "SELECT pg_advisory_unlock({0}, {1})";

    /// <summary>
    /// Ends whatever transaction the session has open. Outside a transaction the
    /// engine responds with a warning rather than an error.
    /// </summary>
    private const string _ROLLBACK_SQL = "ROLLBACK";

    /// <summary>
    /// Ends this session. The engine drops every advisory lock a session holds
    /// when the session ends, and the provider discards the broken connection
    /// rather than returning it to its pool.
    /// </summary>
    private const string _END_SESSION_SQL = "SELECT pg_terminate_backend(pg_backend_pid())";

    /// <summary>
    /// Takes the lock for the current transaction, which the engine releases as
    /// that transaction ends. Waiting on it is bounded by <c>lock_timeout</c>.
    /// </summary>
    private const string _XACT_LOCK_SQL = "SELECT pg_advisory_xact_lock({0}, {1})";

    /// <summary>
    /// The transaction-scoped form for a caller with no wait budget at all,
    /// which reports whether it got the lock rather than queueing for it.
    /// </summary>
    /// <remarks>A scalar <c>SqlQueryRaw</c> reads the value from this column alias.</remarks>
    private const string _TRY_XACT_LOCK_SQL =
        "SELECT pg_try_advisory_xact_lock({0}, {1}) AS \"Value\"";

    /// <summary>
    /// Bounds the wait to the caller's budget. Parameterized through
    /// <c>set_config</c> rather than composed into a <c>SET</c> statement, so
    /// the budget crosses as a value and not as text.
    /// </summary>
    private const string _LOCK_TIMEOUT_SQL = "SELECT set_config('lock_timeout', {0}, true)";

    /// <summary>
    /// Reads the budget in force, so a caller that changes it for one wait can
    /// put the previous value back.
    /// </summary>
    /// <remarks>A scalar <c>SqlQueryRaw</c> reads the value from this column alias.</remarks>
    private const string _READ_LOCK_TIMEOUT_SQL =
        "SELECT current_setting('lock_timeout') AS \"Value\"";

    /// <summary>
    /// Runs <paramref name="work"/> with the session-scoped advisory lock held,
    /// waiting for it until the caller's token is canceled or the context's
    /// command timeout elapses.
    /// </summary>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <param name="lockClass">The registered advisory-lock class.</param>
    /// <param name="key">The identifier the second integer is folded from.</param>
    /// <param name="work">The work to run under the lock. It owns its own saves.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A task that completes when the work has returned and the lock is released.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the context or the work is null.</exception>
    /// <exception cref="DbException">
    /// Thrown when the wait for the lock reached the context's command timeout,
    /// in which case the work did not run. With Npgsql it is an
    /// <c>NpgsqlException</c> whose inner exception is a <see cref="TimeoutException"/>.
    /// </exception>
    public static async Task ExecuteLockedAsync(
        DbContext db,
        int lockClass,
        Guid key,
        Func<CancellationToken, Task> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);

        await ExecuteLockedAsync<object?>(
            db,
            lockClass,
            key,
            async inner =>
            {
                await work(inner);
                return null;
            },
            ct);
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the session-scoped advisory lock held
    /// and returns what the work returned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wait for the lock is one statement, so it lasts until the caller's
    /// token is canceled or the context's command timeout elapses, whichever
    /// comes first.
    /// </para>
    /// <para>
    /// When the release fails on a connection that is still open, the helper
    /// ends the transaction the session has open and releases again, and when
    /// that fails too it ends the session. A transaction the work left open and
    /// failed is the usual cause: it refuses every statement but the one that
    /// ends it. A pooled connection keeps its session until it is next opened,
    /// so a session still holding the lock would block every waiter on this
    /// class and key until then.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The value the work returns.</typeparam>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <param name="lockClass">The registered advisory-lock class.</param>
    /// <param name="key">The identifier the second integer is folded from.</param>
    /// <param name="work">The work to run under the lock. It owns its own saves.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the work returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the context or the work is null.</exception>
    /// <exception cref="DbException">
    /// Thrown when the wait for the lock reached the context's command timeout,
    /// in which case the work did not run. With Npgsql it is an
    /// <c>NpgsqlException</c> whose inner exception is a <see cref="TimeoutException"/>.
    /// </exception>
    public static async ValueTask<T> ExecuteLockedAsync<T>(
        DbContext db,
        int lockClass,
        Guid key,
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);

        if (!db.Database.IsRelational())
            return await work(ct);

        var lockKey = DeriveKey(key);

        // The lock lives on the session, so the connection has to stay this
        // caller's for as long as it is held, and the release runs on no
        // cancellation token, because a canceled caller needs the same release.
        await db.Database.OpenConnectionAsync(ct);

        try
        {
            await db.Database.ExecuteSqlRawAsync(_LOCK_SQL, [lockClass, lockKey], ct);

            try
            {
                return await work(ct);
            }
            finally
            {
                await ReleaseQuietlyAsync(db, lockClass, lockKey);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the transaction-scoped advisory lock
    /// held, where the wait for that lock is bounded by
    /// <paramref name="waitBudget"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The transaction this opens is the work's own: the work runs inside it and
    /// commits with it. A positive budget is applied as <c>lock_timeout</c>, so
    /// every wait inside the transaction - this lock and any row lock the work
    /// takes - is bounded by the same constant.
    /// </para>
    /// <para>
    /// A budget of zero or less asks the engine to refuse rather than queue at
    /// all, and bounds only that attempt on the advisory lock. No
    /// <c>lock_timeout</c> is set on that branch, because zero disables it in
    /// PostgreSQL, so a wait the work takes is bounded by whatever the session
    /// already runs under.
    /// </para>
    /// <para>
    /// When the work or the commit throws, the transaction rolls back and the
    /// context's change tracker is cleared before the fault is rethrown, for the
    /// reason the returns text gives for a refused wait. A fault raised before
    /// the work started leaves the tracker as it was.
    /// </para>
    /// <para>
    /// A non-relational provider runs the work directly and reports that it ran.
    /// </para>
    /// </remarks>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <param name="lockClass">The registered advisory-lock class.</param>
    /// <param name="key">The identifier the second integer is folded from.</param>
    /// <param name="work">The work to run under the lock. It owns its own saves.</param>
    /// <param name="waitBudget">The longest this caller may block on the lock.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the lock was held and the work ran. With a positive
    /// budget, <c>false</c> when some wait inside the transaction reached the
    /// budget, in which case the work did not run to completion and nothing it
    /// would have written exists. The budget is <c>lock_timeout</c> for the
    /// whole transaction, so the wait that reached it is this lock's or one the
    /// work took under the same bound - a row lock, say. With a budget of zero
    /// or less, <c>false</c> means only that the lock was held. Both outcomes - a
    /// lock held under a zero budget and a wait that reached a positive budget -
    /// return <c>false</c> on purpose: both mean this caller did not get through inside the
    /// time it allowed itself, and the engine reports them with one SQLSTATE, so
    /// telling the caller which resource was contended would be a guess. On a
    /// <c>false</c> from a wait that reached the budget the context's
    /// change tracker is cleared, including entities tracked before the call: the
    /// rollback undid every save the work made, so what the context tracked may
    /// no longer match the database, and a later save on the same context
    /// writes none of the refused work. A <c>false</c> from a budget of zero or
    /// less that found the lock held leaves the tracker as it was, because the
    /// work did not run.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the context or the work is null.</exception>
    public static async Task<bool> TryExecuteLockedAsync(
        DbContext db,
        int lockClass,
        Guid key,
        Func<CancellationToken, Task> work,
        TimeSpan waitBudget,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);

        if (!db.Database.IsRelational())
        {
            await work(ct);
            return true;
        }

        var lockKey = DeriveKey(key);
        var strategy = db.Database.CreateExecutionStrategy();
        var ran = false;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var workStarted = false;

            try
            {
                if (waitBudget > TimeSpan.Zero)
                {
                    await ApplyLockTimeoutAsync(db, waitBudget, ct);
                    await db.Database.ExecuteSqlRawAsync(
                        _XACT_LOCK_SQL, [lockClass, lockKey], ct);
                }
                else
                {
                    var taken = await db.Database
                        .SqlQueryRaw<bool>(_TRY_XACT_LOCK_SQL, lockClass, lockKey)
                        .SingleAsync(ct);

                    if (!taken)
                    {
                        // Somebody else holds it and this caller may not queue at
                        // all, so nothing ran and there is nothing to undo beyond
                        // the transaction that took no lock.
                        await RollbackQuietlyAsync(tx);
                        ran = false;

                        return;
                    }
                }

                workStarted = true;
                await work(ct);
                await tx.CommitAsync(ct);
                ran = true;
            }
            catch (Exception refused) when (IsLockNotAvailable(refused))
            {
                // Some wait inside this transaction reached the budget: this
                // lock, or one the work took under the lock_timeout applied
                // above. The engine reports both with one SQLSTATE and the
                // result is the same either way - this caller did not get
                // through inside the time it allowed itself. Nothing committed,
                // but the context still tracks what the work added, changed or
                // saved inside the rolled-back transaction, and a later save on
                // it would write that outside the lock.
                await RollbackQuietlyAsync(tx);
                db.ChangeTracker.Clear();
                ran = false;
            }
            catch
            {
                // The rollback undid whatever the work saved, so the context
                // would otherwise track rows the database no longer holds.
                await RollbackQuietlyAsync(tx);

                if (workStarted)
                    db.ChangeTracker.Clear();

                throw;
            }
        });

        return ran;
    }

    /// <summary>
    /// Folds an identifier into the second integer of the advisory lock.
    /// </summary>
    /// <remarks>
    /// The fold drops the sign bit, so the key is the value PostgreSQL reports
    /// for the lock. Two identifiers that fold to one key wait for each other,
    /// which costs them latency and nothing else: the work inside still decides
    /// on a read of the rows it identifies.
    /// </remarks>
    /// <param name="id">The identifier to fold.</param>
    /// <returns>The second integer to take the lock on.</returns>
    public static int DeriveKey(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);

        var key = 0;

        for (var offset = 0; offset < bytes.Length; offset += sizeof(int))
            key ^= BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);

        return key & int.MaxValue;
    }

    /// <summary>
    /// Reports whether an exception is the engine's <c>lock_not_available</c>
    /// refusal, wherever it sits in the exception's inner chain.
    /// </summary>
    /// <remarks>
    /// The provider exception reaches the caller bare only from a raw statement.
    /// A save wraps it in <see cref="DbUpdateException"/>, and the execution
    /// strategy wraps an error it classes as transient - a refused lock is one -
    /// in an <see cref="InvalidOperationException"/>, so a wait the work reaches
    /// through either has the SQLSTATE one or two levels down.
    /// </remarks>
    /// <param name="exception">The exception the transaction body raised.</param>
    /// <returns>
    /// <c>true</c> when some exception in the chain is a <see cref="DbException"/>
    /// whose SqlState is <see cref="LOCK_NOT_AVAILABLE"/>.
    /// </returns>
    internal static bool IsLockNotAvailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { SqlState: LOCK_NOT_AVAILABLE })
                return true;
        }

        return false;
    }

    /// <summary>
    /// Bounds every subsequent wait in the current transaction to the caller's
    /// budget.
    /// </summary>
    /// <param name="db">The context.</param>
    /// <param name="waitBudget">The longest the caller may block.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the budget has been applied.</returns>
    internal static async Task ApplyLockTimeoutAsync(
        DbContext db, TimeSpan waitBudget, CancellationToken ct)
    {
        var budget = Math.Max(1, (long)Math.Ceiling(waitBudget.TotalMilliseconds));

        await SetLockTimeoutAsync(
            db, budget.ToString(CultureInfo.InvariantCulture) + "ms", ct);
    }

    /// <summary>
    /// Sets the budget for the rest of the current transaction to a setting
    /// value the engine reported.
    /// </summary>
    /// <param name="db">The context.</param>
    /// <param name="setting">The value, in the form <c>current_setting</c> returns.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the setting has been applied.</returns>
    internal static async Task SetLockTimeoutAsync(
        DbContext db, string setting, CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync(_LOCK_TIMEOUT_SQL, [setting], ct);

    /// <summary>
    /// Reads the budget the current transaction is running under.
    /// </summary>
    /// <param name="db">The context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The value, in the form <c>current_setting</c> returns.</returns>
    internal static async Task<string> ReadLockTimeoutAsync(
        DbContext db, CancellationToken ct) =>
        await db.Database.SqlQueryRaw<string>(_READ_LOCK_TIMEOUT_SQL).SingleAsync(ct);

    /// <summary>
    /// Rolls the transaction back where there is still one to roll back.
    /// </summary>
    /// <remarks>
    /// The engine completes a transaction itself when it refuses its commit, and
    /// asking a completed transaction to roll back throws, hiding the failure that
    /// must reach the caller. The rollback runs on <see cref="CancellationToken.None"/>
    /// for the same reason <c>ReleaseQuietlyAsync</c> does: every call site is a
    /// catch arm that rethrows the original fault, or the release that runs in a
    /// <c>finally</c>, and honoring a token the caller had already canceled would
    /// replace that fault or surface a cancellation instead.
    /// </remarks>
    /// <param name="tx">The transaction.</param>
    /// <returns>A task that completes when the rollback has been attempted.</returns>
    internal static async Task RollbackQuietlyAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The transaction is already complete - the engine finishes one
            // itself as it refuses its commit. The caller still gets the failure
            // being rethrown around this, so failing to roll back a transaction
            // that no longer exists loses nothing.
        }
    }

    /// <summary>
    /// Releases the session-scoped advisory lock, and ends the session where the
    /// lock cannot be released any other way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine drops a session's advisory locks with its session, so a release
    /// that fails because the connection is gone has nothing left to release.
    /// </para>
    /// <para>
    /// A release that fails on a live connection leaves the lock held, and a
    /// pooled connection keeps its session until it is next opened. The usual
    /// cause is a transaction the work left open and failed, which refuses every
    /// statement but the one that ends it, so that transaction is ended and the
    /// release asked again. When the second release fails too, the session is
    /// ended.
    /// </para>
    /// <para>
    /// Every step runs on <see cref="CancellationToken.None"/>, and no step's
    /// <see cref="DbException"/> is rethrown: the release runs in a
    /// <c>finally</c>, where a throw would replace what the work returned or the
    /// fault the work raised.
    /// </para>
    /// </remarks>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <param name="lockClass">The advisory-lock class.</param>
    /// <param name="lockKey">The folded key.</param>
    /// <returns>A task that completes when the lock is released or the session ended.</returns>
    private static async Task ReleaseQuietlyAsync(DbContext db, int lockClass, int lockKey)
    {
        if (await TryUnlockAsync(db, lockClass, lockKey) || !IsConnectionOpen(db))
            return;

        await EndOpenTransactionQuietlyAsync(db);

        if (await TryUnlockAsync(db, lockClass, lockKey) || !IsConnectionOpen(db))
            return;

        try
        {
            await db.Database.ExecuteSqlRawAsync(_END_SESSION_SQL, CancellationToken.None);
        }
        catch (DbException)
        {
            // The engine reports the end of the session to the statement that
            // asked for it, so a failure here is the expected result.
        }
    }

    /// <summary>
    /// Runs the release statement and reports whether the engine accepted it.
    /// </summary>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <param name="lockClass">The advisory-lock class.</param>
    /// <param name="lockKey">The folded key.</param>
    /// <returns>
    /// <c>true</c> when the statement ran; <c>false</c> when the engine or the
    /// connection refused it.
    /// </returns>
    private static async Task<bool> TryUnlockAsync(DbContext db, int lockClass, int lockKey)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                _UNLOCK_SQL, [lockClass, lockKey], CancellationToken.None);

            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ends the transaction the session has open, whether the work opened it
    /// through the context or through a statement.
    /// </summary>
    /// <param name="db">The context whose connection holds the lock.</param>
    /// <returns>A task that completes when the transaction has been ended.</returns>
    private static async Task EndOpenTransactionQuietlyAsync(DbContext db)
    {
        try
        {
            if (db.Database.CurrentTransaction is { } leftOpen)
            {
                await RollbackQuietlyAsync(leftOpen);
                await leftOpen.DisposeAsync();
            }
            else
            {
                await db.Database.ExecuteSqlRawAsync(_ROLLBACK_SQL, CancellationToken.None);
            }
        }
        catch (DbException)
        {
            // The release is attempted again next either way, and its result decides
            // whether the session has to end.
        }
    }

    /// <summary>
    /// Reports whether the context's connection still has a session behind it.
    /// </summary>
    /// <param name="db">The context.</param>
    /// <returns><c>true</c> when the connection is open.</returns>
    private static bool IsConnectionOpen(DbContext db) =>
        db.Database.GetDbConnection().State == ConnectionState.Open;
}
