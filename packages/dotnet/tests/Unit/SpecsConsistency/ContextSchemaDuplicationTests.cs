// -----------------------------------------------------------------------
// <copyright file="ContextSchemaDuplicationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// The schema in <c>contracts/auth-context</c> and the schema in
/// <c>contracts/request-context</c> are one schema kept in two directories so each
/// spec directory stands alone without a symlink, and
/// <c>contracts/request-context/schema.json</c> states that invariant in its own
/// opening description: the copies stay identical apart from that description.
/// </summary>
/// <remarks>
/// Both schemas declare <c>additionalProperties: false</c>, so a keyword added
/// to one and not the other makes a spec valid in one directory and invalid in
/// the other, which contradicts the claim that the auth-context and
/// request-context schemas are one.
/// </remarks>
public sealed class ContextSchemaDuplicationTests
{
    [Fact]
    public void TheContextSchemas_DifferOnlyByTheirOpeningDescription()
    {
        var authContext = ReadWithoutTopLevelDescription("auth-context");
        var requestContext = ReadWithoutTopLevelDescription("request-context");

        requestContext.Should().Be(authContext);
    }

    [Fact]
    public void RequestContextSchema_StatesTheDuplicationAndItsCanonicalSource()
    {
        var description = TopLevelDescription("request-context");

        description.Should().Contain("contracts/auth-context/schema.json");
    }

    private static string SchemaPath(string directory) =>
        Path.Combine(TestPaths.PublicContractsRoot(), directory, "schema.json");

    private static string TopLevelDescription(string directory)
    {
        var node = JsonNode.Parse(File.ReadAllText(SchemaPath(directory)))!.AsObject();

        return node["description"]!.GetValue<string>();
    }

    /// <summary>
    /// The schema with its own opening description removed, rendered so two
    /// files that carry the same keywords compare equal whatever their
    /// whitespace.
    /// </summary>
    /// <param name="directory">The contracts directory holding the schema.</param>
    /// <returns>The normalized schema text.</returns>
    private static string ReadWithoutTopLevelDescription(string directory)
    {
        var node = JsonNode.Parse(File.ReadAllText(SchemaPath(directory)))!.AsObject();
        node.Remove("description");

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
