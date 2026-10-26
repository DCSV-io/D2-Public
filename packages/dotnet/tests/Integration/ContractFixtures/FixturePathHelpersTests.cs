// -----------------------------------------------------------------------
// <copyright file="FixturePathHelpersTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System.IO;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

public sealed class FixturePathHelpersTests
{
    [Fact]
    public void FixturePath_ResolvesUnderTheContractTestsFixturesOfThePublicTypescriptTree()
    {
        var path = FixturePathHelpers.FixturePath("headers", "common");

        path.Should().Be(Path.Combine(
            TestPaths.PublicPackagesTypescriptRoot(),
            "contract-tests",
            "fixtures",
            "headers",
            "common.json"));
    }

    [Fact]
    public void FixturePath_ResolvesToACommittedFixtureInThisLayout()
    {
        File.Exists(FixturePathHelpers.FixturePath("headers", "common")).Should().BeTrue();
    }
}
