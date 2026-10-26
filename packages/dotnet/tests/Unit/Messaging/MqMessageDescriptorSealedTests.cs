// -----------------------------------------------------------------------
// <copyright file="MqMessageDescriptorSealedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Tests of the computed <see cref="MqMessageDescriptor.IsSealed"/> and
/// <see cref="MqMessageDescriptor.ConsumerService"/> properties of the
/// descriptor. They resolve through <see cref="EncryptionDomainModeCatalog"/>
/// (overlay first, then the generated <see cref="EncryptionDomainModes"/>
/// baseline) after the dual values are stripped.
/// </summary>
public sealed class MqMessageDescriptorSealedTests
{
    [Fact]
    public void FixtureSealedDomain_IsSealed_WithConsumerService()
    {
        var descriptor = Descriptor(EncryptionDomains.FIXTURE_SEALED);

        descriptor.IsSealed.Should().BeTrue();
        descriptor.ConsumerService.Should().Be("payload-fixture-sealed");
        descriptor.IsPlaintext.Should().BeFalse();
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("shipping")]
    [InlineData("inventory")]
    public void PrivateSealedDomainStrings_AreNotSealedOnPublicCatalog(string domain)
    {
        // The public catalog does not declare these sealed domains, so here
        // they resolve as unknown -> Symmetric.
        var descriptor = Descriptor(domain);

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
    }

    [Fact]
    public void PlaintextDomain_IsNotSealed_NoConsumerService()
    {
        var descriptor = Descriptor(MqMessageDescriptor.PLAINTEXT);

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
        descriptor.IsPlaintext.Should().BeTrue();
    }

    [Fact]
    public void UnknownDomain_DefaultsToSymmetric_NotSealed()
    {
        // A synthetic test domain is not sealed, because a domain can be sealed
        // only if the spec catalog declares it. This is the documented ModeFor
        // default of unknown -> Symmetric.
        var descriptor = Descriptor("some-fixture-domain");

        descriptor.IsSealed.Should().BeFalse();
        descriptor.ConsumerService.Should().BeNull();
    }

    private static MqMessageDescriptor Descriptor(string domain) => new(
        Constant: "TestConstant",
        MessageTypeName: "D2.Test.SampleMessage",
        Exchange: "d2.test.events",
        ExchangeType: "fanout",
        Encryption: domain,
        EncryptionReason: domain == MqMessageDescriptor.PLAINTEXT ? "test" : null,
        DefaultRoutingKey: string.Empty);
}
