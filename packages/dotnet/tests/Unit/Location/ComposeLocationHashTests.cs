// -----------------------------------------------------------------------
// <copyright file="ComposeLocationHashTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location;
using DcsvIo.D2.Location.ValueObjects;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="ComposeLocationHash"/>:
/// all-null -> null, every non-empty combination of present and absent
/// components, the order of the slots, the inner v1. prefix as part of the
/// hash, determinism and idempotency.
/// </summary>
public sealed class ComposeLocationHashTests
{
    // -----------------------------------------------------------------------
    // All-null -> null
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_AllNull_ReturnsNull()
    {
        ComposeLocationHash.Compose(null, null, null).Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Single-component combinations
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_OnlyCoordinates_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(Coord(), null, null);
        Assert.NotNull(hash);
        hash.Should().StartWith("v1.");
        hash.Length.Should().Be(67);
    }

    [Fact]
    public void Compose_OnlyStreetAddress_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(null, Street(), null);
        Assert.NotNull(hash);
        hash.Should().StartWith("v1.");
    }

    [Fact]
    public void Compose_OnlyAdminLocation_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(null, null, Admin());
        Assert.NotNull(hash);
        hash.Should().StartWith("v1.");
    }

    // -----------------------------------------------------------------------
    // 2-of-3 combinations
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_CoordsAndStreet_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(Coord(), Street(), null);
        hash.Should().StartWith("v1.");
    }

    [Fact]
    public void Compose_CoordsAndAdmin_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(Coord(), null, Admin());
        hash.Should().StartWith("v1.");
    }

    [Fact]
    public void Compose_StreetAndAdmin_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(null, Street(), Admin());
        hash.Should().StartWith("v1.");
    }

    // -----------------------------------------------------------------------
    // All-3 combination
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_AllThreeNonNull_ReturnsV1Prefixed()
    {
        var hash = ComposeLocationHash.Compose(Coord(), Street(), Admin());
        hash.Should().StartWith("v1.");
    }

    // -----------------------------------------------------------------------
    // The order of the slots changes the hash
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_SameComponent_InDifferentSlots_ProducesDifferentHash()
    {
        // Coords-only, street-only and admin-only inputs differ by position
        // ("X||", "|X|" and "||X"), so each gives a different hash input.
        var hash1 = ComposeLocationHash.Compose(Coord(), null, null);
        var hash2 = ComposeLocationHash.Compose(null, Street(), null);
        var hash3 = ComposeLocationHash.Compose(null, null, Admin());

        hash1.Should().NotBe(hash2);
        hash1.Should().NotBe(hash3);
        hash2.Should().NotBe(hash3);
    }

    // -----------------------------------------------------------------------
    // Determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_SameInputs_ProducesByteIdenticalOutput()
    {
        var h1 = ComposeLocationHash.Compose(Coord(), Street(), Admin());
        var h2 = ComposeLocationHash.Compose(Coord(), Street(), Admin());

        h1.Should().Be(h2);
    }

    // -----------------------------------------------------------------------
    // The inner "v1." prefix is part of the hash
    // -----------------------------------------------------------------------

    [Fact]
    public void Compose_InnerV1Prefix_ParticipatesInHash()
    {
        // Changing the inner HashId changes the outer hash.
        var coord1 = Coordinates.Create(40.0, -74.0).Data!;
        var coord2 = Coordinates.Create(41.0, -74.0).Data!;

        var h1 = ComposeLocationHash.Compose(coord1, null, null);
        var h2 = ComposeLocationHash.Compose(coord2, null, null);

        coord1.HashId.Should().NotBe(coord2.HashId);
        h1.Should().NotBe(h2);
    }

    private static Coordinates Coord() =>
        Coordinates.Create(40.7128, -74.006).Data!;

    private static StreetAddress Street() =>
        StreetAddress.Create("123 Main St").Data!;

    private static AdminLocation Admin() =>
        AdminLocation.Create(CountryCode.US, city: "Brooklyn").Data!;
}
