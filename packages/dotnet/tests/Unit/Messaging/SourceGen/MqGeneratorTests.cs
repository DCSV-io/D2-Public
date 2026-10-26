// -----------------------------------------------------------------------
// <copyright file="MqGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator tests for <see cref="MqGenerator"/>, driven through a
/// synthetic <see cref="CSharpGeneratorDriver"/> and not through the build. The
/// generator has two targets: the public one, selected by assembly name, and
/// an extension target, selected by the extension properties. The extension
/// target takes the union of every spec file it receives and rejects a constant
/// that two of them declare. <c>Run</c> passes the extension properties to the
/// compilation named <c>_EXTENSION_ASSEMBLY</c> and to no other.
/// </summary>
public sealed class MqGeneratorTests
{
    private const string _PUBLIC_ASSEMBLY = "DcsvIo.D2.Messaging.Abstractions";
    private const string _EXTENSION_ASSEMBLY = "Fixture.Messaging.Host";
    private const string _EXTRA_DOMAINS_TYPE = "Example.Ext.Encryption.SampleEncryptionDomains";

    private const string _EMPTY_MESSAGES = """
    { "messages": [] }
    """;

    private const string _EMPTY_SUBSCRIPTIONS = """
    { "subscriptions": [] }
    """;

    private const string _EXTENSION_MESSAGES = """
    {
      "messages": [
        {
          "constant": "FixtureRotated",
          "messageType": "DcsvIo.D2.Fixture.Events.FixtureRotatedEvent",
          "exchange": "d2.fixture.rotated",
          "exchangeType": "fanout",
          "encryption": "plaintext",
          "encryptionReason": "Fixture entry for the generator tests.",
          "defaultRoutingKey": ""
        }
      ]
    }
    """;

    private const string _SEALED_EXTENSION_MESSAGES = """
    {
      "messages": [
        {
          "constant": "SealedFixture",
          "messageType": "DcsvIo.D2.Fixture.Events.SealedFixtureEvent",
          "exchange": "d2.fixture.sealed",
          "exchangeType": "fanout",
          "encryption": "orders",
          "defaultRoutingKey": ""
        }
      ]
    }
    """;

    // The domain catalogs as source, so that a synthetic compilation holds the
    // symbols from which the generator reads its encryption whitelist. The
    // references of a target's assembly decide which catalog the target sees.
    private const string _FRAMEWORK_DOMAINS_SOURCE = """
    namespace DcsvIo.D2.Encryption;
    public static class EncryptionDomains
    {
        public const string PLAINTEXT = "plaintext";
        public const string FIXTURE_SEALED = "payload-fixture-sealed";
    }
    """;

    private const string _PRODUCT_DOMAINS_SOURCE = """
    namespace Example.Ext.Encryption;
    public static class SampleEncryptionDomains
    {
        public const string ORDERS = "orders";
        public const string PLAINTEXT = "plaintext";
        public const string FIXTURE_SEALED = "payload-fixture-sealed";
    }
    """;

    private const string _EXTENSION_SUBSCRIPTIONS = """
    {
      "subscriptions": [
        {
          "constant": "FixtureRefresh",
          "messageType": "DcsvIo.D2.Fixture.Events.FixtureRotatedEvent",
          "queueName": "fixture-refresher",
          "pattern": "FanoutExclusiveAutoDelete",
          "routingKeyBinding": "",
          "prefetch": 10,
          "idempotency": false
        }
      ]
    }
    """;

    [Fact]
    public void Generator_ForAnUnrelatedAssembly_EmitsNothing()
    {
        var result = Run("SomeOther.Assembly", Public()).GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_ForThePublicAssembly_EmitsTheFrameworkTypeNames()
    {
        var result = Run(_PUBLIC_ASSEMBLY, Public()).GetRunResult();

        var names = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        names.Should().Equal("MqMessages.g.cs", "MqSubscriptions.g.cs");
        Source(result, "MqMessages.g.cs").Should()
            .Contain("namespace DcsvIo.D2.Messaging;")
            .And.Contain("public static class MqMessages")
            .And.Contain("public static class MqMessagesRegistry");
        Source(result, "MqSubscriptions.g.cs").Should()
            .Contain("public static class MqSubscriptions")
            .And.Contain("public static class MqSubscriptionsRegistry");
    }

    [Fact]
    public void Generator_ForThePublicAssemblyWithZeroEntries_EmitsEmptyRegistries()
    {
        // The framework catalog ships with no entries. The registry types must
        // still exist, or the abstractions assembly and every assembly that
        // references it fails to compile.
        var result = Run(_PUBLIC_ASSEMBLY, Public()).GetRunResult();

        var source = Source(result, "MqMessages.g.cs");
        source.Should().Contain("ByConstant = new Dictionary<string, MqMessageDescriptor>");
        source.Should().NotContain("public const string");
    }

    [Fact]
    public void Generator_ForTheExtensionTarget_EmitsThePrefixedTypeNames()
    {
        var result = Run(_EXTENSION_ASSEMBLY, PublicAndExtension()).GetRunResult();

        var names = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        names.Should().Equal("SampleMqMessages.g.cs", "SampleMqSubscriptions.g.cs");
        Source(result, "SampleMqMessages.g.cs").Should()
            .Contain("namespace Example.Ext.Messaging;")
            .And.Contain("using DcsvIo.D2.Messaging;")
            .And.Contain("public static class SampleMqMessages")
            .And.Contain("public static class SampleMqMessagesRegistry");
        Source(result, "SampleMqSubscriptions.g.cs").Should()
            .Contain("public static class SampleMqSubscriptions")
            .And.Contain("public static class SampleMqSubscriptionsRegistry");
    }

    [Fact]
    public void Generator_ForTheExtensionTarget_UnionsEveryEntryOfBothHalves()
    {
        var result = Run(_EXTENSION_ASSEMBLY, PublicAndExtension()).GetRunResult();

        Source(result, "SampleMqMessages.g.cs").Should()
            .Contain("public const string FixtureRotated = \"FixtureRotated\";");
        Source(result, "SampleMqSubscriptions.g.cs").Should()
            .Contain("public const string FixtureRefresh = \"FixtureRefresh\";");
    }

    [Fact]
    public void Generator_WithOneMessageConstantInBothSpecs_ReportsD2MQ013AndEmitsNeither()
    {
        // This differs on purpose from the encryption generator, which keeps
        // the first constant in path order and reports nothing. Which file wins
        // there depends on where the repository sits on disk.
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [
                Spec("a/mq-messages.spec.json", _EXTENSION_MESSAGES),
                Spec("b/mq-messages.spec.json", _EXTENSION_MESSAGES),
                Spec("a/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
            ]).GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.CrossFileDuplicateConstant);
        result.Diagnostics
            .Single(d => d.Id == DiagnosticIds.CrossFileDuplicateConstant)
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain("FixtureRotated")
            .And.Contain("a/mq-messages.spec.json")
            .And.Contain("b/mq-messages.spec.json");

        Source(result, "SampleMqMessages.g.cs").Should()
            .NotContain("FixtureRotated");
    }

    [Fact]
    public void Generator_WithOneSubscriptionConstantInBothSpecs_ReportsD2MQ013()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [
                Spec("a/mq-messages.spec.json", _EXTENSION_MESSAGES),
                Spec("a/mq-subscriptions.spec.json", _EXTENSION_SUBSCRIPTIONS),
                Spec("b/mq-subscriptions.spec.json", _EXTENSION_SUBSCRIPTIONS),
            ]).GetRunResult();

        result.Diagnostics
            .Single(d => d.Id == DiagnosticIds.CrossFileDuplicateConstant)
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain("FixtureRefresh");

        Source(result, "SampleMqSubscriptions.g.cs").Should()
            .NotContain("FixtureRefresh");
    }

    [Fact]
    public void Generator_WithNoMessagesSpec_ReportsD2MQ009AndEmitsTheEmptyShell()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [Spec("a/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS)]).GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingMessagesSpecFile);
        Source(result, "SampleMqMessages.g.cs").Should()
            .Contain("namespace Example.Ext.Messaging;")
            .And.NotContain("class");
    }

    [Fact]
    public void Generator_WithNoSubscriptionsSpec_ReportsD2MQ010AndEmitsTheEmptyShell()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [Spec("a/mq-messages.spec.json", _EXTENSION_MESSAGES)]).GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSubscriptionsSpecFile);
        Source(result, "SampleMqSubscriptions.g.cs").Should()
            .Contain("namespace Example.Ext.Messaging;")
            .And.NotContain("class");
    }

    [Fact]
    public void Generator_WithAMalformedSpecInOneHalf_ReportsD2MQ001AndEmitsNoRegistry()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [
                Spec("a/mq-messages.spec.json", "{not valid"),
                Spec("a/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
            ]).GetRunResult();

        result.Diagnostics.Should().Contain(d => d.Id == DiagnosticIds.MalformedSpec);
        Source(result, "SampleMqMessages.g.cs").Should().NotContain("class");
    }

    [Fact]
    public void Generator_WithAMalformedSubscriptionsSpec_ReportsD2MQ001AndEmitsNoRegistry()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [
                Spec("a/mq-messages.spec.json", _EXTENSION_MESSAGES),
                Spec("a/mq-subscriptions.spec.json", "{not valid"),
            ]).GetRunResult();

        result.Diagnostics.Should().Contain(d => d.Id == DiagnosticIds.MalformedSpec);
        Source(result, "SampleMqSubscriptions.g.cs").Should().NotContain("class");
    }

    [Fact]
    public void Generator_RunTwiceOnTheSameSpecs_ProducesIdenticalOutput()
    {
        var first = SourcesOf(Run(_EXTENSION_ASSEMBLY, PublicAndExtension()).GetRunResult());
        var second = SourcesOf(Run(_EXTENSION_ASSEMBLY, PublicAndExtension()).GetRunResult());

        second.Should().Equal(first);
    }

    [Fact]
    public void Generator_ForTheExtensionTarget_IsUnaffectedByTheOrderTheSpecsArrive()
    {
        var forward = SourcesOf(Run(_EXTENSION_ASSEMBLY, PublicAndExtension()).GetRunResult());
        var reversed = SourcesOf(
            Run(_EXTENSION_ASSEMBLY, [.. PublicAndExtension().Reverse()]).GetRunResult());

        reversed.Should().Equal(forward);
    }

    [Fact]
    public void Generator_ForTheExtensionTargetWithAProductDomain_EmitsTheEntry()
    {
        var result = Run(
            _EXTENSION_ASSEMBLY,
            [
                Spec("public/mq-messages.spec.json", _EMPTY_MESSAGES),
                Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
                Spec("private/mq-messages.spec.json", _SEALED_EXTENSION_MESSAGES),
                Spec("private/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
            ],
            _FRAMEWORK_DOMAINS_SOURCE,
            _PRODUCT_DOMAINS_SOURCE).GetRunResult();

        result.Diagnostics.Should().NotContain(d => d.Id == DiagnosticIds.UnknownEncryption);
        Source(result, "SampleMqMessages.g.cs").Should()
            .Contain("public const string SealedFixture = \"SealedFixture\";")
            .And.Contain("\"orders\"");
    }

    [Fact]
    public void Generator_ForThePublicAssemblyWithAProductDomain_ReportsD2MQ004()
    {
        // The public assembly references no host-owned domain catalog, so the
        // generator rejects the same entry there. Without this check the union
        // would admit a host-owned domain into the framework registry.
        var result = Run(
            _PUBLIC_ASSEMBLY,
            [
                Spec("public/mq-messages.spec.json", _SEALED_EXTENSION_MESSAGES),
                Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
            ],
            _FRAMEWORK_DOMAINS_SOURCE).GetRunResult();

        var diagnostic = result.Diagnostics
            .Single(d => d.Id == DiagnosticIds.UnknownEncryption)
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture);

        diagnostic.Should().Contain("SealedFixture").And.Contain("orders");

        // The list of valid values states what this target accepts.
        diagnostic.Should()
            .Contain("payload-fixture-sealed")
            .And.NotContain("shipping");
        Source(result, "MqMessages.g.cs").Should().NotContain("SealedFixture");
    }

    [Fact]
    public void Generator_ForTheExtensionTargetWithoutTheExtraDomainsProperty_ReportsD2MQ004()
    {
        // The same compilation as the admitting case above, without the
        // property that names the extra domain type.
        var result = RunWithProperties(
            _EXTENSION_ASSEMBLY,
            SealedExtensionSpecs(),
            ExtensionProperties(extraDomainsType: null),
            _FRAMEWORK_DOMAINS_SOURCE,
            _PRODUCT_DOMAINS_SOURCE).GetRunResult();

        result.Diagnostics
            .Single(d => d.Id == DiagnosticIds.UnknownEncryption)
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain("SealedFixture");
        Source(result, "SampleMqMessages.g.cs").Should().NotContain("SealedFixture");
    }

    [Fact]
    public void Generator_ForTheExtensionTargetWithAnUnresolvedExtraDomainsType_ReportsD2MQ004()
    {
        var result = RunWithProperties(
            _EXTENSION_ASSEMBLY,
            SealedExtensionSpecs(),
            ExtensionProperties("Example.Ext.Encryption.MissingDomains"),
            _FRAMEWORK_DOMAINS_SOURCE,
            _PRODUCT_DOMAINS_SOURCE).GetRunResult();

        result.Diagnostics.Should().ContainSingle(d => d.Id == DiagnosticIds.UnknownEncryption);
        Source(result, "SampleMqMessages.g.cs").Should().NotContain("SealedFixture");
    }

    [Fact]
    public void Generator_ForTheExtensionTargetWithAPaddedExtraDomainsType_TrimsTheValue()
    {
        var result = RunWithProperties(
            _EXTENSION_ASSEMBLY,
            SealedExtensionSpecs(),
            ExtensionProperties("  " + _EXTRA_DOMAINS_TYPE + "\t"),
            _FRAMEWORK_DOMAINS_SOURCE,
            _PRODUCT_DOMAINS_SOURCE).GetRunResult();

        result.Diagnostics.Should().NotContain(d => d.Id == DiagnosticIds.UnknownEncryption);
        Source(result, "SampleMqMessages.g.cs").Should().Contain("\"orders\"");
    }

    [Fact]
    public void Generator_ForThePublicAssemblyWithEveryProperty_IgnoresTheProperties()
    {
        // The public target ignores the extension properties, the extra domain
        // type among them, so a domain only that type declares stays unknown.
        var result = RunWithProperties(
            _PUBLIC_ASSEMBLY,
            [
                Spec("public/mq-messages.spec.json", _SEALED_EXTENSION_MESSAGES),
                Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
            ],
            ExtensionProperties(_EXTRA_DOMAINS_TYPE),
            _FRAMEWORK_DOMAINS_SOURCE,
            _PRODUCT_DOMAINS_SOURCE).GetRunResult();

        result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Should().Equal("MqMessages.g.cs", "MqSubscriptions.g.cs");
        result.Diagnostics.Should().ContainSingle(d => d.Id == DiagnosticIds.UnknownEncryption);
    }

    private static IReadOnlyList<AdditionalText> SealedExtensionSpecs() =>
    [
        Spec("public/mq-messages.spec.json", _EMPTY_MESSAGES),
        Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
        Spec("private/mq-messages.spec.json", _SEALED_EXTENSION_MESSAGES),
        Spec("private/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
    ];

    private static IReadOnlyList<AdditionalText> Public() =>
    [
        Spec("public/mq-messages.spec.json", _EMPTY_MESSAGES),
        Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
    ];

    private static IReadOnlyList<AdditionalText> PublicAndExtension() =>
    [
        Spec("public/mq-messages.spec.json", _EMPTY_MESSAGES),
        Spec("public/mq-subscriptions.spec.json", _EMPTY_SUBSCRIPTIONS),
        Spec("private/mq-messages.spec.json", _EXTENSION_MESSAGES),
        Spec("private/mq-subscriptions.spec.json", _EXTENSION_SUBSCRIPTIONS),
    ];

    private static AdditionalText Spec(string path, string json) =>
        new InMemoryAdditionalTextStub(path, json);

    private static string Source(GeneratorDriverRunResult result, string fileName) =>
        result.GeneratedTrees
            .Single(t => Path.GetFileName(t.FilePath) == fileName)
            .ToString();

    private static List<string> SourcesOf(GeneratorDriverRunResult result) =>
        result.GeneratedTrees
            .OrderBy(t => t.FilePath, StringComparer.Ordinal)
            .Select(t => t.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Trim())
            .ToList();

    private static GeneratorDriver Run(
        string assemblyName,
        IReadOnlyList<AdditionalText> specs,
        params string[] domainSources)
    {
        var properties = string.Equals(assemblyName, _EXTENSION_ASSEMBLY, StringComparison.Ordinal)
            ? ExtensionProperties(_EXTRA_DOMAINS_TYPE)
            : new Dictionary<string, string>();

        return RunWithProperties(assemblyName, specs, properties, domainSources);
    }

    private static GeneratorDriver RunWithProperties(
        string assemblyName,
        IReadOnlyList<AdditionalText> specs,
        IReadOnlyDictionary<string, string> properties,
        params string[] domainSources)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [.. domainSources.Select(s => CSharpSyntaxTree.ParseText(s))],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new MqGenerator().AsSourceGenerator()],
            additionalTexts: [.. specs],
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(properties));

        return driver.RunGenerators(compilation);
    }

    private static Dictionary<string, string> ExtensionProperties(string? extraDomainsType)
    {
        var properties = new Dictionary<string, string>
        {
            ["D2MqExtensionNamespace"] = "Example.Ext.Messaging",
            ["D2MqExtensionClassPrefix"] = "Sample",
        };

        if (extraDomainsType is not null)
            properties["D2MqExtraEncryptionDomainsType"] = extraDomainsType;

        return properties;
    }

    private sealed class InMemoryAdditionalTextStub : AdditionalText
    {
        private readonly SourceText r_text;

        public InMemoryAdditionalTextStub(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
