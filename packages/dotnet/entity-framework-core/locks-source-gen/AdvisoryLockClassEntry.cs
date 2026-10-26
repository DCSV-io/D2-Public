// -----------------------------------------------------------------------
// <copyright file="AdvisoryLockClassEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

/// <summary>
/// One two-integer advisory-lock class parsed from a host-supplied
/// <c>advisory-locks.spec.json</c> AdditionalFiles catalog.
/// </summary>
/// <remarks>
/// PostgreSQL tags a two-integer advisory lock differently from a single-bigint
/// one, so a class value and a single-bigint key never identify the same lock. The
/// second integer is supplied at runtime, usually folded from a row
/// identifier, so only the class is registered here.
/// </remarks>
/// <param name="ConstName">UPPER_SNAKE_CASE C# constant identifier.</param>
/// <param name="Database">
/// Owning database. The uniqueness check is per database: the same class in two
/// different databases is legal.
/// </param>
/// <param name="Class">
/// Explicit signed 32-bit integer passed as the first argument of
/// <c>pg_advisory_lock(class, key)</c>.
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record AdvisoryLockClassEntry(
    string ConstName,
    string Database,
    int Class,
    string Doc);
