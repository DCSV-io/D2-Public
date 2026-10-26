// -----------------------------------------------------------------------
// <copyright file="IntegrationPlaintextEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Messaging;

/// <summary>Integration message fixture exercising the plaintext code path.
/// Its descriptor lives in <see cref="IntegrationMessageFixtures.Catalog"/> -
/// see <see cref="IntegrationAuditEvent"/> for the rationale.</summary>
[MqPub(IntegrationMessageFixtures.INTEGRATION_PLAINTEXT)]
public sealed class IntegrationPlaintextEvent
{
    /// <summary>Gets or sets the test correlation marker.</summary>
    public string? Marker { get; set; }
}
