// -----------------------------------------------------------------------
// <copyright file="AnonymizationClassifierSerialCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance;

using Xunit;

/// <summary>
/// xUnit collection definition covering these test classes, which call
/// <c>AnonymizationTierClassifier.ClearCache()</c> and assert exact
/// <c>CacheCount</c> values against the process-global static
/// <c>ConcurrentDictionary</c> inside <c>AnonymizationTierClassifier</c>:
/// <c>AnonymizationTierClassifierTests</c>, <c>AnonymizationModelValidatorTests</c>,
/// <c>AnonymizationEngineUnitTests</c> and <c>AnonymizationModelValidatorBootTests</c>.
/// <para>
/// Those are its whole scope. The remaining callers of <c>ClearCache()</c> -
/// <c>AnonymizationEngineIntegrationTests</c>, <c>AnonymizationEngineGapTests</c>,
/// <c>AnonymizationEngineDualVoRegressionTests</c>,
/// <c>AnonymizationEngineTierAConverterRegressionTests</c>,
/// <c>InvoiceAnonymizationIntegrationTests</c> and <c>ContactHostIntegrationTests</c> -
/// are in the <c>Postgres</c> collection, which this definition does not cover.
/// The scheduler keeps them from interfering, not this collection: classes
/// in one collection never run concurrently, and a collection carrying
/// <c>DisableParallelization = true</c> runs only once every parallelizable
/// collection has finished, so the <c>Postgres</c> classes are done before any class
/// here starts.
/// </para>
/// <para>
/// The EF Core model is immutable after build and a singleton per <c>DbContext</c>
/// type, so the process-global cache is correct in production, where no test-only
/// <c>ClearCache()</c> call happens.
/// </para>
/// </summary>
[CollectionDefinition("AnonymizationClassifierSerial", DisableParallelization = true)]
public sealed class AnonymizationClassifierSerialCollectionDefinition
{
}
