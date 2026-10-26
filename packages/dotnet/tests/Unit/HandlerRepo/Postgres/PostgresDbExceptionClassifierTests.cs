// -----------------------------------------------------------------------
// <copyright file="PostgresDbExceptionClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Handler.Repo.Postgres;
using global::Npgsql;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Adversarial tests of the PostgreSQL SQLSTATE -> DbFailureKind matrix and
/// of every wrapper and exception shape that the classifier walks.
/// </summary>
/// <remarks>
/// A misclassification in the classifier has one of two effects. A programmer
/// error classified as a transient failure is retried. A recoverable failure
/// that falls through as UnhandledException is not retried. The tests below
/// assert every behavior that the XML doc of PostgresDbExceptionClassifier
/// lists.
/// </remarks>
public sealed class PostgresDbExceptionClassifierTests
{
    private readonly PostgresDbExceptionClassifier _sut = new();

    // ----------------------------------------------------------------------
    // Pass 1: SQLSTATE matrix
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("23505", DbFailureKind.UniqueViolation)]

    // exclusion_violation - deferrable EXCLUDE breach -> same 409 Conflict as unique_violation
    [InlineData("23P01", DbFailureKind.UniqueViolation)]
    [InlineData("23503", DbFailureKind.ForeignKeyViolation)]
    [InlineData("23502", DbFailureKind.NotNullViolation)]
    [InlineData("23514", DbFailureKind.CheckViolation)]
    [InlineData("40001", DbFailureKind.Deadlock)] // serialization_failure
    [InlineData("40P01", DbFailureKind.Deadlock)] // deadlock_detected
    [InlineData("57014", DbFailureKind.Timeout)] // query_canceled (server statement_timeout)
    [InlineData("57P03", DbFailureKind.ConnectionFailure)] // cannot_connect_now
    [InlineData("53300", DbFailureKind.ConnectionFailure)] // too_many_connections
    public void Classify_RawPgException_KnownSqlState_ReturnsExpectedKind(
        string sqlState,
        DbFailureKind expected)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // 08xxx connection-exception class (StartsWith dispatch)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("08000")] // connection_exception
    [InlineData("08001")] // sqlclient_unable_to_establish_sqlconnection
    [InlineData("08003")] // connection_does_not_exist
    [InlineData("08006")] // connection_failure
    [InlineData("08P01")] // protocol_violation (still 08-class)
    public void Classify_RawPgException_ConnectionExceptionClass_ReturnsConnectionFailure(
        string sqlState)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    // ----------------------------------------------------------------------
    // Unrecognized SQLSTATE -> pass 2 network check -> null
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("42601")] // syntax_error - programmer error
    [InlineData("42P01")] // undefined_table - programmer error
    [InlineData("XX000")] // internal_error
    [InlineData("00000")] // successful_completion - a real exception never carries it
    public void Classify_RawPgException_UnknownSqlState_ReturnsNull(string sqlState)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().BeNull(
            "unknown SQLSTATE - surface as UnhandledException so ops sees the bug");
    }

    // ----------------------------------------------------------------------
    // Wrapper unwrapping: DbUpdateException, AggregateException, TIE
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_DbUpdateExceptionWrappingPg_Unwraps_AndClassifies()
    {
        var pg = PgExceptionFactory.Create("23505");
        var ex = new DbUpdateException("update failed", pg);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public void Classify_AggregateExceptionWrappingDbUpdateWrappingPg_Unwraps()
    {
        // 3-level chain: AggregateException -> DbUpdateException -> PG.
        // The classifier unwraps both wrapper layers; the PostgreSQL
        // exception is at depth 2.
        var pg = PgExceptionFactory.Create("23505");
        var dbu = new DbUpdateException("update failed", pg);
        var agg = new AggregateException("agg", dbu);

        var kind = _sut.Classify(agg);

        kind.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public void Classify_TargetInvocationExceptionWrappingPg_Unwraps()
    {
        // 2-level chain: TargetInvocationException, the wrapper that
        // reflection calls throw, around PG.
        var pg = PgExceptionFactory.Create("23503");
        var tie = new TargetInvocationException(pg);

        var kind = _sut.Classify(tie);

        kind.Should().Be(DbFailureKind.ForeignKeyViolation);
    }

    [Fact]
    public void Classify_PgAtDepth9_InsideMixedWrapperChain_StillFinds()
    {
        // Boundary: the classifier reaches depth 9. A PostgreSQL exception
        // buried that deep is still found.
        var pg = PgExceptionFactory.Create("40P01");
        var wrap = WrapNTimes(pg, 9);

        var kind = _sut.Classify(wrap);

        kind.Should().Be(DbFailureKind.Deadlock);
    }

    [Fact]
    public void Classify_PgBeyondDepth10_ReturnsNull()
    {
        // Boundary: the classifier stops at depth 10, so a PostgreSQL
        // exception beyond it is not found and the result is null, which
        // surfaces downstream as UnhandledException. A wrapper chain this
        // deep is almost certainly a bug that needs investigation, and the
        // result is deliberate so that it is not retried.
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 11);

        var kind = _sut.Classify(wrap);

        kind.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Pass 2: network-level (SocketException / IOException) detection
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_BareNpgsqlExceptionWithSocketInner_ReturnsConnectionFailure()
    {
        var inner = new SocketException((int)SocketError.ConnectionRefused);
        var ex = new NpgsqlException("connection refused", inner);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareNpgsqlExceptionWithIOWrappingSocket_ReturnsConnectionFailure()
    {
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var io = new IOException("stream closed", sock);
        var ex = new NpgsqlException("network drop", io);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareExceptionWithIoInner_ReturnsConnectionFailure()
    {
        // A non-NpgsqlException that wraps an IOException is also classified
        // as ConnectionFailure, because the network pass searches the chain
        // of any exception, not only NpgsqlException chains.
        var io = new IOException("network error");
        var ex = new InvalidOperationException("wrap", io);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareNpgsqlExceptionWithNoInner_ReturnsNull()
    {
        // A bare NpgsqlException with no inner cause returns null. It
        // surfaces as a programmer or configuration failure that needs
        // investigation.
        var ex = new NpgsqlException("bare npgsql, no inner");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull(
            "bare NpgsqlException without recognizable inner cause is NOT a transient failure");
    }

    [Fact]
    public void Classify_PlainArgumentException_ReturnsNull()
    {
        // A programmer error is not classified as any database failure.
        var ex = new ArgumentException("bad arg");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_PlainBclTimeoutException_ReturnsNull()
    {
        // The classifier does not classify a BCL TimeoutException. Only
        // PostgreSQL SQLSTATE 57014 counts as a database timeout.
        // BaseHandler (public/packages/dotnet/handler/core/BaseHandler.cs)
        // catches OperationCanceledException and has no TimeoutException
        // catch.
        var ex = new TimeoutException("waited too long");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_NullException_ThrowsArgumentNullException()
    {
        Action act = () => _sut.Classify(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----------------------------------------------------------------------
    // Precedence: SQLSTATE pass 1 wins over pass-2 network detection
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_PgWithSqlStateAndSocketInner_PrefersSqlStateClassification()
    {
        // Precedence: an exception that matches both pass 1 (PostgreSQL
        // exception with a SqlState) and pass 2 (SocketException in the
        // chain) is classified by pass 1, because the server reported the
        // SQLSTATE itself.
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var pg = PgExceptionFactory.CreateWithInner("23505", sock);

        var kind = _sut.Classify(pg);

        kind.Should().Be(
            DbFailureKind.UniqueViolation,
            "SQLSTATE 23505 must beat the SocketException-in-chain detection");
    }

    [Fact]
    public void Classify_DbUpdateWrappingPgWithUnknownSqlState_FallsThroughToNetworkPass()
    {
        // When the inner PostgreSQL exception has an unknown SQLSTATE, pass 1
        // returns null and the classifier runs pass 2. Pass 2 finds nothing
        // in the chain, so the result is null.
        var pg = PgExceptionFactory.Create("XX000");
        var wrap = new DbUpdateException("wrap", pg);

        var kind = _sut.Classify(wrap);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_AggregateWithUnknownPgAndSocketSibling_DetectsNetwork()
    {
        // An AggregateException wraps a PostgreSQL exception with an unknown
        // SqlState, which does not classify on its own, and a SocketException
        // sibling at InnerExceptions[1]. The classifier must search every
        // branch of AggregateException.InnerExceptions and not only the first
        // one through Exception.InnerException. Otherwise it misses a connection
        // drop that EF or parallel batch operations surface as the second
        // inner exception.
        var pg = PgExceptionFactory.Create("XX000");
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var agg = new AggregateException("agg", pg, sock);

        var kind = _sut.Classify(agg);

        // Pass 1 finds pg first, but XX000 does not classify, so it returns
        // null. Pass 2 searches for a SocketException, descends every branch
        // of the AggregateException and finds the socket sibling.
        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static Exception WrapNTimes(Exception inner, int times)
    {
        var current = inner;
        for (var i = 0; i < times; i++)
            current = new InvalidOperationException($"wrap-{i}", current);
        return current;
    }
}
