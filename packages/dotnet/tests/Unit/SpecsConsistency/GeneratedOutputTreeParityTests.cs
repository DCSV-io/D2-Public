// -----------------------------------------------------------------------
// <copyright file="GeneratedOutputTreeParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using DcsvIo.D2.Context.SourceGen;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using DcsvIo.D2.EncryptionFrame.SourceGen;
using DcsvIo.D2.Geo.SourceGen;
using DcsvIo.D2.I18n.SourceGen;
using DcsvIo.D2.Messaging.SourceGen;
using DcsvIo.D2.Result.Envelope.SourceGen;
using DcsvIo.D2.Tests.Unit.Auth;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using DcsvIo.D2.Validation.SourceGen;
using DcsvIo.D2.WireShapes.SourceGen;
using Xunit;
using ResultErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;

/// <summary>
/// Snapshot tests for the generators whose suites otherwise assert markers in
/// the emitted text. Each case runs the generator over the real spec its host
/// package builds from and compares every file it produces with the committed
/// generated file as a parsed syntax tree. Drift trials on the protocol
/// audiences spec (a changed value, a missing entry and an extra entry) show
/// that the gate fails and reports the drifted value; each drift is made in memory,
/// so the spec file needs no revert.
/// </summary>
public sealed class GeneratedOutputTreeParityTests
{
    private const string _REGENERATE = "run dotnet build to regenerate the committed file";

    [Fact]
    public void Audiences_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new AudiencesGenerator(),
            "DcsvIo.D2.Auth.Abstractions",
            [Spec("auth-audiences", "audiences.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "auth/abstractions",
            "DcsvIo.D2.Auth.Audiences.SourceGen",
            "DcsvIo.D2.Auth.Audiences.SourceGen.AudiencesGenerator");
    }

    [Fact]
    public void ProtocolAudiences_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new ProtocolAudiencesGenerator(),
            "DcsvIo.D2.Auth.Abstractions",
            [Spec("auth-protocol-audiences", "protocol-audiences.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "auth/abstractions",
            "DcsvIo.D2.Auth.ProtocolAudiences.SourceGen",
            "DcsvIo.D2.Auth.ProtocolAudiences.SourceGen.ProtocolAudiencesGenerator");
    }

    [Fact]
    public void EncryptionFrame_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new EncryptionFrameGenerator(),
            "DcsvIo.D2.Encryption",
            [Spec("encryption-frame", "encryption-frame.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "encryption/core",
            "DcsvIo.D2.EncryptionFrame.SourceGen",
            "DcsvIo.D2.EncryptionFrame.SourceGen.EncryptionFrameGenerator");
    }

    [Fact]
    public void SealedFrame_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new SealedFrameGenerator(),
            "DcsvIo.D2.Encryption",
            [Spec("encryption-frame-sealed", "encryption-frame-sealed.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "encryption/core",
            "DcsvIo.D2.EncryptionFrame.SourceGen",
            "DcsvIo.D2.EncryptionFrame.SourceGen.SealedFrameGenerator");
    }

    [Fact]
    public void TK_RegeneratedOutput_MatchesCommittedTree()
    {
        var messages = Directory
            .EnumerateFiles(TestPaths.MessagesDirectory(), "*.json")
            .Select(p => ("contracts/messages/" + Path.GetFileName(p), File.ReadAllText(p)));

        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new TKGenerator(),
            "DcsvIo.D2.I18n.Keys",
            messages);

        ShouldMatchCommitted(
            produced,
            "i18n/keys",
            "DcsvIo.D2.I18n.SourceGen",
            "DcsvIo.D2.I18n.SourceGen.TKGenerator");
    }

    [Fact]
    public void FieldConstraints_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new FieldConstraintsGenerator(),
            "DcsvIo.D2.Validation.Abstractions",
            [Spec("validation", "field-constraints.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "validation/abstractions",
            "DcsvIo.D2.Validation.SourceGen",
            "DcsvIo.D2.Validation.SourceGen.FieldConstraintsGenerator");
    }

    [Fact]
    public void Mq_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new MqGenerator(),
            "DcsvIo.D2.Messaging.Abstractions",
            [
                Spec("mq-messages", "mq-messages.spec.json"),
                Spec("mq-subscriptions", "mq-subscriptions.spec.json"),
            ]);

        ShouldMatchCommitted(
            produced,
            "messaging/abstractions",
            "DcsvIo.D2.Messaging.SourceGen",
            "DcsvIo.D2.Messaging.SourceGen.MqGenerator");
    }

    [Theory]
    [InlineData("DcsvIo.D2.Context.Abstractions", "context/abstractions")]
    [InlineData("DcsvIo.D2.AuthContext.Abstractions", "auth/context-abstractions")]
    public void Context_RegeneratedOutput_MatchesCommittedTree(
        string assemblyName,
        string packageDirectory)
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new ContextGenerator(),
            assemblyName,
            [
                Spec("auth-context", "IAuthContext.spec.json"),
                Spec("request-context", "IRequestContext.spec.json"),
            ]);

        ShouldMatchCommitted(
            produced,
            packageDirectory,
            "DcsvIo.D2.Context.SourceGen",
            "DcsvIo.D2.Context.SourceGen.ContextGenerator");
    }

    [Theory]
    [InlineData("DcsvIo.D2.I18n.Abstractions", "i18n/abstractions", "tk-message", "tk-message.spec.json")]
    [InlineData("DcsvIo.D2.Result", "result/core", "input-error", "input-error.spec.json")]
    public void WireShapes_RegeneratedOutput_MatchesCommittedTree(
        string assemblyName,
        string packageDirectory,
        string specDirectory,
        string specFile)
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new WireShapesGenerator(),
            assemblyName,
            [Spec(specDirectory, specFile)]);

        ShouldMatchCommitted(
            produced,
            packageDirectory,
            "DcsvIo.D2.WireShapes.SourceGen",
            "DcsvIo.D2.WireShapes.SourceGen.WireShapesGenerator");
    }

    [Fact]
    public void Scopes_RegeneratedOutput_MatchesCommittedTree()
    {
        // Wildcard grantedTo expansion reads the OrgType and Role members from the
        // compilation, so the real enum sources go in beside the spec.
        var abstractions = Path.Combine(TestPaths.PublicPackagesDotnetRoot(), "auth", "abstractions");

        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new ScopesGenerator(),
            "DcsvIo.D2.Auth.Abstractions",
            [Spec("auth-scopes", "scopes.spec.json")],
            [
                File.ReadAllText(Path.Combine(abstractions, "OrgType.cs")),
                File.ReadAllText(Path.Combine(abstractions, "Role.cs")),
            ]);

        ShouldMatchCommitted(
            produced,
            "auth/abstractions",
            "DcsvIo.D2.Auth.Scopes.SourceGen",
            "DcsvIo.D2.Auth.Scopes.SourceGen.ScopesGenerator");
    }

    [Fact]
    public void ResultErrorCodes_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new ResultErrorCodesGenerator(),
            "DcsvIo.D2.Result",
            [
                Spec("error-codes", "error-codes.spec.json"),
                Spec("messages", "en-US.json"),
            ]);

        ShouldMatchCommitted(
            produced,
            "result/core",
            "DcsvIo.D2.Result.ErrorCodes.SourceGen",
            "DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator");
    }

    [Fact]
    public void D2ResultEnvelope_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new D2ResultEnvelopeGenerator(),
            "DcsvIo.D2.Result",
            [Spec("d2result-envelope", "d2result-envelope.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "result/core",
            "DcsvIo.D2.Result.Envelope.SourceGen",
            "DcsvIo.D2.Result.Envelope.SourceGen.D2ResultEnvelopeGenerator");
    }

    [Fact]
    public void EncryptionDomains_RegeneratedOutput_MatchesCommittedTree()
    {
        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new EncryptionDomainsGenerator(),
            "DcsvIo.D2.Encryption",
            [Spec("encryption-domains", "encryption-domains.spec.json")]);

        ShouldMatchCommitted(
            produced,
            "encryption/core",
            "DcsvIo.D2.EncryptionDomains.SourceGen",
            "DcsvIo.D2.EncryptionDomains.SourceGen.EncryptionDomainsGenerator");
    }

    [Theory]
    [InlineData("DcsvIo.D2.Geo.Abstractions", "geo/abstractions")]
    [InlineData("DcsvIo.D2.Geo.Default", "geo/default")]
    public void Geo_RegeneratedOutput_MatchesCommittedTree(
        string assemblyName,
        string packageDirectory)
    {
        var specs = Directory
            .EnumerateFiles(Path.Combine(TestPaths.PublicContractsRoot(), "geo"), "*.spec.json")
            .Select(p => ("contracts/geo/" + Path.GetFileName(p), File.ReadAllText(p)));

        var produced = GeneratedSourceTreeTestKit.RunGenerator(
            new GeoGenerator(),
            assemblyName,
            specs);

        ShouldMatchCommitted(
            produced,
            packageDirectory,
            "DcsvIo.D2.Geo.SourceGen",
            "DcsvIo.D2.Geo.SourceGen.GeoGenerator");
    }

    [Fact]
    public void ProtocolAudiences_DriftTrial_ChangedValue_GateNamesTheNewValue()
    {
        var drifted = RegenerateProtocolAudiences(audiences =>
            ByName(audiences, "D2_EDGE_SELF_AUDIENCE")["value"] = "d2-edge-drifted");

        ProtocolAudiencesGateFailureOver(drifted).Should().Contain("d2-edge-drifted");
    }

    [Fact]
    public void ProtocolAudiences_DriftTrial_MissingEntry_GateNamesTheMissingEntry()
    {
        var drifted = RegenerateProtocolAudiences(audiences =>
            audiences.Remove(ByName(audiences, "D2_EDGE_SELF_AUDIENCE")));

        ProtocolAudiencesGateFailureOver(drifted).Should().Contain("The self-audience for the operations of the entry service");
    }

    [Fact]
    public void ProtocolAudiences_DriftTrial_ExtraEntry_GateNamesTheExtraEntry()
    {
        var drifted = RegenerateProtocolAudiences(audiences => audiences.Add(new JsonObject
        {
            ["name"] = "D2_DRIFTED_AUDIENCE",
            ["value"] = "d2-drifted",
            ["description"] = "D2_DRIFTED_AUDIENCE is not in the real spec.",
        }));

        ProtocolAudiencesGateFailureOver(drifted).Should().Contain("D2_DRIFTED_AUDIENCE");
    }

    private static JsonObject ByName(JsonArray audiences, string name)
        => audiences
            .Select(n => n!.AsObject())
            .Single(o => string.Equals(o["name"]!.GetValue<string>(), name, StringComparison.Ordinal));

    /// <summary>
    /// Regenerates the protocol audiences from the real spec after applying a
    /// mutation to its entries in memory; the spec file is never written.
    /// </summary>
    private static string RegenerateProtocolAudiences(Action<JsonArray> mutate)
    {
        var (path, content) = Spec("auth-protocol-audiences", "protocol-audiences.spec.json");
        var root = JsonNode.Parse(content)!.AsObject();
        mutate(root["protocolAudiences"]!.AsArray());

        return GeneratedSourceTreeTestKit.RunGenerator(
                new ProtocolAudiencesGenerator(),
                "DcsvIo.D2.Auth.Abstractions",
                [(path, root.ToJsonString())])
            .Single().Value;
    }

    /// <summary>
    /// Runs the tree gate over a drifted generation and returns its failure
    /// message, failing the trial when the gate passes.
    /// </summary>
    private static string ProtocolAudiencesGateFailureOver(string drifted)
    {
        var committedText = File.ReadAllText(Path.Combine(
            [
                TestPaths.PublicPackagesDotnetRoot(),
                "auth",
                "abstractions",
                "Generated",
                "DcsvIo.D2.Auth.ProtocolAudiences.SourceGen",
                "DcsvIo.D2.Auth.ProtocolAudiences.SourceGen.ProtocolAudiencesGenerator",
                "WellKnownAudiences.g.cs",
            ]));

        var gate = () => GeneratedSourceTreeTestKit.ShouldMatchCommittedTree(
            drifted, committedText, "the drift trial's gate");

        return gate.Should().Throw<Exception>().Which.Message;
    }

    private static (string Path, string Content) Spec(string directory, string fileName)
        => (
            "contracts/" + directory + "/" + fileName,
            File.ReadAllText(Path.Combine(TestPaths.PublicContractsRoot(), directory, fileName)));

    private static void ShouldMatchCommitted(
        IReadOnlyDictionary<string, string> produced,
        string packageDirectory,
        string generatorAssembly,
        string generatorType)
    {
        var committedDirectory = Path.Combine(
            [
                TestPaths.PublicPackagesDotnetRoot(),
                .. packageDirectory.Split('/'),
                "Generated",
                generatorAssembly,
                generatorType,
            ]);

        var committedFiles = Directory
            .EnumerateFiles(committedDirectory, "*.g.cs")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        produced.Keys.Order(StringComparer.Ordinal).Should().Equal(
            committedFiles,
            "the generator produces exactly the committed files");

        foreach (var fileName in committedFiles)
        {
            GeneratedSourceTreeTestKit.ShouldMatchCommittedTree(
                produced[fileName],
                File.ReadAllText(Path.Combine(committedDirectory, fileName)),
                $"{generatorType} output for {fileName} must match the committed file; {_REGENERATE}");
        }
    }
}
