// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameLayoutParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Spec-runtime parity gate: asserts that the minimum and maximum kid-length
/// constants of <see cref="PayloadCryptoKeyring"/> agree with the spec-derived
/// <see cref="EncryptionFrameLayout"/> constants, so the keyring's validation
/// cannot silently diverge from the frame format the spec defines.
/// </summary>
public sealed class EncryptionFrameLayoutParityTests
{
    [Fact]
    public void MaxKidLength_KeyringMatchesFrameLayoutSpec()
    {
        PayloadCryptoKeyring.MAX_KID_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_MAX_KID_LENGTH);
    }

    [Fact]
    public void MinKidLength_KeyringMatchesFrameLayoutSpec()
    {
        PayloadCryptoKeyring.MIN_KID_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_MIN_KID_LENGTH);
    }
}
