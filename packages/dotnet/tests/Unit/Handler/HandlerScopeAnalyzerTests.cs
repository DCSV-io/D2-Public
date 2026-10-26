// -----------------------------------------------------------------------
// <copyright file="HandlerScopeAnalyzerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

/// <summary>
/// D2HND001-003 analyzer unit suite (real Roslyn APIs).
/// </summary>
public sealed class HandlerScopeAnalyzerTests
{
    /// <summary>
    /// The platform handler contract, declared in the compilation under test so
    /// the analyzer resolves it the way it does in a build that references the
    /// handler package.
    /// </summary>
    private const string _CONTRACT = """
        namespace DcsvIo.D2.Handler.Abstractions;
        public interface IHandler<in TInput, TOutput> where TInput : notnull { }
        """;

    private const string _PRODUCT_ASSEMBLY = "Sample.Product.App";

    [Fact]
    public async Task Positive_PartialWithAttribute_NoDiagnostics()
    {
        const string source = """
            using System;
            using DcsvIo.D2.Handler.Abstractions;
            namespace Prod;
            public sealed record SignInWithPasswordInput;
            public sealed record SignInWithPasswordOutput;
            public interface ISignInWithPasswordHandler
                : IHandler<SignInWithPasswordInput, SignInWithPasswordOutput> { }
            [D2GeneratedHandlerScope(D2HandlerScopeMode.Any)]
            public sealed partial class SignInWithPasswordHandler : ISignInWithPasswordHandler
            {
                protected sealed override HandlerOptions DefaultOptions => new();
            }
            public enum D2HandlerScopeMode { Any }
            public sealed class D2GeneratedHandlerScopeAttribute : Attribute
            {
                public D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) { }
            }
            public sealed class HandlerOptions { }
            """;

        var diags = await RunAsync(source, _PRODUCT_ASSEMBLY);
        diags.Should().BeEmpty();
    }

    [Fact]
    public async Task WrongName_Reports_D2HND001()
    {
        const string source = """
            using System;
            using DcsvIo.D2.Handler.Abstractions;
            namespace Prod;
            public sealed record SignInWithPasswordInput;
            public sealed record SignInWithPasswordOutput;
            public interface ISignInWithPasswordHandler
                : IHandler<SignInWithPasswordInput, SignInWithPasswordOutput> { }
            [D2GeneratedHandlerScope(D2HandlerScopeMode.Any)]
            public sealed partial class OtherHandler : ISignInWithPasswordHandler
            {
                protected sealed override HandlerOptions DefaultOptions => new();
            }
            public enum D2HandlerScopeMode { Any }
            public sealed class D2GeneratedHandlerScopeAttribute : Attribute
            {
                public D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) { }
            }
            public sealed class HandlerOptions { }
            """;

        var diags = await RunAsync(source, _PRODUCT_ASSEMBLY);
        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND001);
    }

    [Fact]
    public async Task NonPartial_Reports_D2HND002()
    {
        const string source = """
            using System;
            using DcsvIo.D2.Handler.Abstractions;
            namespace Prod;
            public sealed record SignInWithPasswordInput;
            public sealed record SignInWithPasswordOutput;
            public interface ISignInWithPasswordHandler
                : IHandler<SignInWithPasswordInput, SignInWithPasswordOutput> { }
            [D2GeneratedHandlerScope(D2HandlerScopeMode.Any)]
            public sealed class SignInWithPasswordHandler : ISignInWithPasswordHandler
            {
                protected sealed override HandlerOptions DefaultOptions => new();
            }
            public enum D2HandlerScopeMode { Any }
            public sealed class D2GeneratedHandlerScopeAttribute : Attribute
            {
                public D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) { }
            }
            public sealed class HandlerOptions { }
            """;

        var diags = await RunAsync(source, _PRODUCT_ASSEMBLY);
        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND002);
    }

    [Fact]
    public async Task MissingScopeBinding_Reports_D2HND003()
    {
        var diags = await RunAsync(UnboundHandler("SignInWithPasswordHandler"), _PRODUCT_ASSEMBLY);

        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND003);
    }

    [Theory]
    [InlineData("FixtureSignInWithPasswordHandler")]
    [InlineData("TestSignInWithPasswordHandler")]
    [InlineData("StubSignInWithPasswordHandler")]
    [InlineData("FakeSignInWithPasswordHandler")]
    [InlineData("SampleSignInWithPasswordHandler")]
    public async Task LeafNameCarriesATestWord_InAProductAssembly_StillReports(string typeName)
    {
        var diags = await RunAsync(UnboundHandler(typeName), _PRODUCT_ASSEMBLY);

        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND003);
    }

    [Theory]
    [InlineData("Sample.Product.Tests")]
    [InlineData("Sample.Product.UnitTests")]
    [InlineData("Sample.Product.IntegrationTests")]
    public async Task TestAssemblySuffix_CarvesOut(string assemblyName)
    {
        var diags = await RunAsync(UnboundHandler("OtherHandler"), assemblyName);

        diags.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Product.Integrations")]
    [InlineData("Sample.Product.Unit.App")]
    [InlineData("Sample.Product.TestKit")]
    public async Task AssemblyNameMerelyContainingATestWord_DoesNotCarveOut(string assemblyName)
    {
        var diags = await RunAsync(UnboundHandler("OtherHandler"), assemblyName);

        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND003);
    }

    [Fact]
    public async Task ScopeMarkerFromAForeignNamespace_Reports_D2HND003()
    {
        var diags = await RunAsync(
            MarkedHandler("Foreign"),
            _PRODUCT_ASSEMBLY);

        diags.Should().Contain(d => d.Id == HandlerScopeAnalyzer.D2HND003);
    }

    [Fact]
    public async Task ScopeMarkerFromThePlatformNamespace_ReportsNo_D2HND003()
    {
        var diags = await RunAsync(
            MarkedHandler("DcsvIo.D2.Handler.Abstractions"),
            _PRODUCT_ASSEMBLY);

        diags.Should().NotContain(d => d.Id == HandlerScopeAnalyzer.D2HND003);
    }

    [Fact]
    public async Task HandlerShapedInterfaceOutsideThePlatformContract_ReportsNothing()
    {
        const string source = """
            namespace Prod;
            public interface IRequestHandler { }
            public sealed class OtherHandler : IRequestHandler
            {
            }
            """;

        var diags = await RunAsync(source, _PRODUCT_ASSEMBLY);
        diags.Should().BeEmpty();
    }

    /// <summary>
    /// A product handler implementing the platform contract with no scope
    /// binding at all, so every rule that fires has something to fire on.
    /// </summary>
    private static string UnboundHandler(string typeName) =>
        $$"""
        using DcsvIo.D2.Handler.Abstractions;
        namespace Prod;
        public sealed record SignInWithPasswordInput;
        public sealed record SignInWithPasswordOutput;
        public interface ISignInWithPasswordHandler
            : IHandler<SignInWithPasswordInput, SignInWithPasswordOutput> { }
        public sealed class {{typeName}} : ISignInWithPasswordHandler
        {
        }
        """;

    /// <summary>
    /// A partial product handler bound only by the scope marker, with the
    /// marker attribute declared in <paramref name="markerNamespace"/>.
    /// </summary>
    private static string MarkedHandler(string markerNamespace) =>
        $$"""
        using DcsvIo.D2.Handler.Abstractions;
        namespace {{markerNamespace}}
        {
            public enum D2HandlerScopeMode { Any }
            public sealed class D2GeneratedHandlerScopeAttribute : System.Attribute
            {
                public D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) { }
            }
        }
        namespace Prod
        {
            public sealed record SignInWithPasswordInput;
            public sealed record SignInWithPasswordOutput;
            public interface ISignInWithPasswordHandler
                : IHandler<SignInWithPasswordInput, SignInWithPasswordOutput> { }
            [{{markerNamespace}}.D2GeneratedHandlerScope(
                {{markerNamespace}}.D2HandlerScopeMode.Any)]
            public sealed partial class SignInWithPasswordHandler : ISignInWithPasswordHandler
            {
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(_CONTRACT)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new HandlerScopeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diags.Where(d => d.Id.StartsWith("D2HND", StringComparison.Ordinal))
            .ToImmutableArray();
    }
}
