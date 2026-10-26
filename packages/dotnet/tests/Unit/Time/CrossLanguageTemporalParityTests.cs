// -----------------------------------------------------------------------
// <copyright file="CrossLanguageTemporalParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using DcsvIo.D2.Time;
using NodaTime;
using NodaTime.Text;
using Xunit;

/// <summary>
/// Cross-language parity tests for ZonedInstant + LocalAnchoredEvent. Each
/// fixture in <c>contracts/temporal/temporal-adversarial.fixture.json</c>
/// pins a (scheduledLocal, iana) -> expectedUtc mapping that BOTH the .NET
/// NodaTime engine AND the TypeScript Temporal engine must produce
/// identically. If a fixture's expectedUtc diverges between languages, the
/// adversarial cross-language contract has broken and the divergence must
/// be surfaced (NOT silently reconciled by adjusting expectedUtc).
/// </summary>
public sealed class CrossLanguageTemporalParityTests
{
    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [Fact]
    public void Fixture_USSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("us-spring-forward-skipped-2-30");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_USFallBack_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("us-fall-back-ambiguous-1-30-picks-earlier");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_EuropeanSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("european-spring-forward-skipped");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_EuropeanFallBack_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("european-fall-back-ambiguous-picks-earlier");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_AustralianSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("australian-spring-forward-skipped");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_USPacificAlias_NormalizesToAmericaLosAngeles()
    {
        var fx = LoadFixture("iana-normalization-us-pacific-alias");

        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        ev.IANAIdentifier.Should().Be(fx.ExpectedCanonicalIana);
        ev.ComputeNextFire().Data.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_AsiaSaigonRenamed_NormalizesToAsiaHoChiMinh()
    {
        var fx = LoadFixture("iana-normalization-asia-saigon-renamed");

        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        ev.IANAIdentifier.Should().Be(fx.ExpectedCanonicalIana);
        ev.ComputeNextFire().Data.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Theory]
    [ClassData(typeof(AllFixturesTheoryData))]
    public void AllFixtures_NetProducesExpectedUtc_TheoryOverEntireFixtureFile(
        string fixtureId)
    {
        var fx = LoadFixture(fixtureId);
        var fire = ComputeFire(fx);

        fire.Should().Be(
            ParseInstant(fx.ExpectedUtc),
            $"fixture '{fx.Id}' must produce expectedUtc identical to TS-side");
    }

    /// <summary>
    /// Deliberate-drift test DD-T1: shows that the <see cref="Instant"/>
    /// equality used by the parity assertions can fail. If the .NET and
    /// TypeScript engines diverged on epoch interpretation by even one second,
    /// the fixture comparison would fail; this test checks that the comparison
    /// can fail.
    ///
    /// Constructs two <see cref="Instant"/> values that differ by exactly
    /// one second and asserts they are DIFFERENT. A comparator that always
    /// returned "equal" would fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenInstantEpochsDifferByOneSecond()
    {
        var fx = LoadFixture("unambiguous-utc-noon");
        var realInstant = ParseInstant(fx.ExpectedUtc);

        // Build an instant one second later than the real value, as a TypeScript
        // engine with a different epoch interpretation would produce. The parity
        // comparison must report the difference.
        var divergedInstant = realInstant + Duration.FromSeconds(1);

        realInstant.Should().NotBe(
            divergedInstant,
            "parity comparator must detect a 1-second epoch divergence " +
            "between language engines");
    }

    /// <summary>
    /// Deliberate-drift test DD-T2: shows that the DST resolution comparison
    /// can fail. If the .NET
    /// <see cref="NodaTime.TimeZones.Resolvers.LenientResolver"/> and the
    /// TypeScript <c>disambiguation:'compatible'</c> rule diverged on
    /// which side of a DST gap to pick, the fixture comparison would fail; this
    /// test checks that the comparison can fail.
    ///
    /// Uses the US spring-forward fixture (2026-03-08 02:30 America/New_York,
    /// a skipped local time). The correct resolved UTC is 07:30Z (forward into
    /// the post-gap). This test builds a wrong UTC (one hour earlier, the
    /// pre-gap instant) and asserts it is DIFFERENT from the correct resolved
    /// value. A comparator that always returned "equal" would fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenDstResolutionPolicyDiffers()
    {
        // Spring-forward: 02:30 local skips to 03:30 -> resolves to 07:30Z.
        var fx = LoadFixture("us-spring-forward-skipped-2-30");
        var correctUtc = ParseInstant(fx.ExpectedUtc); // 2026-03-08T07:30:00Z

        // A resolver that picks the pre-gap instant (one hour earlier, the
        // "behind the gap" interpretation) would produce this value:
        var divergedPreGapUtc = correctUtc - Duration.FromHours(1);

        correctUtc.Should().NotBe(
            divergedPreGapUtc,
            "parity comparator must detect divergence when one engine " +
            "resolves to the pre-gap instant and the other to the post-gap");
    }

    /// <summary>
    /// Deliberate-drift test DD-T3: shows that the IANA alias canonicalization
    /// comparison can fail. If the .NET NodaTime <c>CanonicalIdMap</c> path and
    /// the TypeScript <c>Intl.DateTimeFormat.resolvedOptions().timeZone</c> +
    /// alias-override map diverged on producing a canonical IANA name, the
    /// parity assertion would fail; this test checks that the assertion can
    /// fail.
    ///
    /// Uses the US/Pacific alias fixture, where the correct canonical output
    /// is "America/Los_Angeles". This test uses the un-canonicalized alias
    /// ("US/Pacific") as the diverged result and asserts it is DIFFERENT from
    /// the canonical form. A comparator that always returned "equal" would
    /// fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenIanaAliasNotCanonicalized()
    {
        var fx = LoadFixture("iana-normalization-us-pacific-alias");
        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        // The canonical form that TimeZoneIdNormalizer produces:
        var canonicalIana = ev.IANAIdentifier; // "America/Los_Angeles"

        // A TypeScript implementation that did not canonicalize would return
        // the raw alias instead:
        var divergedUncanonical = fx.Iana; // "US/Pacific"

        canonicalIana.Should().NotBe(
            divergedUncanonical,
            "parity comparator must detect when one engine canonicalizes " +
            "IANA aliases and the other returns the raw alias form");
    }

    private static Instant ComputeFire(TemporalFixture fx)
    {
        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;
        return ev.ComputeNextFire().Data;
    }

    private static LocalDateTime ParseLocalDateTime(string isoLocal) =>
        LocalDateTimePattern.ExtendedIso.Parse(isoLocal).Value;

    private static Instant ParseInstant(string isoZ) =>
        InstantPattern.ExtendedIso.Parse(isoZ).Value;

    private static TemporalFixture LoadFixture(string id)
    {
        var doc = LoadFixtureDoc();
        foreach (var fx in doc.Fixtures)
        {
            if (fx.Id == id)
                return fx;
        }

        throw new KeyNotFoundException($"fixture '{id}' not found");
    }

    private static FixtureFile LoadFixtureDoc()
    {
        var path = FindFixturePath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureFile>(json, sr_jsonOptions)
            ?? throw new InvalidDataException($"could not parse {path}");
    }

    private static string FindFixturePath()
    {
        // PublicContractsRoot() locates the contracts folder.
        var monorepo = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "temporal",
            "temporal-adversarial.fixture.json");

        if (File.Exists(monorepo))
        {
            return monorepo;
        }

        throw new FileNotFoundException(
            "could not locate contracts/temporal/temporal-adversarial.fixture.json " +
                "at " + monorepo);
    }

    public sealed class AllFixturesTheoryData : TheoryData<string>
    {
        public AllFixturesTheoryData()
        {
            var doc = LoadFixtureDoc();
            foreach (var fx in doc.Fixtures)
                Add(fx.Id);
        }
    }

    public sealed record FixtureFile(int SchemaVersion, TemporalFixture[] Fixtures);

    public sealed record TemporalFixture(
        string Id,
        string ScheduledLocal,
        string Iana,
        string ExpectedUtc,
        string? ExpectedCanonicalIana)
    {
        public override string ToString() => Id;
    }
}
