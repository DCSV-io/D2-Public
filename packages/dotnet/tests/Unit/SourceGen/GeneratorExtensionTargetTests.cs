// -----------------------------------------------------------------------
// <copyright file="GeneratorExtensionTargetTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen;

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using DcsvIo.D2.I18n.SourceGen;
using DcsvIo.D2.Messaging.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AudiencesIds = DcsvIo.D2.Auth.Audiences.SourceGen.DiagnosticIds;
using EncryptionDomainsIds = DcsvIo.D2.EncryptionDomains.SourceGen.DiagnosticIds;
using I18nIds = DcsvIo.D2.I18n.SourceGen.DiagnosticIds;
using MqIds = DcsvIo.D2.Messaging.SourceGen.DiagnosticIds;
using ScopesIds = DcsvIo.D2.Auth.Scopes.SourceGen.DiagnosticIds;

/// <summary>
/// The extension-target selection that the scopes, audiences,
/// encryption-domains, translation-key and messaging generators share: the
/// public assembly name selects the public output and ignores the properties,
/// a valid namespace and class prefix select the extension output, an empty
/// or whitespace value counts as absent, and an invalid value or a prefix
/// without a namespace reports a diagnostic and produces no output. Every
/// case runs once per generator, with invented assembly and namespace names.
/// </summary>
public sealed class GeneratorExtensionTargetTests
{
    private const string _FIXTURE_ASSEMBLY = "Fixture.Extension.Host";
    private const string _NAMESPACE = "Example.Ext";
    private const string _PREFIX = "Sample";

    private static readonly Dictionary<string, ExtensionTargetGeneratorCaseFixture> sr_cases =
        new(StringComparer.Ordinal)
        {
            ["Scopes"] = new(
                () => new ScopesGenerator(),
                "DcsvIo.D2.Auth.Abstractions",
                "D2ScopesExtensionNamespace",
                "D2ScopesExtensionClassPrefix",
                [(
                    "contracts/auth-scopes/scopes.spec.json",
                    """
                    { "scopes": [ { "name": "orders.read", "impersonationBlocked": false,
                      "grantedTo": { "*": ["*"] } } ] }
                    """)],
                [
                    """
                    namespace DcsvIo.D2.Auth.Abstractions
                    {
                        public enum OrgType { Admin }
                        public enum Role { Owner }
                    }
                    """,
                ],
                ["Scopes.g.cs"],
                ["Scopes"],
                "D2SCP011",
                "D2SCP012",
                "D2SCP013"),
            ["Audiences"] = new(
                () => new AudiencesGenerator(),
                "DcsvIo.D2.Auth.Abstractions",
                "D2AudiencesExtensionNamespace",
                "D2AudiencesExtensionClassPrefix",
                [(
                    "contracts/auth-audiences/audiences.spec.json",
                    """{ "audiences": [ { "name": "Orders", "url": "https://orders.example.internal" } ] }""")],
                [],
                ["Audiences.g.cs"],
                ["Audiences"],
                "D2AUD007",
                "D2AUD008",
                "D2AUD009"),
            ["EncryptionDomains"] = new(
                () => new EncryptionDomainsGenerator(),
                "DcsvIo.D2.Encryption",
                "D2EncryptionDomainsExtensionNamespace",
                "D2EncryptionDomainsExtensionClassPrefix",
                [(
                    "contracts/encryption-domains/encryption-domains.spec.json",
                    """
                    { "domains": [ { "constName": "PLAINTEXT", "value": "plaintext", "doc": "d" } ] }
                    """)],
                [],
                ["EncryptionDomains.g.cs"],
                ["EncryptionDomains"],
                "D2ED010",
                "D2ED011",
                "D2ED012"),
            ["TK"] = new(
                () => new TKGenerator(),
                "DcsvIo.D2.I18n.Keys",
                "D2TkExtensionNamespace",
                "D2TkExtensionClassPrefix",
                [("contracts/messages/en-US.json", """{ "common_errors_NOT_FOUND": "Not found." }""")],
                [],
                ["TK.g.cs"],
                ["TK"],
                "D2I18N007",
                "D2I18N008",
                "D2I18N009"),
            ["Mq"] = new(
                () => new MqGenerator(),
                "DcsvIo.D2.Messaging.Abstractions",
                "D2MqExtensionNamespace",
                "D2MqExtensionClassPrefix",
                [
                    ("contracts/mq-messages/mq-messages.spec.json", """{ "messages": [] }"""),
                    (
                        "contracts/mq-subscriptions/mq-subscriptions.spec.json",
                        """{ "subscriptions": [] }"""),
                ],
                [],
                ["MqMessages.g.cs", "MqSubscriptions.g.cs"],
                ["MqMessages", "MqSubscriptions"],
                "D2MQ014",
                "D2MQ015",
                "D2MQ016"),
        };

    public static TheoryData<string> Generators => [.. sr_cases.Keys];

    public static TheoryData<string, string> AbsentValues =>
        Cross([string.Empty, "   ", "\t \r\n"]);

    public static TheoryData<string, string> InvalidNamespaces =>
        Cross(["1Bad", "A..B", "Example.", ".Example", "Example.class", "Exa mple", "Example-Ext"]);

    public static TheoryData<string, string> ValidNamespaces =>
        Cross(["Ext", "A1.B2.C3", "_Ext.record"]);

    public static TheoryData<string, string> InvalidPrefixes =>
        Cross(["1x", "class", "Sam-ple", "Sam ple", "A.B"]);

    [Theory]
    [MemberData(nameof(Generators))]
    public void ExtensionProperties_ValidValues_EmitPrefixedClassesUnderTheNamespace(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, _NAMESPACE, _PREFIX));

        result.Diagnostics.Should().BeEmpty();
        HintNames(result).Should().Equal(ExtensionHints(testCase, _PREFIX));
        result.GeneratedTrees.Should().AllSatisfy(t =>
            t.ToString().Should().Contain("namespace Example.Ext;"));
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void ExtensionProperties_ValidValues_DeclareEachPrefixedClass(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, _NAMESPACE, _PREFIX));

        var text = string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
        foreach (var baseName in testCase.BaseClassNames)
            text.Should().MatchRegex(@"\bclass Sample" + baseName + @"\b");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void NoProperties_UnrelatedAssembly_EmitsNothingAndReportsNothing(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, new Dictionary<string, string>());

        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(AbsentValues))]
    public void EmptyOrWhitespaceValues_CountAsAbsent(string name, string value)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, value, value));

        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void PaddedValues_AreTrimmed(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(
            testCase,
            _FIXTURE_ASSEMBLY,
            Properties(testCase, "  " + _NAMESPACE + "\t", " " + _PREFIX + " "));

        result.Diagnostics.Should().BeEmpty();
        HintNames(result).Should().Equal(ExtensionHints(testCase, _PREFIX));
        result.GeneratedTrees.Should().AllSatisfy(t =>
            t.ToString().Should().Contain("namespace Example.Ext;"));
    }

    [Theory]
    [MemberData(nameof(ValidNamespaces))]
    public void ValidNamespace_AtTheIdentifierRules_IsAdmitted(string name, string ns)
    {
        // The admitting side of the namespace check: a one-segment namespace, a
        // segment with digits after the first character, a leading underscore
        // and a contextual keyword are all valid C# namespaces.
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, ns, _PREFIX));

        result.Diagnostics.Should().BeEmpty();
        HintNames(result).Should().Equal(ExtensionHints(testCase, _PREFIX));
        result.GeneratedTrees.Should().AllSatisfy(t =>
            t.ToString().Should().Contain("namespace " + ns + ";"));
    }

    [Theory]
    [MemberData(nameof(InvalidNamespaces))]
    public void InvalidNamespace_ReportsTheNamespaceErrorAndEmitsNothing(string name, string ns)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, ns, _PREFIX));

        result.GeneratedTrees.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(testCase.InvalidNamespaceId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Contain(testCase.NamespaceProperty)
            .And.Contain("'" + ns + "'");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void NamespaceWithoutPrefix_ReportsThePrefixErrorAndEmitsNothing(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(
            testCase,
            _FIXTURE_ASSEMBLY,
            new Dictionary<string, string> { [testCase.NamespaceProperty] = _NAMESPACE });

        result.GeneratedTrees.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(testCase.InvalidClassPrefixId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Contain(testCase.ClassPrefixProperty)
            .And.Contain(testCase.NamespaceProperty);
    }

    [Theory]
    [MemberData(nameof(InvalidPrefixes))]
    public void InvalidPrefix_ReportsThePrefixErrorAndEmitsNothing(string name, string prefix)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, _NAMESPACE, prefix));

        result.GeneratedTrees.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(testCase.InvalidClassPrefixId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Contain(testCase.ClassPrefixProperty)
            .And.Contain("'" + prefix + "'");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void InvalidNamespaceAndPrefix_ReportsBothErrors(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, _FIXTURE_ASSEMBLY, Properties(testCase, "1Bad", "class"));

        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal).Should().Equal(
            testCase.InvalidNamespaceId,
            testCase.InvalidClassPrefixId);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void PrefixWithoutNamespace_ReportsTheWarningAndEmitsNothing(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(
            testCase,
            _FIXTURE_ASSEMBLY,
            new Dictionary<string, string> { [testCase.ClassPrefixProperty] = _PREFIX });

        result.GeneratedTrees.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(testCase.ClassPrefixWithoutNamespaceId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Contain(testCase.ClassPrefixProperty)
            .And.Contain(testCase.NamespaceProperty)
            .And.Contain("'Sample'");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void PublicAssembly_WithValidProperties_EmitsThePublicOutput(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(
            testCase,
            testCase.PublicAssemblyName,
            Properties(testCase, _NAMESPACE, _PREFIX));

        result.Diagnostics.Should().BeEmpty();
        HintNames(result).Should().Equal(testCase.PublicHintNames);
        result.GeneratedTrees.Should().AllSatisfy(t =>
            t.ToString().Should().NotContain("Example.Ext"));
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void PublicAssembly_WithInvalidProperties_IgnoresThemAndReportsNothing(string name)
    {
        var testCase = sr_cases[name];

        var result = Run(testCase, testCase.PublicAssemblyName, Properties(testCase, "1Bad", "class"));

        result.Diagnostics.Should().BeEmpty();
        HintNames(result).Should().Equal(testCase.PublicHintNames);
    }

    [Theory]
    [InlineData("D2SCP011", typeof(ScopesIds), nameof(ScopesIds.InvalidExtensionNamespace))]
    [InlineData("D2SCP012", typeof(ScopesIds), nameof(ScopesIds.InvalidExtensionClassPrefix))]
    [InlineData(
        "D2SCP013", typeof(ScopesIds), nameof(ScopesIds.ExtensionClassPrefixWithoutNamespace))]
    [InlineData("D2AUD007", typeof(AudiencesIds), nameof(AudiencesIds.InvalidExtensionNamespace))]
    [InlineData(
        "D2AUD008", typeof(AudiencesIds), nameof(AudiencesIds.InvalidExtensionClassPrefix))]
    [InlineData(
        "D2AUD009",
        typeof(AudiencesIds),
        nameof(AudiencesIds.ExtensionClassPrefixWithoutNamespace))]
    [InlineData(
        "D2ED010",
        typeof(EncryptionDomainsIds),
        nameof(EncryptionDomainsIds.InvalidExtensionNamespace))]
    [InlineData(
        "D2ED011",
        typeof(EncryptionDomainsIds),
        nameof(EncryptionDomainsIds.InvalidExtensionClassPrefix))]
    [InlineData(
        "D2ED012",
        typeof(EncryptionDomainsIds),
        nameof(EncryptionDomainsIds.ExtensionClassPrefixWithoutNamespace))]
    [InlineData("D2I18N007", typeof(I18nIds), nameof(I18nIds.InvalidExtensionNamespace))]
    [InlineData("D2I18N008", typeof(I18nIds), nameof(I18nIds.InvalidExtensionClassPrefix))]
    [InlineData(
        "D2I18N009", typeof(I18nIds), nameof(I18nIds.ExtensionClassPrefixWithoutNamespace))]
    [InlineData("D2MQ014", typeof(MqIds), nameof(MqIds.InvalidExtensionNamespace))]
    [InlineData("D2MQ015", typeof(MqIds), nameof(MqIds.InvalidExtensionClassPrefix))]
    [InlineData("D2MQ016", typeof(MqIds), nameof(MqIds.ExtensionClassPrefixWithoutNamespace))]
    public void ExtensionDiagnosticIds_HaveStableValues(
        string expectedId,
        Type idsType,
        string constantName)
    {
        idsType
            .GetField(constantName, BindingFlags.Public | BindingFlags.Static)!
            .GetRawConstantValue()
            .Should().Be(expectedId);
    }

    private static TheoryData<string, string> Cross(string[] values)
    {
        var data = new TheoryData<string, string>();

        foreach (var name in sr_cases.Keys)
        {
            foreach (var value in values)
                data.Add(name, value);
        }

        return data;
    }

    private static Dictionary<string, string> Properties(
        ExtensionTargetGeneratorCaseFixture testCase,
        string ns,
        string prefix) =>
        new()
        {
            [testCase.NamespaceProperty] = ns,
            [testCase.ClassPrefixProperty] = prefix,
        };

    private static string[] ExtensionHints(
        ExtensionTargetGeneratorCaseFixture testCase,
        string prefix) =>
        [
            .. testCase.BaseClassNames
                .Select(n => prefix + n + ".g.cs")
                .Order(StringComparer.Ordinal),
        ];

    private static string[] HintNames(GeneratorDriverRunResult result) =>
        [
            .. result.GeneratedTrees
                .Select(t => Path.GetFileName(t.FilePath))
                .Order(StringComparer.Ordinal),
        ];

    private static GeneratorDriverRunResult Run(
        ExtensionTargetGeneratorCaseFixture testCase,
        string assemblyName,
        IReadOnlyDictionary<string, string> properties)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: testCase.Sources.Select(s => CSharpSyntaxTree.ParseText(s)),
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [testCase.CreateGenerator().AsSourceGenerator()],
            additionalTexts:
            [
                .. testCase.AdditionalFiles.Select(f =>
                    (AdditionalText)new InMemoryAdditionalTextFixture(f.Path, f.Content)),
            ],
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(properties));

        return driver.RunGenerators(compilation).GetRunResult();
    }
}
