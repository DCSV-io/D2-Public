// -----------------------------------------------------------------------
// <copyright file="DbFailureKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

/// <summary>
/// Provider-agnostic categorization of a database failure. Returned by
/// <see cref="IDbExceptionClassifier.Classify"/> when an exception thrown
/// during a DB operation matches a known failure shape.
/// </summary>
public enum DbFailureKind
{
    /// <summary>
    /// Optimistic-concurrency conflict - the row's version / timestamp /
    /// rowversion did not match. Recovery is to reload, merge and retry, or
    /// to report a user-facing "modified by someone else" error.
    /// </summary>
    ConcurrencyConflict,

    /// <summary>
    /// Unique-constraint violation - an INSERT / UPDATE produced a duplicate
    /// of a unique index. Typically mapped to a per-field user-facing
    /// error (e.g. "email already in use").
    /// </summary>
    UniqueViolation,

    /// <summary>
    /// Foreign-key-constraint violation - an INSERT / UPDATE referenced a
    /// row that does not exist, OR a DELETE / UPDATE was blocked by a
    /// referencing row. Typically mapped to "referenced item not
    /// found" or "still in use" depending on direction.
    /// </summary>
    ForeignKeyViolation,

    /// <summary>
    /// NOT NULL constraint violation - a column required a value but the
    /// statement supplied <c>NULL</c>. Usually a domain-validation gap;
    /// mapped to a per-field "required" error.
    /// </summary>
    NotNullViolation,

    /// <summary>
    /// CHECK constraint violation - a column-level or row-level CHECK
    /// expression rejected the row. Mapped to a domain-specific
    /// "invalid value" error.
    /// </summary>
    CheckViolation,

    /// <summary>
    /// Statement was canceled by a server-side timeout (statement_timeout,
    /// command timeout, lock_timeout, etc.). Distinct from a cancellation
    /// token firing on the calling side, which the base handler's
    /// <c>OperationCanceledException</c> path handles. Treated as transient.
    /// </summary>
    Timeout,

    /// <summary>
    /// Deadlock or serialization failure detected by the engine. The whole
    /// transaction was rolled back, so the whole operation may be retried
    /// safely. Distinct from <see cref="ConcurrencyConflict"/> in that
    /// no version field is involved - the engine itself resolved the conflict.
    /// </summary>
    Deadlock,

    /// <summary>
    /// Connection-level failure - the engine refused, the socket dropped,
    /// the pool was exhausted, the DB shut down mid-query. Treated as
    /// transient; a retry can succeed once the engine recovers.
    /// </summary>
    ConnectionFailure,
}
