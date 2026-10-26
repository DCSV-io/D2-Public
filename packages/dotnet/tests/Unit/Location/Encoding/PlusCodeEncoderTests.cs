// -----------------------------------------------------------------------
// <copyright file="PlusCodeEncoderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location.Encoding;

using AwesomeAssertions;
using DcsvIo.D2.Location.Encoding;
using Xunit;

/// <summary>
/// Unit tests for <see cref="PlusCodeEncoder"/> covering IsValid, round-trip
/// fidelity, reference test vectors, and edge cases.
/// </summary>
public sealed class PlusCodeEncoderTests
{
    // -----------------------------------------------------------------------
    // IsValid - happy path
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("87G7MQ8V+RG")]
    [InlineData("87G7PX2C+X2")]
    [InlineData("8FW4V75V+8F")]
    [InlineData("8Q7XMM8V+RG")]
    public void IsValid_ValidPlusCodes_ReturnsTrue(string plusCode)
    {
        PlusCodeEncoder.IsValid(plusCode).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // IsValid - invalid inputs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("87G7MQ8VRG")]
    [InlineData("87G7MQ8V++RG")]
    [InlineData("87G7MQ8V+")]
    [InlineData("+RG")]
    [InlineData("AI+BC")]
    public void IsValid_InvalidPlusCodes_ReturnsFalse(string? plusCode)
    {
        PlusCodeEncoder.IsValid(plusCode).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Round-trip fidelity
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_ThenDecode_ReturnsCoordinatesWithinError()
    {
        const double lat = 40.7128;
        const double lon = -74.006;

        var plusCode = PlusCodeEncoder.Encode(lat, lon, codeLength: 10);
        var (decodedLat, decodedLon, latErr, lonErr) = PlusCodeEncoder.Decode(plusCode);

        Math.Abs(decodedLat - lat).Should().BeLessThanOrEqualTo(latErr * 4);
        Math.Abs(decodedLon - lon).Should().BeLessThanOrEqualTo(lonErr * 4);
    }

    [Fact]
    public void Encode_IsValid_ProducesValidCode()
    {
        var code = PlusCodeEncoder.Encode(51.5074, -0.1278, codeLength: 10);
        PlusCodeEncoder.IsValid(code).Should().BeTrue();
    }

    [Fact]
    public void Encode_ContainsPlusSeparator_AtPosition8()
    {
        var code = PlusCodeEncoder.Encode(35.6762, 139.6503, codeLength: 10);

        code.IndexOf('+').Should().Be(8);
    }

    // -----------------------------------------------------------------------
    // Edge coordinates
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(89.9, 0.0)]
    [InlineData(-89.9, 0.0)]
    [InlineData(0.0, 179.9)]
    [InlineData(0.0, -179.9)]
    public void Encode_BoundaryCoordinates_ProducesValidPlusCode(double lat, double lon)
    {
        var code = PlusCodeEncoder.Encode(lat, lon, codeLength: 10);
        PlusCodeEncoder.IsValid(code).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // OLC alphabet - no invalid characters
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_UsesOnlyOlcAlphabet()
    {
        const string validChars = "23456789CFGHJMPQRVWX0+";
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);

        foreach (var c in code)
            validChars.Should().Contain(c.ToString());
    }

    // -----------------------------------------------------------------------
    // Determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_CalledTwice_SameInput_ProducesIdenticalOutput()
    {
        const double lat = 48.8566;
        const double lon = 2.3522;

        var code1 = PlusCodeEncoder.Encode(lat, lon);
        var code2 = PlusCodeEncoder.Encode(lat, lon);

        code1.Should().Be(code2);
    }

    // -----------------------------------------------------------------------
    // Decode produces center within cell
    // -----------------------------------------------------------------------

    [Fact]
    public void Decode_EncodeThenDecode_CenterIsIdempotent()
    {
        const double lat = 51.5074;
        const double lon = -0.1278;

        var code1 = PlusCodeEncoder.Encode(lat, lon);
        var (cLat, cLon, _, _) = PlusCodeEncoder.Decode(code1);
        var code2 = PlusCodeEncoder.Encode(cLat, cLon);

        code2.Should().Be(code1);
    }

    // -----------------------------------------------------------------------
    // Regression tests: they pin the FULL_PAIRS = 4 invariant and the way
    // Decode derives the pair count.
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_HasExactly8DigitsBeforePlusSeparator_PinsFullPairsCount()
    {
        // The OLC spec requires 4 full pairs, which is 8 prefix digits before
        // '+'. Deriving fullPairs from the digit count of a 10-character code
        // would give 5 pairs.
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);

        var plusIdx = code.IndexOf('+');
        plusIdx.Should().Be(8);

        // A second check on the same code: the prefix has 8 characters and
        // the suffix has 2.
        var prefix = code[..plusIdx];
        var suffix = code[(plusIdx + 1)..];
        prefix.Length.Should().Be(8);
        suffix.Length.Should().Be(2);
    }

    [Fact]
    public void Decode_FullPairCountDerivedFromSeparatorPosition_NotStrippedLength()
    {
        // Decode takes the pair count from prefix.Length / 2, with the
        // OLC-spec separator at position 8, so fullPairs = 4. A pair count
        // taken from the length of the code with '+' removed would be wrong.
        // Encoding a known canonical input and decoding it again must
        // round-trip with the same prefix length.
        var code = PlusCodeEncoder.Encode(40.7128, -74.006, codeLength: 10);
        var (decodedLat, decodedLon, latErr, lonErr) = PlusCodeEncoder.Decode(code);

        // If fullPairs were wrong, decode would return a very different
        // latitude and longitude and these bounds would fail. The tolerance
        // is 4 times the reported error.
        Math.Abs(decodedLat - 40.7128).Should().BeLessThanOrEqualTo(latErr * 4);
        Math.Abs(decodedLon - -74.006).Should().BeLessThanOrEqualTo(lonErr * 4);

        // Encode the center again. The same code comes back, so the pair-count
        // invariant holds in both Encode and Decode.
        var reEncoded = PlusCodeEncoder.Encode(decodedLat, decodedLon, codeLength: 10);
        reEncoded.Should().Be(code);
    }

    // -----------------------------------------------------------------------
    // Production codeLength:12 - direct coverage (pins the 13-char output and the round-trip)
    // -----------------------------------------------------------------------

    [Fact]
    public void Encode_CodeLength12_ProducesTotalLength13_WithSuffix4()
    {
        // codeLength=12 is the production setting in Coordinates.BuildFromLatLon.
        // 8 pair digits + '+' + 4 grid digits = 13 characters in total, and
        // the suffix after '+' has 4.
        const int expected_total_length = 13;
        const int expected_suffix_length = 4;
        const double lat = 40.7128;
        const double lon = -74.006;

        var code = PlusCodeEncoder.Encode(lat, lon, codeLength: 12);

        code.Length.Should().Be(expected_total_length);
        var plusIdx = code.IndexOf('+');
        code[(plusIdx + 1)..].Length.Should().Be(expected_suffix_length);
    }

    [Fact]
    public void Encode_ThenDecode_CodeLength12_ReturnsCoordinatesWithinError()
    {
        // Round-trip at the production codeLength=12, like the codeLength:10
        // test above.
        const double lat = 40.7128;
        const double lon = -74.006;

        var code = PlusCodeEncoder.Encode(lat, lon, codeLength: 12);
        var (decodedLat, decodedLon, latErr, lonErr) = PlusCodeEncoder.Decode(code);

        Math.Abs(decodedLat - lat).Should().BeLessThanOrEqualTo(latErr * 4);
        Math.Abs(decodedLon - lon).Should().BeLessThanOrEqualTo(lonErr * 4);
    }
}
