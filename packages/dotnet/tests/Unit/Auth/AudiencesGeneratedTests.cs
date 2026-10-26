// -----------------------------------------------------------------------
// <copyright file="AudiencesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Tests for the public <c>Audiences</c> catalog, which contains the entries of the public
/// audiences spec.
/// </summary>
public sealed class AudiencesGeneratedTests
{
    [Fact]
    public void Audiences_TypeExists()
    {
        var audiencesType = typeof(Audiences);

        audiencesType.Should().NotBeNull();
        audiencesType.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        audiencesType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Audiences_PublicCatalog_HoldsExactlyThePublicSpecEntries()
    {
        var spec = ReadPublicAudiencesSpec();

        Audiences.AllUrls.Should().BeEquivalentTo(spec.Select(e => e.Url));
        Audiences.ByName.Keys.Should().BeEquivalentTo(spec.Select(e => e.Name));
    }

    [Theory]
    [InlineData("https://orders.example.internal")]
    [InlineData("https://inventory.example.internal")]
    [InlineData("https://shipping.example.internal")]
    [InlineData("https://billing.example.internal")]
    public void IsKnown_UrlTheSpecDoesNotDeclare_ReturnsFalse(string url)
    {
        Audiences.IsKnown(url).Should().BeFalse();
    }

    [Fact]
    public void IsKnown_EmptyString_ReturnsFalse()
    {
        Audiences.IsKnown(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Audiences.Resolve("Catalog").Should().BeNull();
        Audiences.Resolve("not-a-real-audience").Should().BeNull();
    }

    private static (string Name, string Url)[] ReadPublicAudiencesSpec()
    {
        var specPath = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "auth-audiences",
            "audiences.spec.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(specPath));

        return
        [
            .. doc.RootElement.GetProperty("audiences").EnumerateArray().Select(entry => (
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("url").GetString()!)),
        ];
    }
}
