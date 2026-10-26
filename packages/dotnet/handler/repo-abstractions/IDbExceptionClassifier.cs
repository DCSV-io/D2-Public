// -----------------------------------------------------------------------
// <copyright file="IDbExceptionClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using System;

/// <summary>
/// Provider-specific classifier for database exceptions. The base repo
/// handler uses an injected implementation to translate any
/// <see cref="Exception"/> thrown during a DB operation into a
/// provider-agnostic <see cref="DbFailureKind"/>.
/// </summary>
/// <remarks>
/// Each database provider has one implementation. PostgreSQL's is in
/// <c>DcsvIo.D2.Handler.Repo.Postgres</c>; an implementation for another
/// provider, such as SQL Server, SQLite or MySQL, registers the same
/// interface from its own package. The base repo handler stays
/// provider-agnostic.
/// <para>
/// Implementations MUST be thread-safe. The classifier is registered as a
/// DI singleton, so request threads invoke it concurrently. Stateless
/// designs (static helpers, no instance fields) satisfy the contract
/// trivially; if state is added, lock or use
/// <see cref="System.Collections.Concurrent"/> primitives.
/// </para>
/// </remarks>
public interface IDbExceptionClassifier
{
    /// <summary>
    /// Inspects <paramref name="exception"/> and returns the matching
    /// <see cref="DbFailureKind"/>, or <c>null</c> if the exception is not
    /// a recognized DB failure (it is then treated as an unhandled
    /// exception).
    /// </summary>
    /// <param name="exception">The exception thrown during a DB operation.</param>
    /// <returns>The classified failure kind, or <c>null</c>.</returns>
    DbFailureKind? Classify(Exception exception);
}
