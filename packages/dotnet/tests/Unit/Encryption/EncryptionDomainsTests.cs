// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Pins the public encryption-domain wire values.
/// The other sealed domains are emitted on a separate catalog, not on
/// <see cref="EncryptionDomains"/>.
/// </summary>
public sealed class EncryptionDomainsTests
{
    [Fact]
    public void Plaintext_HasExpectedValue()
        => EncryptionDomains.PLAINTEXT.Should().Be("plaintext");

    [Fact]
    public void FixtureSealed_HasExpectedValue()
        => EncryptionDomains.FIXTURE_SEALED.Should().Be("payload-fixture-sealed");

    [Fact]
    public void AllDomains_EnumeratesEveryPublicEntryInSpecOrder()
        => EncryptionDomains.AllDomains.Should()
            .Equal("plaintext", "payload-fixture-sealed");

    [Fact]
    public void AllDomains_ContainsNoDuplicates()
        => EncryptionDomains.AllDomains
            .Distinct()
            .Should()
            .HaveCount(EncryptionDomains.AllDomains.Count);
}
