// -----------------------------------------------------------------------
// <copyright file="StreetAddressTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="StreetAddress"/>:
/// the 5-line constructor, the rule that Line1 is required, the no-gap rule,
/// two-stage normalization (stored form and hash form), deduplication of
/// Latin diacritics, preservation of non-Latin scripts, adversarial emoji,
/// CRLF, NUL, TAB, BiDi-override and ZWJ inputs, and HashId invariants.
/// </summary>
public sealed class StreetAddressTests
{
    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllFiveLines_ReturnsOk_WithCasePreserved()
    {
        var result = StreetAddress.Create("123 Main St", "Apt 4B", "Brooklyn", "NY 11201", "USA");

        result.Success.Should().BeTrue();
        var addr = result.Data!;
        addr.Line1.Should().Be("123 Main St");
        addr.Line2.Should().Be("Apt 4B");
        addr.Line3.Should().Be("Brooklyn");
        addr.Line4.Should().Be("NY 11201");
        addr.Line5.Should().Be("USA");
        addr.HashId.Should().StartWith("v1.");
        addr.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_OnlyLine1_ReturnsOk()
    {
        var result = StreetAddress.Create("Just Line 1");

        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("Just Line 1");
        result.Data!.Line2.Should().BeNull();
        result.Data!.Line3.Should().BeNull();
        result.Data!.Line4.Should().BeNull();
        result.Data!.Line5.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // No-gap rule - Line1 and Line5 with nulls between them are valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line1AndLine5_WithNullsBetween_ReturnsOk_NoGapRule()
    {
        var result = StreetAddress.Create("Line 1", null, null, null, "Line 5");

        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("Line 1");
        result.Data!.Line5.Should().Be("Line 5");
    }

    // -----------------------------------------------------------------------
    // Line1 required
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public void Create_Line1NullOrWhitespace_ReturnsValidationFailed(string? line1)
    {
        var result = StreetAddress.Create(line1);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.ADDRESS_LINE1_REQUIRED.Key);
    }

    [Fact]
    public void Create_Line1OnlyPunctuation_ReturnsValidationFailed_AfterStrip()
    {
        // The stored form removes decorative punctuation entirely.
        var result = StreetAddress.Create("....");
        result.Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Two-stage normalization - stored form
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DecorativePunctuationInStored_IsStripped_CasePreserved()
    {
        // The stored form removes periods, commas, semicolons, colons,
        // exclamation marks and question marks.
        var result = StreetAddress.Create("123 Main St., Apt. 4!");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St Apt 4");
    }

    [Fact]
    public void Create_SemanticPunctuationInStored_IsPreserved()
    {
        // Hyphens, apostrophes, parentheses, '#' and '&' stay in the stored
        // form.
        var result = StreetAddress.Create("O'Connor's #4 & Co (Suite-B)");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("O'Connor's #4 & Co (Suite-B)");
    }

    [Fact]
    public void Create_InternalWhitespaceCollapsedInStored()
    {
        var result = StreetAddress.Create("123    Main    St");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St");
    }

    [Fact]
    public void Create_OuterWhitespaceTrimmedInStored()
    {
        var result = StreetAddress.Create("   123 Main St   ");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main St");
    }

    // -----------------------------------------------------------------------
    // Hash dedup - case
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_DifferentCase_ProducesSameHashId()
    {
        var r1 = StreetAddress.Create("123 Main St");
        var r2 = StreetAddress.Create("123 main st");
        var r3 = StreetAddress.Create("123 MAIN ST");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
        r1.Data!.HashId.Should().Be(r3.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // Hash dedup - Latin diacritics
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_LatinDiacritics_DedupViaNfd_Café_vs_Cafe_SameHashId()
    {
        var r1 = StreetAddress.Create("Café");
        var r2 = StreetAddress.Create("Cafe");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_LatinDiacritics_Zürich_vs_Zurich_SameHashId()
    {
        var r1 = StreetAddress.Create("Zürich");
        var r2 = StreetAddress.Create("Zurich");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // HashId invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_StartsWithV1Prefix()
    {
        var result = StreetAddress.Create("123 Main St");
        result.Data!.HashId.Should().StartWith("v1.");
    }

    [Fact]
    public void Create_HashId_HasCorrectLength()
    {
        var result = StreetAddress.Create("123 Main St");

        // "v1." (3) + 64 hex characters = 67
        result.Data!.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_HashId_IsLowercaseHex()
    {
        var result = StreetAddress.Create("123 Main St");
        var hexPart = result.Data!.HashId[3..];
        hexPart.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Create_HashId_Deterministic_AcrossCalls()
    {
        var r1 = StreetAddress.Create("123 Main St", "Apt 4");
        var r2 = StreetAddress.Create("123 Main St", "Apt 4");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_AllFiveSlots_Differ_From_FewerSlots()
    {
        var r1 = StreetAddress.Create("Line 1");
        var r2 = StreetAddress.Create("Line 1", "Line 2");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_LinePopulationDistinct_ProducesDistinctHashes()
    {
        // Adding Line5 to an input that already has Line1 changes the hash
        // input, so the hash differs from the Line1-only input.
        var r1 = StreetAddress.Create("Line 1");
        var r2 = StreetAddress.Create("Line 1", null, null, null, "Line 5");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // Non-Latin scripts - preserved on purpose
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_Cyrillic_PreservedAfterUppercase()
    {
        StreetAddress.NormalizeForHash("Москва").Should().Be("МОСКВА");
    }

    [Fact]
    public void NormalizeForHash_CJK_PreservedAsIs_CaselessScript()
    {
        StreetAddress.NormalizeForHash("东京").Should().Be("东京");
    }

    [Fact]
    public void NormalizeForHash_Greek_NfdStripsCombiningMarks_UppercasePreserved()
    {
        StreetAddress.NormalizeForHash("Αθήνα").Should().Be("ΑΘΗΝΑ");
    }

    [Fact]
    public void NormalizeForHash_Arabic_PreservedAsIs_CaselessScript()
    {
        StreetAddress.NormalizeForHash("الرياض").Should().Be("الرياض");
    }

    // -----------------------------------------------------------------------
    // Adversarial - emoji / CRLF / NUL / TAB / BiDi / ZWJ
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_EmojiInInput_Stripped()
    {
        // U+1F4A9 PILE OF POO is a surrogate pair, and the method strips it by
        // iterating over Rune values.
        StreetAddress.NormalizeForHash("💩 Address").Should().Be(" ADDRESS");
    }

    [Fact]
    public void NormalizeForHash_OnlyEmoji_ProducesEmpty()
    {
        StreetAddress.NormalizeForHash("💩🌍🚀").Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeForHash_AllPunctuation_ProducesEmpty()
    {
        StreetAddress.NormalizeForHash("....,,,;;;:::!!!???").Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeForHash_CRLF_Stripped()
    {
        // CRLF characters are Control characters, not a Letter, Digit or
        // space, so the method strips them.
        var hashed = StreetAddress.NormalizeForHash("123 Main\r\nINJECTED");
        hashed.Should().NotContain("\r");
        hashed.Should().NotContain("\n");
    }

    [Fact]
    public void NormalizeForHash_NullByte_Stripped()
    {
        var hashed = StreetAddress.NormalizeForHash("123 Main\0INJECTED");
        hashed.Should().NotContain("\0");
    }

    [Fact]
    public void NormalizeForHash_Tab_Stripped()
    {
        var hashed = StreetAddress.NormalizeForHash("123 Main\t\tApt");
        hashed.Should().NotContain("\t");
    }

    [Fact]
    public void NormalizeForHash_ZeroWidthJoiner_Stripped()
    {
        // U+200D ZERO WIDTH JOINER is in the Format category.
        var hashed = StreetAddress.NormalizeForHash("Main‍St");
        hashed.Should().Be("MAINST");
    }

    [Fact]
    public void NormalizeForHash_BiDiOverride_Stripped()
    {
        // U+202E RIGHT-TO-LEFT OVERRIDE is in the Format category.
        var hashed = StreetAddress.NormalizeForHash("Hello‮World");
        hashed.Should().Be("HELLOWORLD");
    }

    [Fact]
    public void NormalizeForHash_MixedScriptWithHyphenAndDigits_HyphenStripped()
    {
        // A hyphen is Punctuation, not a Letter, Digit or space, so the
        // method strips it.
        var hashed = StreetAddress.NormalizeForHash("123 Москва-Center");
        hashed.Should().Be("123 МОСКВАCENTER");
    }

    // -----------------------------------------------------------------------
    // Stored form - CRLF, NUL and TAB collapse to a single space or are stripped
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CrLfInLine1_CollapsedOrStrippedInStoredForm()
    {
        var result = StreetAddress.Create("123 Main\r\nINJECTED LOG LINE");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().NotContain("\r");
        result.Data!.Line1.Should().NotContain("\n");

        // Whitespace and control characters collapse to a single space.
        result.Data!.Line1.Should().Be("123 Main INJECTED LOG LINE");
    }

    [Fact]
    public void Create_NullByteInLine1_StrippedFromStoredAndHashForms()
    {
        var result = StreetAddress.Create("123 Main\0INJECTED");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().NotContain("\0");
    }

    [Fact]
    public void Create_TabInLine1_CollapsedToSpaceInStoredForm()
    {
        var result = StreetAddress.Create("123 Main\t\tApt");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("123 Main Apt");
    }

    // -----------------------------------------------------------------------
    // Boundary - single-character and very long inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_SingleChar_Ok()
    {
        var result = StreetAddress.Create("A");
        result.Success.Should().BeTrue();
        result.Data!.Line1.Should().Be("A");
    }

    // -----------------------------------------------------------------------
    // Length caps - Line1 (required)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line1_ExactlyMax_ReturnsOk()
    {
        var line1 = new string('a', FieldConstraints.STREET_LINE_MAX);
        var result = StreetAddress.Create(line1);
        result.Success.Should().BeTrue();
        result.Data!.Line1.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    [Fact]
    public void Create_Line1_OverMax_ReturnsTooLong()
    {
        var line1 = new string('a', FieldConstraints.STREET_LINE_MAX + 1);
        var result = StreetAddress.Create(line1);
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.STREET_LINE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps - Line2 (optional)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line2_ExactlyMax_ReturnsOk()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            new string('a', FieldConstraints.STREET_LINE_MAX));
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_Line2_OverMax_ReturnsTooLong()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            new string('a', FieldConstraints.STREET_LINE_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.STREET_LINE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps - Line3 (optional)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line3_ExactlyMax_ReturnsOk()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX));
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_Line3_OverMax_ReturnsTooLong()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.STREET_LINE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps - Line4 (optional)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line4_ExactlyMax_ReturnsOk()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX));
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_Line4_OverMax_ReturnsTooLong()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.STREET_LINE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps - Line5 (optional)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line5_ExactlyMax_ReturnsOk()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            null,
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX));
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_Line5_OverMax_ReturnsTooLong()
    {
        var result = StreetAddress.Create(
            "Valid Line 1",
            null,
            null,
            null,
            new string('a', FieldConstraints.STREET_LINE_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.STREET_LINE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // The cap applies to the cleaned value
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line1_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        // The raw input is longer than MAX, but cleaning strips the decorative
        // punctuation, so the cleaned length is at most MAX. The value is
        // STREET_LINE_MAX 'a' characters followed by the stripped character
        // '.', so the cleaned length equals STREET_LINE_MAX.
        var line1 = new string('a', FieldConstraints.STREET_LINE_MAX) + ".";
        var result = StreetAddress.Create(line1);
        result.Success.Should().BeTrue();
        result.Data!.Line1.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    // -----------------------------------------------------------------------
    // The cap applies to the cleaned value - Lines 2-5 (T7)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Line2_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        // The raw input is longer than MAX because of an appended decorative
        // punctuation character '.', which cleaning strips. The cleaned length
        // equals STREET_LINE_MAX, so the line is accepted.
        var line2 = new string('a', FieldConstraints.STREET_LINE_MAX) + ".";
        var result = StreetAddress.Create("Valid Line 1", line2);
        result.Success.Should().BeTrue();
        result.Data!.Line2!.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    [Fact]
    public void Create_Line3_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        var line3 = new string('a', FieldConstraints.STREET_LINE_MAX) + ".";
        var result = StreetAddress.Create("Valid Line 1", null, line3);
        result.Success.Should().BeTrue();
        result.Data!.Line3!.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    [Fact]
    public void Create_Line4_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        var line4 = new string('a', FieldConstraints.STREET_LINE_MAX) + ".";
        var result = StreetAddress.Create("Valid Line 1", null, null, line4);
        result.Success.Should().BeTrue();
        result.Data!.Line4!.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    [Fact]
    public void Create_Line5_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        var line5 = new string('a', FieldConstraints.STREET_LINE_MAX) + ".";
        var result = StreetAddress.Create("Valid Line 1", null, null, null, line5);
        result.Success.Should().BeTrue();
        result.Data!.Line5!.Length.Should().Be(FieldConstraints.STREET_LINE_MAX);
    }

    // -----------------------------------------------------------------------
    // Hash-stability regression - golden-value pin
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_KnownInput_MatchesGoldenValue()
    {
        // Pin: StreetAddress.Create("123 Main St") -> hash input "123 MAIN ST||||"
        // -> SHA-256 (UTF-8) -> v1.<64-hex>.
        // A change to the NormalizeForHash algorithm fails this test and
        // prevents silent drift.
        const string expected_hash =
            "v1.e7a9181fea764351c88f38fd0bfe3644a368ce058e3f75fa0e85cc27b06bab78";
        var result = StreetAddress.Create("123 Main St");
        result.Data!.HashId.Should().Be(expected_hash);
    }

    // -----------------------------------------------------------------------
    // NormalizeForHash - single character and oversized input
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeForHash_SingleLetter_Preserved()
    {
        StreetAddress.NormalizeForHash("a").Should().Be("A");
    }

    [Fact]
    public void NormalizeForHash_1000CharInput_NoTimeout()
    {
        var huge = new string('a', 1000);
        var hashed = StreetAddress.NormalizeForHash(huge);
        hashed.Length.Should().Be(1000);
        hashed.Should().Be(new string('A', 1000));
    }

    [Fact]
    public void NormalizeForHash_TypicalUSAddress_ExpectedOutput()
    {
        StreetAddress.NormalizeForHash("123 Main St").Should().Be("123 MAIN ST");
    }
}
