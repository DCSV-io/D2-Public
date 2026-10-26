// -----------------------------------------------------------------------
// <copyright file="PgExceptionFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using System.Reflection;
using global::Npgsql;

/// <summary>
/// Builds <see cref="PostgresException"/> instances for tests. Npgsql 9.0.4
/// exposes two public constructors that take SqlState directly; this class
/// uses the smaller one, which takes the message text, the severity, the
/// invariant severity and the SqlState.
/// </summary>
internal static class PgExceptionFactory
{
    /// <summary>
    /// Constructs a <see cref="PostgresException"/> with the supplied
    /// SQLSTATE.
    /// </summary>
    /// <param name="sqlState">PostgreSQL SQLSTATE string.</param>
    /// <param name="severity">Severity (default ERROR).</param>
    /// <param name="messageText">Display message text.</param>
    /// <returns>A constructed <see cref="PostgresException"/>.</returns>
    public static PostgresException Create(
        string sqlState,
        string severity = "ERROR",
        string messageText = "test pg error")
    {
        return new PostgresException(
            messageText,
            severity,
            severity,
            sqlState);
    }

    /// <summary>
    /// Constructs a <see cref="PostgresException"/> with a specific SqlState
    /// and a specific InnerException. The classifier-precedence tests use it
    /// to check that pass 1 (SqlState) wins over pass 2 (network detection)
    /// when both apply. The public constructors take no InnerException
    /// argument, and PostgresException is not a wrapper exception by design,
    /// so this method sets the BCL Exception._innerException field by
    /// reflection.
    /// </summary>
    /// <param name="sqlState">PostgreSQL SQLSTATE string.</param>
    /// <param name="inner">Inner exception to attach via reflection.</param>
    /// <returns>The constructed exception with the inner attached.</returns>
    public static PostgresException CreateWithInner(string sqlState, Exception inner)
    {
        var ex = Create(sqlState);
        var innerField = typeof(Exception).GetField(
            "_innerException",
            BindingFlags.Instance | BindingFlags.NonPublic);
        innerField!.SetValue(ex, inner);
        return ex;
    }
}
