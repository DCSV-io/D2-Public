// -----------------------------------------------------------------------
// <copyright file="SampleOrderPlaced.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures.Fixtures;

/// <summary>
/// A test-only message body for the plaintext consumer golden. It is serialized
/// with <c>MessagingJsonOptions.Options</c>, so its wire keys are the camelCase
/// property names.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="CustomerRef">An opaque customer reference.</param>
/// <param name="AmountMinor">The amount in minor currency units.</param>
/// <param name="PlacedAt">The time the order was placed, as an ISO 8601 string.</param>
internal sealed record SampleOrderPlaced(
    string OrderId,
    string CustomerRef,
    long AmountMinor,
    string PlacedAt);
