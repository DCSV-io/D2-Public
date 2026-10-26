// -----------------------------------------------------------------------
// <copyright file="RegistryCollisionCheckerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RegistryCollisionChecker"/> - the pure-logic
/// cross-catalog safety gate. Tests run without a Roslyn driver, directly
/// exercising the diagnostic-production logic over synthetic
/// <see cref="RegistrySpecEntry"/> inputs.
/// </summary>
public sealed class RegistryCollisionCheckerTests
{
    // Synthetic catalogs are registered the way a host registers its own, so
    // each carries a declared identity rather than one derived from its name.
    private static readonly System.Collections.Generic.Dictionary<
        string, RegistrySpecLoader.CatalogIdentity> sr_fixtureIdentities =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["gen-error-codes.spec.json"] =
                new RegistrySpecLoader.CatalogIdentity("lib", "gen", "GEN_"),
            ["gen-sub-error-codes.spec.json"] =
                new RegistrySpecLoader.CatalogIdentity("lib", "gen-sub", "GEN_SUB_"),
        };

    // -----------------------------------------------------------------------
    // Cross-catalog collision (D2ERC004)
    // -----------------------------------------------------------------------

    [Fact]
    public void Check_NoDuplicates_ReturnsNoCollisionDiagnostics()
    {
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("LIB_AUTH_BEARER_MISSING", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode);
    }

    [Fact]
    public void Check_SameCodeInTwoCatalogs_EmitsD2ERC004()
    {
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("GEN_NOT_FOUND", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
            because: "two catalogs declaring the same code must fire D2ERC004");
    }

    [Fact]
    public void Check_DuplicateWithinSameCatalog_EmitsDuplicateDiagnostic()
    {
        // Two entries with the same code from the same spec file. The
        // cross-catalog checker fires on any duplicated code in the aggregate
        // set - intra-catalog duplication should have been caught by the
        // per-catalog emitter but is still surfaced here if it reaches the
        // registry.
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
            because: "any duplicate code in the aggregate set is a collision");
    }

    [Fact]
    public void Check_EmptyEntries_ReturnsNoDiagnostics()
    {
        var diagnostics = RegistryCollisionChecker.Check([]);
        diagnostics.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Reserved-namespace violation (D2ERC005)
    // -----------------------------------------------------------------------

    [Fact]
    public void Check_UnprefixedCodeInDomainSpec_EmitsD2ERC005()
    {
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),

            // UNPREFIXED_CODE has no AUTH_ prefix - violation in auth catalog.
            MakeEntry("UNPREFIXED_CODE", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "an unprefixed code in a per-domain spec must fire D2ERC005");
    }

    [Fact]
    public void Check_PrefixedCodeInGenericSpec_EmitsD2ERC005()
    {
        // AUTH_BEARER_MISSING in the generic (common) catalog - violation.
        var entries = new[]
        {
            MakeEntry("LIB_AUTH_BEARER_MISSING", "common", "error-codes.spec.json"),
            MakeEntry("LIB_AUTH_REAL_CODE", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "a domain-prefixed code in the generic spec must fire D2ERC005");
    }

    [Fact]
    public void Check_ValidGenericCodeNoAuthDomain_NoReservedNamespaceDiagnostic()
    {
        // Without any per-domain catalog present there are no known domain
        // prefixes, so the generic catalog's codes cannot be "prefixed"
        // by definition. No D2ERC005 should fire.
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("GEN_CONFLICT", "common", "error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation);
    }

    [Fact]
    public void Check_ValidAuthCode_NoReservedNamespaceDiagnostic()
    {
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("LIB_AUTH_BEARER_MISSING", "auth", "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation);
    }

    [Fact]
    public void Check_ANonGenCatalogResolvingToTheGenPrefix_LeavesGenCodesClean()
    {
        // "gen-error-codes.spec.json" is registered under the owner "lib" with the
        // prefix "GEN_", so GEN_ is added to the foreign-prefix set. Every code in
        // the real gen catalog starts with GEN_, and none may be reported as
        // having a foreign prefix for it.
        var entries = new[]
        {
            MakeEntry("GEN_NOT_FOUND", "common", "error-codes.spec.json"),
            MakeEntry("GEN_SYNTHETIC", "gen", "gen-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().NotContain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "a catalog whose own prefix is GEN_ does not make the gen "
                + "catalog's own codes foreign-prefixed");
    }

    [Fact]
    public void Check_AGenCodeCarryingALongerForeignPrefix_EmitsD2ERC005()
    {
        // The guard still reports the case it exists for: a foreign catalog whose
        // prefix extends the gen one, and a gen code that starts with it.
        var entries = new[]
        {
            MakeEntry("GEN_SUB_LEAKED", "common", "error-codes.spec.json"),
            MakeEntry("GEN_SUB_REAL_CODE", "gen-sub", "gen-sub-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation
                && Equals(d.Args[0], "GEN_SUB_LEAKED"),
            because: "the gen catalog must not carry another catalog's full prefix");
    }

    private static RegistrySpecEntry MakeEntry(
        string code, string domain, string specFileName)
    {
        var identity = RegistrySpecLoader.ResolveIdentity(specFileName, sr_fixtureIdentities);
        identity.Should().NotBeNull(because: $"'{specFileName}' must be a registered catalog");
        return new(
            Code: code,
            HttpStatus: 400,
            Category: "validation_failure",
            UserMessageKey: "TK.Common.Errors.VALIDATION_FAILED",
            FactoryName: "TestFactory",
            FactoryShape: "standard",
            Doc: "Test.",
            Domain: string.IsNullOrEmpty(domain) ? identity.DomainToken : domain,
            Owner: identity.Owner,
            DomainPrefix: identity.DomainPrefix,
            SpecFileName: specFileName);
    }
}
