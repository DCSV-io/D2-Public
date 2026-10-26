// -----------------------------------------------------------------------
// <copyright file="IntegrationMessageFixtures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Collections.Generic;
using DcsvIo.D2.Messaging;

/// <summary>
/// The <see cref="MessagingCatalog"/> the integration message fixtures publish
/// and consume against. The generated registries carry only real domain
/// messages, so the fixture types under
/// <c>DcsvIo.D2.Tests.Integration.Messaging</c> declare their own constants and
/// this catalog carries their descriptors. A host registers it in place of the
/// framework default.
/// </summary>
internal static class IntegrationMessageFixtures
{
    /// <summary>
    /// The synthetic SYMMETRIC encryption domain the encrypted integration fixtures ride.
    /// The encryption domains that hosts register for real messages are SEALED
    /// (per-consumer-service asymmetric), so the symmetric publish/consume path is
    /// exercised on a test-seam domain - unknown to the generated catalog, therefore
    /// Symmetric by the documented <c>EncryptionDomainModes.ModeFor</c> default.
    /// </summary>
    public const string SYMMETRIC_FIXTURE_DOMAIN = "payload-fixture-symmetric";

    /// <summary>Constant on <see cref="IntegrationAuditEvent"/>.</summary>
    public const string INTEGRATION_AUDIT = "IntegrationAudit";

    /// <summary>Constant on <see cref="IntegrationPlaintextEvent"/>.</summary>
    public const string INTEGRATION_PLAINTEXT = "IntegrationPlaintext";

    /// <summary>Constant on <see cref="BroadcastFixtureEvent"/>.</summary>
    public const string INTEGRATION_BROADCAST = "IntegrationBroadcast";

    /// <summary>Gets the catalog carrying
    /// <see cref="IntegrationAuditEvent"/> (encrypted, symmetric fixture domain),
    /// <see cref="IntegrationPlaintextEvent"/> (plaintext topic), and
    /// <see cref="BroadcastFixtureEvent"/> (plaintext fanout).</summary>
    public static MessagingCatalog Catalog { get; } = new(
        new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal)
        {
            [INTEGRATION_AUDIT] = new MqMessageDescriptor(
                Constant: INTEGRATION_AUDIT,
                MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
                Exchange: "d2.test.integration-audit",
                ExchangeType: "topic",
                Encryption: SYMMETRIC_FIXTURE_DOMAIN,
                EncryptionReason: null,
                DefaultRoutingKey: string.Empty),
            [INTEGRATION_PLAINTEXT] = new MqMessageDescriptor(
                Constant: INTEGRATION_PLAINTEXT,
                MessageTypeName: typeof(IntegrationPlaintextEvent).FullName!,
                Exchange: "d2.test.integration-plaintext",
                ExchangeType: "topic",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "Integration fixture exercising the plaintext code path.",
                DefaultRoutingKey: string.Empty),
            [INTEGRATION_BROADCAST] = new MqMessageDescriptor(
                Constant: INTEGRATION_BROADCAST,
                MessageTypeName: typeof(BroadcastFixtureEvent).FullName!,
                Exchange: "d2.test.integration-broadcast",
                ExchangeType: "fanout",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason:
                    "Integration fixture exercising the plaintext fanout broadcast path.",
                DefaultRoutingKey: string.Empty),
        },
        new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal));
}
