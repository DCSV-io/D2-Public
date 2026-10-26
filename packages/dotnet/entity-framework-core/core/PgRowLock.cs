// -----------------------------------------------------------------------
// <copyright file="PgRowLock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore;

using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Pessimistic <c>SELECT ... FOR UPDATE</c> on one row, with the table and the
/// column resolved from the EF model.
/// </summary>
/// <remarks>
/// <para>
/// The statements are PostgreSQL - <c>FOR UPDATE</c>, <c>FOR UPDATE NOWAIT</c>
/// and <c>lock_timeout</c> - and this type ships from the provider-neutral
/// package anyway. It needs no driver type: the SQL crosses as text on the EF
/// context, the identifiers come from the provider's
/// <see cref="ISqlGenerationHelper"/>, and the refusal is read from
/// <see cref="DbException.SqlState"/>.
/// </para>
/// <para>
/// SQL has no parameter form for a table or a column name, so both are
/// interpolated. Resolved from the model they are not caller input, and they are
/// delimited through the provider's <see cref="ISqlGenerationHelper"/> rather
/// than hand-quoted: PostgreSQL folds an unquoted identifier to lower case, so a
/// PascalCase column or a reserved word would otherwise break against a schema
/// that uses one.
/// </para>
/// <para>
/// A non-relational provider runs the work directly. The serialization is
/// asserted against a real database, the only place it can be asserted.
/// </para>
/// </remarks>
public static class PgRowLock
{
    /// <remarks>
    /// The value is selected under a column alias rather than discarded, because
    /// the caller has to know whether the statement matched a row: a
    /// <c>FOR UPDATE</c> that matched nothing locked nothing.
    /// </remarks>
    private const string _LOCK_SQL =
        "SELECT 1 AS \"Value\" FROM {0} WHERE {1} = {{0}} FOR UPDATE";

    /// <summary>The <c>NOWAIT</c> form of <see cref="_LOCK_SQL"/>.</summary>
    private const string _LOCK_NOWAIT_SQL =
        "SELECT 1 AS \"Value\" FROM {0} WHERE {1} = {{0}} FOR UPDATE NOWAIT";

    /// <summary>
    /// Runs <paramref name="work"/> with one row of <typeparamref name="TEntity"/>
    /// locked, where the wait for that row is bounded by
    /// <paramref name="waitBudget"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the context already has a transaction, this helper enlists in it
    /// rather than opening a second one, and a refused row lock aborts that
    /// transaction - so the refusal is thrown here and reported by whoever
    /// opened the transaction. When the context has none, this helper opens its
    /// own, commits it after the work, and reports the refusal itself.
    /// </para>
    /// <para>
    /// The budget is applied as <c>lock_timeout</c>, which the engine keeps for
    /// the rest of the transaction. On the enlisting branch that transaction
    /// belongs to the caller, so this helper reads the value in force before it
    /// changes it and puts that value back once the row is held. The caller's
    /// later waits inside its own transaction are bounded as they were.
    /// </para>
    /// <para>
    /// A budget of zero or less asks the engine to refuse rather than queue at
    /// all, and bounds only that attempt on the row: the statement takes it with
    /// <c>NOWAIT</c> and no <c>lock_timeout</c> is set, because zero disables it
    /// in PostgreSQL, so a wait the work takes is bounded by whatever the
    /// transaction already runs under.
    /// </para>
    /// <para>
    /// On the branch where this helper opens the transaction, a throw from the
    /// work or the commit rolls the transaction back and clears the context's
    /// change tracker before the fault is rethrown, for the reason the returns
    /// text gives for a refused wait. A fault raised before the work started
    /// leaves the tracker as it was.
    /// </para>
    /// <para>
    /// <b>A row that does not exist locks nothing</b>, so the work does not run
    /// and this returns <c>false</c>. <c>SELECT ... FOR UPDATE</c> can only hold
    /// a row that is there; two callers locking a row neither has created yet
    /// would otherwise both proceed with no lock between them, which is the race
    /// a row lock is taken to prevent. A caller that needs to serialize the
    /// creation of the row takes a lock that exists without it -
    /// <see cref="PgClassedAdvisoryLock"/> on the identifier.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEntity">The mapped entity whose row is locked.</typeparam>
    /// <param name="db">The context.</param>
    /// <param name="keyProperty">The property that identifies the key column.</param>
    /// <param name="key">The row to lock.</param>
    /// <param name="work">The work to run under the lock. It owns its own saves.</param>
    /// <param name="waitBudget">The longest this caller may block on the row.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <para>
    /// <c>true</c> when the row was held and the work ran.
    /// </para>
    /// <para>
    /// On the branch where this helper opens the transaction, <c>false</c> means
    /// either that no row has the key or that a wait reached the budget, and the
    /// result does not say which. A positive budget is applied as
    /// <c>lock_timeout</c> for the rest of that transaction, so the wait that
    /// reached it is this row's or one the work itself took; either way the
    /// transaction rolled back and nothing it staged exists. With a budget of
    /// zero or less the only wait the budget refuses is this row's. On a wait that
    /// reached the budget the context's change tracker is cleared as well,
    /// including entities tracked before the call: the rollback undid every save
    /// the work made, so a later save on the same context writes none of the
    /// refused work. A <c>false</c> for an absent row leaves the tracker as it
    /// was, because the work did not run.
    /// </para>
    /// <para>
    /// On the enlisting branch the helper puts the caller's previous
    /// <c>lock_timeout</c> back as soon as the row is held, so the budget bounds
    /// the row wait alone and not the work. <c>false</c> there means no row
    /// has the key; a wait the work itself takes is bounded by whatever the
    /// caller's transaction was already running under, and a refusal under that
    /// bound is thrown rather than returned.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the context, the key expression or the work is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the key expression is not a direct property access on the
    /// entity parameter.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> is not a mapped table entity, or
    /// when the key expression does not name one of its mapped properties.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown on the enlisting branch when the engine refuses the wait. The
    /// refusal aborts the caller's transaction, so there is nothing left for this
    /// helper to return <c>false</c> from, and the refusal reaches the caller that
    /// opened the transaction. Only the branch where this helper opened the
    /// transaction itself returns <c>false</c> for a refusal.
    /// </exception>
    public static Task<bool> TryExecuteLockedAsync<TEntity>(
        DbContext db,
        Expression<Func<TEntity, Guid>> keyProperty,
        Guid key,
        Func<CancellationToken, Task> work,
        TimeSpan waitBudget,
        CancellationToken ct)
        where TEntity : class
        => RanAsync(RunAsync(db, keyProperty, key, work, waitBudget, ct));

    /// <summary>
    /// Runs <paramref name="work"/> with one row of <typeparamref name="TEntity"/>
    /// locked, waiting for that row until the caller's token is canceled or the
    /// context's command timeout elapses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The waiting sibling of <see cref="TryExecuteLockedAsync{TEntity}"/>, for a
    /// caller whose result depends on getting the row rather than on how long it
    /// took. It sets no <c>lock_timeout</c>, so it neither bounds the wait nor
    /// disturbs a bound the caller's own transaction is already running under.
    /// </para>
    /// <para>
    /// It enlists in the context's transaction where there is one and opens its
    /// own where there is not, exactly as the budgeted form does. A refusal the
    /// engine reports under a bound somebody else set is thrown rather than
    /// returned, because this form has no return value for it.
    /// </para>
    /// <para>
    /// <b>A row that does not exist locks nothing, and this form runs the work
    /// anyway</b>, returning <c>false</c> so the caller can tell a serialized run
    /// from an unserialized one. The caller must establish that the row exists -
    /// inside the same transaction, or by holding a lock that does not depend on
    /// it - before asking for serialization on it.
    /// Two callers locking a row neither has created yet both proceed here with
    /// nothing between them, and <see cref="PgClassedAdvisoryLock"/> on the
    /// identifier is the lock that exists whether or not its row does. The
    /// budgeted sibling refuses instead: there an absent row is <c>false</c> and
    /// the work does not run.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEntity">The mapped entity whose row is locked.</typeparam>
    /// <param name="db">The context.</param>
    /// <param name="keyProperty">The property that identifies the key column.</param>
    /// <param name="key">The row to lock.</param>
    /// <param name="work">The work to run under the lock. It owns its own saves.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the row was held for the work, <c>false</c> when no row
    /// has the key. The work ran either way; the result says whether it ran
    /// serialized. A caller that does not care may ignore it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the context, the key expression or the work is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the key expression is not a direct property access on the
    /// entity parameter.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> is not a mapped table entity, or
    /// when the key expression does not name one of its mapped properties. Also
    /// thrown on the branch where this helper opens the transaction when the wait
    /// reaches the context's command timeout: the execution strategy wraps the
    /// provider's timeout in it - with Npgsql, an <c>NpgsqlException</c> whose
    /// inner exception is a <see cref="TimeoutException"/> - and the work does
    /// not run.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the engine refuses the wait under a <c>lock_timeout</c> the
    /// caller's own transaction was already running under. This form sets none
    /// and has no return value for a refusal, so the refusal reaches whoever set the
    /// bound. Also thrown on the enlisting branch when the wait reaches the
    /// context's command timeout - with Npgsql, an <c>NpgsqlException</c> whose
    /// inner exception is a <see cref="TimeoutException"/>.
    /// </exception>
    public static async Task<bool> ExecuteLockedAsync<TEntity>(
        DbContext db,
        Expression<Func<TEntity, Guid>> keyProperty,
        Guid key,
        Func<CancellationToken, Task> work,
        CancellationToken ct)
        where TEntity : class
        => (await RunAsync(db, keyProperty, key, work, waitBudget: null, ct)).Held;

    /// <summary>
    /// Renders the lock statement for <typeparamref name="TEntity"/>, with the
    /// table and the key column delimited by the provider.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity whose row is locked.</typeparam>
    /// <param name="db">The context.</param>
    /// <param name="keyProperty">The property that identifies the key column.</param>
    /// <param name="waitBudget">
    /// The caller's budget; zero or less selects NOWAIT, and null waits.
    /// </param>
    /// <returns>The statement, with one positional parameter placeholder for the key.</returns>
    internal static string BuildLockSql<TEntity>(
        DbContext db,
        Expression<Func<TEntity, Guid>> keyProperty,
        TimeSpan? waitBudget)
        where TEntity : class
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"'{typeof(TEntity).Name}' is not part of the model of "
                + $"'{db.GetType().Name}', so it has no table to lock.");

        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"'{typeof(TEntity).Name}' is mapped to no table, so it has no row to lock.");

        var schema = entityType.GetSchema();
        var propertyName = PropertyNameOf(keyProperty);

        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"'{typeof(TEntity).Name}' has no mapped property '{propertyName}'.");

        var storeObject = StoreObjectIdentifier.Table(tableName, schema);

        var columnName = property.GetColumnName(storeObject)
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' of '{typeof(TEntity).Name}' is mapped to no "
                + $"column of table '{tableName}'.");

        var helper = db.GetService<ISqlGenerationHelper>();
        var table = helper.DelimitIdentifier(tableName, schema);
        var column = helper.DelimitIdentifier(columnName);

        var template = waitBudget is not { } budget || budget > TimeSpan.Zero
            ? _LOCK_SQL
            : _LOCK_NOWAIT_SQL;

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture, template, table, column);
    }

    /// <summary>
    /// Reads the property name out of a <c>e =&gt; e.Member</c> expression.
    /// </summary>
    /// <typeparam name="TEntity">The entity the expression is over.</typeparam>
    /// <param name="keyProperty">The expression.</param>
    /// <returns>The named property.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the expression is not a direct member access on the parameter.
    /// </exception>
    internal static string PropertyNameOf<TEntity>(Expression<Func<TEntity, Guid>> keyProperty)
    {
        var body = keyProperty.Body;

        if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
            body = convert.Operand;

        if (body is MemberExpression { Expression: ParameterExpression } member)
            return member.Member.Name;

        throw new ArgumentException(
            "The key expression must be a direct property access on the entity parameter, "
            + "as in 'e => e.Id'.",
            nameof(keyProperty));
    }

    /// <summary>
    /// The body both public forms take, with the budget absent on the waiting one.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity whose row is locked.</typeparam>
    /// <param name="db">The context.</param>
    /// <param name="keyProperty">The property that identifies the key column.</param>
    /// <param name="key">The row to lock.</param>
    /// <param name="work">The work to run under the lock.</param>
    /// <param name="waitBudget">
    /// The longest this caller may block on the row, or null to wait until the
    /// token is canceled or the context's command timeout elapses.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ran</c> is whether the work ran, which a budgeted caller reads;
    /// <c>Held</c> is whether a row was there to lock, which the waiting caller
    /// reads. On a provider with no row locks both are <c>true</c>.
    /// </returns>
    private static async Task<(bool Ran, bool Held)> RunAsync<TEntity>(
        DbContext db,
        Expression<Func<TEntity, Guid>> keyProperty,
        Guid key,
        Func<CancellationToken, Task> work,
        TimeSpan? waitBudget,
        CancellationToken ct)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(keyProperty);
        ArgumentNullException.ThrowIfNull(work);

        if (!db.Database.IsRelational())
        {
            // A provider with no row locks has no row to hold, and the work is
            // the only thing left to do. Both results say so.
            await work(ct);

            return (Ran: true, Held: true);
        }

        var sql = BuildLockSql(db, keyProperty, waitBudget);

        // Only the bounded form refuses an absent row. The waiting form runs
        // the work either way and reports whether the row was held, so its
        // caller decides what an absent row means.
        var refuseAbsentRow = waitBudget is not null;

        if (db.Database.CurrentTransaction is not null)
        {
            var enlistedHeld = await TakeRowAsync(
                db, sql, key, waitBudget, restoreBudget: true, ct);

            if (!enlistedHeld && refuseAbsentRow)
                return (Ran: false, Held: false);

            await work(ct);

            return (Ran: true, Held: enlistedHeld);
        }

        var strategy = db.Database.CreateExecutionStrategy();
        var ran = false;
        var rowHeld = false;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var workStarted = false;

            try
            {
                var held = await TakeRowAsync(db, sql, key, waitBudget, restoreBudget: false, ct);
                rowHeld = held;

                if (!held && refuseAbsentRow)
                {
                    // No such row, so nothing was locked and nothing ran. The
                    // transaction holds no work to keep.
                    await PgClassedAdvisoryLock.RollbackQuietlyAsync(tx);
                    ran = false;

                    return;
                }

                workStarted = true;
                await work(ct);
                await tx.CommitAsync(ct);
                ran = true;
            }
            catch (Exception refused)
                when (waitBudget is not null
                    && PgClassedAdvisoryLock.IsLockNotAvailable(refused))
            {
                // The context still tracks what the work added, changed or
                // saved inside the rolled-back transaction, and a later save on
                // it would write that outside the lock.
                await PgClassedAdvisoryLock.RollbackQuietlyAsync(tx);
                db.ChangeTracker.Clear();
                ran = false;
                rowHeld = false;
            }
            catch
            {
                // The rollback undid whatever the work saved, so the context
                // would otherwise track rows the database no longer holds.
                await PgClassedAdvisoryLock.RollbackQuietlyAsync(tx);

                if (workStarted)
                    db.ChangeTracker.Clear();

                throw;
            }
        });

        return (ran, rowHeld);
    }

    /// <summary>
    /// Projects the run's result down to whether the work ran, for the budgeted
    /// form whose contract is that one fact.
    /// </summary>
    /// <param name="run">The running lock-and-work.</param>
    /// <returns>Whether the work ran.</returns>
    private static async Task<bool> RanAsync(Task<(bool Ran, bool Held)> run)
        => (await run).Ran;

    /// <summary>
    /// Takes the row lock inside whatever transaction is open and reports
    /// whether a row was there to lock.
    /// </summary>
    /// <param name="db">The context.</param>
    /// <param name="sql">The rendered lock statement.</param>
    /// <param name="key">The row to lock.</param>
    /// <param name="waitBudget">
    /// The longest this caller may block on the row, or null to wait and leave
    /// <c>lock_timeout</c> as it stands.
    /// </param>
    /// <param name="restoreBudget">
    /// Whether to put the transaction's previous <c>lock_timeout</c> back once
    /// the row is held. True on the branch that enlists in a transaction this
    /// helper did not open, where the setting would otherwise bound waits the
    /// caller takes afterwards.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the statement matched a row and now holds it;
    /// <c>false</c> when no row has the key, in which case nothing is locked.
    /// </returns>
    private static async Task<bool> TakeRowAsync(
        DbContext db,
        string sql,
        Guid key,
        TimeSpan? waitBudget,
        bool restoreBudget,
        CancellationToken ct)
    {
        string? previousBudget = null;

        if (waitBudget is { } budget && budget > TimeSpan.Zero)
        {
            if (restoreBudget)
                previousBudget = await PgClassedAdvisoryLock.ReadLockTimeoutAsync(db, ct);

            await PgClassedAdvisoryLock.ApplyLockTimeoutAsync(db, budget, ct);
        }

        List<int> matched = [];

        try
        {
            matched = await db.Database.SqlQueryRaw<int>(sql, key).ToListAsync(ct);
        }
        catch (InvalidOperationException wrapped)
            when (wrapped.InnerException is DbException refused)
        {
            // The query pipeline wraps a provider exception it classes as
            // transient, and a refused lock is one of those. A caller reads the
            // refusal off DbException.SqlState, so the provider exception has to
            // reach it - with the stack it was thrown on rather than a new one.
            ExceptionDispatchInfo.Throw(refused);
        }

        // A refused lock aborts the transaction and never reaches here, so the
        // restore runs only where there is a transaction left to restore it in.
        if (previousBudget is not null)
            await PgClassedAdvisoryLock.SetLockTimeoutAsync(db, previousBudget, ct);

        return matched.Count > 0;
    }
}
