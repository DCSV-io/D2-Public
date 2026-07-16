// -----------------------------------------------------------------------
// <copyright file="ErrorCodesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using ResultErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;

/// <summary>
/// CI-enforced byte-parity gate for the public Result ErrorCodes / D2Result
/// factory catalog. Auth ErrorCodes/AuthFailures parity lives on the private
/// composition host as <c>AuthErrorCodesOutputParityTests</c>.
/// </summary>
public sealed class ErrorCodesOutputParityTests
{
    private const string _RESULT_ASSEMBLY = "DcsvIo.D2.Result";
    private const string _RESULT_SPEC_NAME = "error-codes.spec.json";

    private static readonly string sr_generatedResultBase =
        Path.Combine(
            TestPaths.PublicPackagesDotnetRoot(),
            "result",
            "core",
            "Generated",
            "DcsvIo.D2.Result.ErrorCodes.SourceGen",
            "DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator");

    [Fact]
    public void ErrorCodes_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateResult()["ErrorCodes.g.cs"];
        var committed = ReadCommitted(Path.Combine(sr_generatedResultBase, "ErrorCodes.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed ErrorCodes.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    [Theory]
    [InlineData("ErrorCodes.g.cs")]
    [InlineData("D2Result.Factories.g.cs")]
    [InlineData("D2Result.Generic.Factories.g.cs")]
    [InlineData("D2Result.Booleans.g.cs")]
    public void ResultCatalog_EveryGeneratedFile_MatchesCommittedFile(string fileName)
    {
        var regenerated = RegenerateResult()[fileName];
        var committed = ReadCommitted(Path.Combine(sr_generatedResultBase, fileName));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                $"the committed {fileName} must be byte-identical to a fresh "
                + "generation from the spec; run dotnet build to regenerate");
    }

    private static Dictionary<string, string> RegenerateResult()
    {
        var specPath = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "error-codes",
            "error-codes.spec.json");
        var enUsPath = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "messages",
            "en-US.json");

        var specJson = File.ReadAllText(specPath);
        var enUsJson = File.ReadAllText(enUsPath);

        return RunGenerator(
            _RESULT_ASSEMBLY,
            new ResultErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_RESULT_SPEC_NAME, specJson),
                new FileBackedAdditionalText("messages/en-US.json", enUsJson),
            ]);
    }

    private static Dictionary<string, string> RunGenerator(
        string assemblyName,
        ISourceGenerator generator,
        AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation);

        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean spec must produce no build-time diagnostics");

        return result.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    private static string ReadCommitted(string path) => File.ReadAllText(path);

    private static string Normalize(string source) =>
        source.Replace("\r\n", "\n").Replace("\r", "\n");

    private sealed class FileBackedAdditionalText : AdditionalText
    {
        private readonly SourceText r_text;

        public FileBackedAdditionalText(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
