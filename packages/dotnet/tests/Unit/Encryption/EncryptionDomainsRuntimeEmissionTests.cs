// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the public EncryptionDomains closed-set
/// catalog (plaintext + framework fixture sealed domain). Sealed domains that a
/// host registers outside the public catalog are declared in the host's own
/// catalog and are not pinned here.
/// </summary>
public sealed class EncryptionDomainsRuntimeEmissionTests
{
    [Theory]
    [InlineData("plaintext")]
    [InlineData("payload-fixture-sealed")]
    public void EveryCatalogValue_HasMatchingConstant(string expectedValue)
    {
        EncryptionDomains.AllDomains.Should().Contain(expectedValue);
    }

    [Fact]
    public void PublicCatalog_HoldsExactlyThePublicSpecValuesInSpecOrder()
    {
        var specPath = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "encryption-domains",
            "encryption-domains.spec.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(specPath));
        var specValues = doc.RootElement.GetProperty("domains").EnumerateArray()
            .Select(domain => domain.GetProperty("value").GetString())
            .ToList();

        EncryptionDomains.AllDomains.Should().Equal(specValues);
    }

    [Fact]
    public void FixtureSealedDomain_RegistersResolvableKeyedSealerPathConstant()
    {
        // The public catalog contains one framework fixture sealed domain, so the sealed
        // ModeFor and compose paths stay unit-testable without a product domain.
        EncryptionDomains.FIXTURE_SEALED.Should().Be("payload-fixture-sealed");
        EncryptionDomainModes.ModeFor(EncryptionDomains.FIXTURE_SEALED)
            .Should().Be(EncryptionDomainMode.Sealed);
    }

    [Fact]
    public void PlaintextDomain_FlowsThroughMqMessageDescriptor()
    {
        // The PLAINTEXT sentinel does not register a keyed IPayloadCrypto.
        // Its production emit site is mq-messages.spec entries with
        // "encryption": "plaintext".
        var descriptor = new DcsvIo.D2.Messaging.MqMessageDescriptor(
            Constant: "TestPlaintext",
            MessageTypeName: typeof(object).FullName!,
            Exchange: "d2.test.plaintext",
            ExchangeType: "fanout",
            Encryption: EncryptionDomains.PLAINTEXT,
            EncryptionReason: "Test fixture only.",
            DefaultRoutingKey: string.Empty);

        descriptor.IsPlaintext.Should().BeTrue(
            "the PLAINTEXT catalog value must flow through to the "
            + "wire-resolver's plaintext-detection helper byte-for-byte");
    }

    [Fact]
    public void AllDomains_EnumeratesExactlyThePublicSpecCatalogValues()
    {
        EncryptionDomains.AllDomains
            .Should().BeEquivalentTo(
                ["plaintext", "payload-fixture-sealed"],
                opts => opts.WithStrictOrdering());
    }
}
