// -----------------------------------------------------------------------
// <copyright file="IntegrationAuditEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Messaging;

/// <summary>Integration message fixture exercising the encrypted code path.
/// Its descriptor (exchange, encryption domain) lives in
/// <see cref="IntegrationMessageFixtures.Catalog"/>, which a fixture host
/// registers in place of the framework default - the generated registries
/// only cover real domain messages.</summary>
[MqPub(IntegrationMessageFixtures.INTEGRATION_AUDIT)]
public sealed class IntegrationAuditEvent
{
    /// <summary>Gets or sets the test correlation marker.</summary>
    public string? Marker { get; set; }
}
