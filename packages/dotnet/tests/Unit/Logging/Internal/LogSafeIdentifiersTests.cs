// -----------------------------------------------------------------------
// <copyright file="LogSafeIdentifiersTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Internal;

using System.Globalization;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Logging.Internal;
using Xunit;

/// <summary>
/// Unit tests for <see cref="LogSafeIdentifiers"/>. The digest must support
/// correlation, by being stable and distinct per input, and must never contain
/// the credential itself.
/// </summary>
public sealed class LogSafeIdentifiersTests
{
    [Fact]
    public void SessionDigest_IsStableAcrossCalls()
    {
        var sessionId = Guid.Parse("7f3a1c2e-9b41-4d6a-8e05-2c7d1f4b6a90");

        var first = LogSafeIdentifiers.SessionDigest(sessionId);
        var second = LogSafeIdentifiers.SessionDigest(sessionId);

        // Correlation across log lines and services depends on this, so the
        // digest uses no random salt.
        second.Should().Be(first);
    }

    [Fact]
    public void SessionDigest_IsPinnedValue_SoCorrelationSurvivesRefactors()
    {
        var sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // A changed digest breaks every saved operator query, so the test pins
        // the value.
        LogSafeIdentifiers.SessionDigest(sessionId).Should().Be("307026e5e4ac7cb3");
    }

    [Fact]
    public void SessionDigest_DiffersPerSession()
    {
        var first = LogSafeIdentifiers.SessionDigest(
            Guid.Parse("7f3a1c2e-9b41-4d6a-8e05-2c7d1f4b6a90"));
        var second = LogSafeIdentifiers.SessionDigest(
            Guid.Parse("1d9e4b70-3a25-4f88-9c31-6e0a7b2d5c14"));

        first.Should().NotBe(second);
    }

    [Fact]
    public void SessionDigest_NeverContainsAnySpellingOfTheRawId()
    {
        var sessionId = Guid.Parse("7f3a1c2e-9b41-4d6a-8e05-2c7d1f4b6a90");

        var digest = LogSafeIdentifiers.SessionDigest(sessionId);

        foreach (var format in new[] { "N", "D", "B", "P", "X" })
            digest.Should().NotContain(sessionId.ToString(format, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SessionDigest_IsLowercaseHexOfDeclaredLength()
    {
        var digest = LogSafeIdentifiers.SessionDigest(Guid.CreateVersion7());

        digest.Should().HaveLength(LogSafeIdentifiers.DIGEST_HEX_LENGTH);
        digest.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void SessionDigest_EmptyGuid_StillProducesADigest()
    {
        // Guid.Empty is never a live session, but the helper must not
        // special-case it or throw on it, because a throwing enricher would
        // stop request logging.
        LogSafeIdentifiers.SessionDigest(Guid.Empty)
            .Should().HaveLength(LogSafeIdentifiers.DIGEST_HEX_LENGTH);
    }

    [Fact]
    public void ToLogSafe_AChainDeeperThanTheCap_MarksTheEntryTheProjectionStoppedAt()
    {
        // Without the mark, a chain that the cap cut renders to the sink as a
        // complete chain that ends there, and a reader cannot tell that actors
        // were dropped. A cut chain is the one case where the actor chain
        // matters most.
        var projected = LogSafeIdentifiers.ToLogSafe([ChainOfDepth(40)]);

        var entry = projected.Should().ContainSingle().Subject;
        var depth = 0;

        while (entry.Act is { } next)
        {
            entry.ActTruncated.Should().BeFalse(
                "only the entry the cap stopped at carries the mark");
            entry = next;
            depth++;
        }

        depth.Should().Be(20, "the projection stops at the depth cap");
        entry.ActTruncated.Should().BeTrue(
            "the input carried a nested actor past the cap");
    }

    [Fact]
    public void ToLogSafe_AChainInsideTheCap_MarksNothingAsTruncated()
    {
        // The case that the cap admits: a chain that ended on its own is not
        // marked as cut.
        var projected = LogSafeIdentifiers.ToLogSafe([ChainOfDepth(3)]);

        var entry = projected.Should().ContainSingle().Subject;

        while (entry is not null)
        {
            entry.ActTruncated.Should().BeFalse();
            entry = entry.Act;
        }
    }

    private static ActorEntry ChainOfDepth(int links)
    {
        var entry = new ActorEntry(ActorKind.Service, "leaf");

        for (var i = 0; i < links; i++)
        {
            entry = new ActorEntry(
                ActorKind.Service,
                "link-" + i.ToString(CultureInfo.InvariantCulture),
                Act: entry);
        }

        return entry;
    }
}
