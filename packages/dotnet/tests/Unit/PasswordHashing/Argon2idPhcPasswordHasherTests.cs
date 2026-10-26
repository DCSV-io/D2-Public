// -----------------------------------------------------------------------
// <copyright file="Argon2idPhcPasswordHasherTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.PasswordHashing;

using System;
using AwesomeAssertions;
using DcsvIo.D2.PasswordHashing;
using Xunit;

/// <summary>
/// Unit tests for <see cref="Argon2idPhcPasswordHasher"/>.
/// </summary>
public sealed class Argon2idPhcPasswordHasherTests
{
    /// <summary>
    /// Index of the salt in a dollar-delimited PHC string: the empty leading
    /// field, <c>argon2id</c>, <c>v=19</c>, the parameter header, then the salt.
    /// </summary>
    private const int _SALT_SEGMENT = 4;

    /// <summary>Index of the digest in a dollar-delimited PHC string.</summary>
    private const int _DIGEST_SEGMENT = 5;

    private readonly Argon2idPhcPasswordHasher r_sut = new(
        new PhcPasswordHasherOptions
        {
            // Lower cost settings for faster unit tests; the hash keeps the
            // Argon2id PHC shape.
            MemoryKib = 8 * 1024,
            Iterations = 1,
            Parallelism = 1,
        });

    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void Phc_EmptyPassword_ValidationFailed()
    {
        var result = r_sut.Hash(string.Empty);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Phc_NullPassword_ValidationFailed()
    {
        var result = r_sut.Hash(null);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Phc_OversizedPassword_ValidationFailed()
    {
        var oversized = new string('a', PhcPasswordHasherOptions.MAX_PASSWORD_LENGTH + 1);
        var result = r_sut.Hash(oversized);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Phc_Hash_Verify_RoundTrip_Ok()
    {
        const string password = "correct-horse-battery-staple-12";
        var hash = r_sut.Hash(password);
        hash.Success.Should().BeTrue();
        hash.Data.Should().NotBeNullOrEmpty();

        var verify = r_sut.Verify(password, hash.Data);
        verify.Success.Should().BeTrue();
        verify.Data.Should().BeTrue();
    }

    [Fact]
    public void Phc_Verify_WrongPassword_False_ConstantTimePath()
    {
        var hash = r_sut.Hash("correct-horse-battery-staple-12");
        hash.Success.Should().BeTrue();

        var verify = r_sut.Verify("wrong-password-xxxxxxxx-12", hash.Data);
        verify.Success.Should().BeTrue();
        verify.Data.Should().BeFalse();
    }

    [Fact]
    public void Phc_Output_IsArgon2idPhcString()
    {
        var hash = r_sut.Hash("correct-horse-battery-staple-12");
        hash.Success.Should().BeTrue();
        hash.Data.Should().StartWith("$argon2id$v=19$");
    }

    [Fact]
    public void Phc_Verify_MalformedPhc_FalseOrValidation()
    {
        var result = r_sut.Verify("correct-horse-battery-staple-12", "not-a-phc");
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public void Phc_NeedsRehash_WhenParamsBelowPolicy()
    {
        var weak = new Argon2idPhcPasswordHasher(
            new PhcPasswordHasherOptions
            {
                MemoryKib = 1024,
                Iterations = 1,
                Parallelism = 1,
            });

        var hash = weak.Hash("correct-horse-battery-staple-12");
        hash.Success.Should().BeTrue();

        // The hasher under test uses more memory than this hash did, so the
        // hash needs a rehash.
        r_sut.NeedsRehash(hash.Data).Should().BeTrue();
    }

    [Theory]

    // Cost parameters outside the accepted range, each of which either throws
    // inside Argon2id or claims whatever memory the row specified.
    [InlineData("$argon2id$v=19$m=0,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=8192,t=0,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=8192,t=1,p=0$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=99999999,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=8192,t=999,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=8192,t=1,p=99999$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=99999999999999999999,t=1,p=1$c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]

    // A tag or salt too short to be one.
    [InlineData("$argon2id$v=19$m=8192,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$YWJj")]
    [InlineData("$argon2id$v=19$m=8192,t=1,p=1$YWJj$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]

    // No tag at all.
    [InlineData("$argon2id$v=19$m=8192,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA")]
    public void Phc_Verify_StoredParametersOutsideBounds_AnswersFalse(string stored)
    {
        var result = r_sut.Verify("correct-horse-battery-staple-12", stored);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public void Phc_Verify_StoredParametersExactlyAtTheBounds_Verifies()
    {
        // The admitting side of the bound check above: a stored hash sitting on
        // every accepted minimum still round-trips, so a bound moved by one in
        // the refusing direction fails here.
        var atTheFloor = new Argon2idPhcPasswordHasher(
            new PhcPasswordHasherOptions
            {
                MemoryKib = PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB,
                Iterations = PhcPasswordHasherOptions.MIN_ACCEPTED_ITERATIONS,
                Parallelism = PhcPasswordHasherOptions.MIN_ACCEPTED_PARALLELISM,
                SaltLength = PhcPasswordHasherOptions.MIN_ACCEPTED_SALT_LENGTH,
                HashLength = PhcPasswordHasherOptions.MIN_ACCEPTED_HASH_LENGTH,
            });

        var hash = atTheFloor.Hash("correct-horse-battery-staple-12");
        hash.Success.Should().BeTrue();

        var verify = atTheFloor.Verify("correct-horse-battery-staple-12", hash.Data);

        verify.Success.Should().BeTrue();
        verify.Data.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("not-a-phc")]
    [InlineData("$argon2id$v=19$m=0,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    public void Phc_Verify_UnusableStoredHash_StillSpendsADerivation(string? stored)
    {
        // The absent, unparseable and out-of-bounds paths would otherwise return
        // without deriving, and the response time would tell a caller whether the
        // account exists. The count is read rather than timed, so the assertion
        // holds under a profiler and under coverage instrumentation alike.
        var before = r_sut.DerivationCount;

        var result = r_sut.Verify("wrong-password-xxxxxxxx-12", stored);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
        r_sut.DerivationCount.Should().Be(before + 1);
    }

    [Theory]
    [InlineData("$argon2id$v=19$m=0,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=99999999,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    [InlineData("$argon2id$v=19$m=8192,t=999,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYQ")]
    public void Phc_NeedsRehash_StoredCostsOutsideBounds_True(string stored)
    {
        r_sut.NeedsRehash(stored).Should().BeTrue();
    }

    [Fact]
    public void Phc_HashTwice_ProducesDifferentStringsThatBothVerify()
    {
        const string password = "correct-horse-battery-staple-12";

        var first = r_sut.Hash(password);
        var second = r_sut.Hash(password);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Data.Should().NotBe(first.Data);
        SaltSegmentOf(second.Data!).Should().NotBe(SaltSegmentOf(first.Data!));
        r_sut.Verify(password, first.Data).Data.Should().BeTrue();
        r_sut.Verify(password, second.Data).Data.Should().BeTrue();
    }

    [Theory]
    [InlineData(_SALT_SEGMENT)]
    [InlineData(_DIGEST_SEGMENT)]
    public void Phc_WellFormedStringWithOneAlteredCharacter_VerifiesFalse(int segment)
    {
        const string password = "correct-horse-battery-staple-12";
        var hash = r_sut.Hash(password);
        hash.Success.Should().BeTrue();

        var tampered = WithOneCharacterAltered(hash.Data!, segment);

        // The string is still well formed: same parameter header, same segment
        // lengths, same base64 alphabet. A verifier comparing only the header
        // would accept it.
        tampered.Should().NotBe(hash.Data);
        tampered.Should().StartWith("$argon2id$v=19$");

        var verify = r_sut.Verify(password, tampered);

        verify.Success.Should().BeTrue();
        verify.Data.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1, 1, 16, 32)]
    [InlineData(2097152, 1, 1, 16, 32)]
    [InlineData(8192, 0, 1, 16, 32)]
    [InlineData(8192, 32, 1, 16, 32)]
    [InlineData(8192, 1, 0, 16, 32)]
    [InlineData(8192, 1, 99, 16, 32)]
    [InlineData(8192, 1, 1, 4, 32)]
    [InlineData(8192, 1, 1, 128, 32)]
    [InlineData(8192, 1, 1, 16, 4)]
    [InlineData(8192, 1, 1, 16, 128)]

    // Inside every per-property bound, below the per-lane memory floor.
    [InlineData(8, 1, 4, 16, 32)]
    public void Phc_OptionsOutsideTheAcceptedRange_AreRefusedAtConstruction(
        int memoryKib,
        int iterations,
        int parallelism,
        int saltLength,
        int hashLength)
    {
        var options = new PhcPasswordHasherOptions
        {
            MemoryKib = memoryKib,
            Iterations = iterations,
            Parallelism = parallelism,
            SaltLength = saltLength,
            HashLength = hashLength,
        };

        var construct = () => new Argon2idPhcPasswordHasher(options);

        construct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HashPreservesCapturedParameters_AfterOptionsMutation()
    {
        var options = new PhcPasswordHasherOptions
        {
            MemoryKib = 1024,
            Iterations = 1,
            Parallelism = 1,
            SaltLength = 16,
            HashLength = 32,
        };

        var hasher = new Argon2idPhcPasswordHasher(options);
        options.MemoryKib = 2048;
        options.Iterations = 2;
        options.Parallelism = 2;
        options.SaltLength = 32;
        options.HashLength = 128;

        var result = hasher.Hash("password-sample");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        var segments = result.Data.Split('$');
        segments[3].Should().Be("m=1024,t=1,p=1");
        segments[_SALT_SEGMENT].Length.Should().Be(22);
        segments[_DIGEST_SEGMENT].Length.Should().Be(43);
        hasher.Verify("password-sample", result.Data).Data.Should().BeTrue();
    }

    [Fact]
    public void VerifyPreservesNoMatchDerivation_AfterOptionsMutation()
    {
        var options = new PhcPasswordHasherOptions { MemoryKib = 1024, Iterations = 1 };
        var hasher = new Argon2idPhcPasswordHasher(options);
        options.MemoryKib = 0;
        options.Iterations = 0;
        options.Parallelism = 0;
        options.HashLength = 0;

        var result = hasher.Verify("password-sample", null);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
        hasher.DerivationCount.Should().Be(1);
    }

    [Fact]
    public void NeedsRehashPreservesCapturedPolicy_AfterOptionsMutation()
    {
        var options = new PhcPasswordHasherOptions { MemoryKib = 1024, Iterations = 1 };
        var hasher = new Argon2idPhcPasswordHasher(options);
        var stored = hasher.Hash("password-sample").Data;
        options.MemoryKib = 2048;
        options.Iterations = 2;
        options.Parallelism = 2;
        options.SaltLength = 32;
        options.HashLength = 64;

        hasher.NeedsRehash(stored).Should().BeFalse();
    }

    [Theory]
    [InlineData(8, 32)]
    [InlineData(16, 16)]
    public void NeedsRehashReturnsTrue_VerifiedHashHasShorterPolicyLength(
        int saltLength,
        int hashLength)
    {
        var producer = new Argon2idPhcPasswordHasher(new PhcPasswordHasherOptions
        {
            MemoryKib = 8192,
            Iterations = 1,
            SaltLength = saltLength,
            HashLength = hashLength,
        });

        var stored = producer.Hash("password-sample").Data;
        r_sut.Verify("password-sample", stored).Data.Should().BeTrue();
        r_sut.NeedsRehash(stored).Should().BeTrue();
    }

    [Theory]
    [InlineData(16, 32)]
    [InlineData(64, 64)]
    public void NeedsRehashReturnsFalse_VerifiedHashMeetsPolicyLengths(
        int saltLength,
        int hashLength)
    {
        var producer = new Argon2idPhcPasswordHasher(new PhcPasswordHasherOptions
        {
            MemoryKib = 8192,
            Iterations = 1,
            SaltLength = saltLength,
            HashLength = hashLength,
        });

        var stored = producer.Hash("password-sample").Data;
        r_sut.Verify("password-sample", stored).Data.Should().BeTrue();
        r_sut.NeedsRehash(stored).Should().BeFalse();
    }

    [Theory]
    [InlineData("A", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", "A")]
    [InlineData("AA", "AA")]
    [InlineData("!!!!", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", "!!!!")]
    public void NeedsRehashReturnsTrue_StoredSegmentsAreMalformed(string salt, string digest)
    {
        var stored = "$argon2id$v=19$m=8192,t=1,p=1$" + salt + "$" + digest;

        r_sut.Verify("password-sample", stored).Data.Should().BeFalse();
        r_sut.NeedsRehash(stored).Should().BeTrue();
    }

    [Theory]
    [InlineData(7, 32)]
    [InlineData(65, 32)]
    [InlineData(16, 15)]
    [InlineData(16, 65)]
    public void NeedsRehashReturnsTrue_StoredLengthsExceedAcceptedBounds(
        int saltLength,
        int hashLength)
    {
        var salt = Convert.ToBase64String(new byte[saltLength]).TrimEnd('=');
        var digest = Convert.ToBase64String(new byte[hashLength]).TrimEnd('=');
        var stored = "$argon2id$v=19$m=8192,t=1,p=1$" + salt + "$" + digest;

        r_sut.Verify("password-sample", stored).Data.Should().BeFalse();
        r_sut.NeedsRehash(stored).Should().BeTrue();
    }

    /// <summary>
    /// Returns the salt segment of a PHC string - the fifth dollar-delimited
    /// field.
    /// </summary>
    private static string SaltSegmentOf(string phc) => phc.Split('$')[_SALT_SEGMENT];

    /// <summary>
    /// Returns <paramref name="phc"/> with the first character of the named
    /// dollar-delimited segment replaced by a different base64 character, so the
    /// string keeps its shape and its segment lengths.
    /// </summary>
    private static string WithOneCharacterAltered(string phc, int segment)
    {
        var parts = phc.Split('$');
        var value = parts[segment];
        var replacement = value[0] == 'A' ? 'B' : 'A';
        parts[segment] = replacement + value[1..];

        return string.Join('$', parts);
    }
}
