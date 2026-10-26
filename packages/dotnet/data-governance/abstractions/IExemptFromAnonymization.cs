// -----------------------------------------------------------------------
// <copyright file="IExemptFromAnonymization.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Marks a financially or legally retained entity that the anonymization engine skips
/// entirely. No field on an exempt entity is ever overwritten - even if decorated with
/// <see cref="AnonymizableAttribute"/> - when an erasure sweep runs.
/// </summary>
/// <remarks>
/// This interface exempts records that must be preserved for compliance reasons (for
/// example invoices, audit logs and legal holds) from the sweep. Apply it to the entity
/// class; the engine detects it at model-build time and records every such entity type in
/// <see cref="AnonymizationOutcome.EntityTypesSkippedExempt"/>.
///
/// Like the caching marker interfaces (<c>ILocalCache</c>, <c>IDistributedCache</c> and
/// others), it is an empty marker whose meaning comes from its name alone.
/// </remarks>
public interface IExemptFromAnonymization
{
}
