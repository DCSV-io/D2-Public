// -----------------------------------------------------------------------
// <copyright file="SampleOutboxIntegrationPayload.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox.Fixtures;

/// <summary>
/// A consumer's payload, serialized by the staging seam and stored in the
/// <c>jsonb</c> column.
/// </summary>
/// <param name="Detail">A member present on the wire.</param>
internal sealed record SampleOutboxIntegrationPayload(string Detail);
