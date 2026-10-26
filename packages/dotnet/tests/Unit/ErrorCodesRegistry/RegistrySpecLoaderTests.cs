// -----------------------------------------------------------------------
// <copyright file="RegistrySpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using System.Collections.Immutable;
using AwesomeAssertions;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using RegistrySourceGen::DcsvIo.D2.SourceGen;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RegistrySpecLoader"/> - domain derivation and
/// spec-file identification logic, plus adversarial loading scenarios.
/// </summary>
public sealed class RegistrySpecLoaderTests
{
    private const string _GEO_CATALOG_JSON = """
    {
      "errorCodes": [
        {
          "code": "LIB_GEO_UNKNOWN_REGION",
          "httpStatus": 400,
          "category": "validation_failure",
          "userMessageKey": "TK.Common.Errors.VALIDATION_FAILED",
          "factoryName": "UnknownRegion",
          "factoryShape": "standard",
          "doc": "Unknown region."
        }
      ]
    }
    """;

    // -----------------------------------------------------------------------
    // IsErrorCodeSpec
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("error-codes.spec.json", true)]
    [InlineData("auth-error-codes.spec.json", true)]
    [InlineData("geo-error-codes.spec.json", true)]
    [InlineData("ERROR-CODES.SPEC.JSON", true)] // case-insensitive
    [InlineData("AUTH-ERROR-CODES.SPEC.JSON", true)] // case-insensitive
    [InlineData("something-else.json", false)]
    [InlineData("error-codes.json", false)]
    [InlineData("error-codes.spec.json.bak", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsErrorCodeSpec_VariousFilenames_ReturnsExpected(
        string? fileName, bool expected)
    {
        var result = RegistrySpecLoader.IsErrorCodeSpec(fileName);
        result.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // DeriveDomain
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("error-codes.spec.json", "common")]
    [InlineData("ERROR-CODES.SPEC.JSON", "common")] // case-insensitive
    [InlineData("auth-error-codes.spec.json", "auth")]
    [InlineData("geo-error-codes.spec.json", null)] // unregistered
    [InlineData("inventory-error-codes.spec.json", null)] // not in this map
    public void DeriveDomain_VariousFilenames_ReturnsExpected(
        string fileName, string? expectedDomain)
    {
        var result = RegistrySpecLoader.DeriveDomain(fileName);
        result.Should().Be(expectedDomain);
    }

    [Fact]
    public void ResolveIdentity_CatalogsThePublicMapDoesNotRegister_ReturnNull()
    {
        // The public loader map contains only the public catalogs, so a host-owned
        // catalog resolves to no identity without extra identities.
        RegistrySpecLoader.ResolveIdentity("orders-error-codes.spec.json")
            .Should().BeNull();
        RegistrySpecLoader.ResolveIdentity("inventory-error-codes.spec.json")
            .Should().BeNull();
    }

    [Fact]
    public void ResolveIdentity_ExtraIdentities_ResolvesCatalogsThePublicMapLacks()
    {
        var extras = new System.Collections.Generic.Dictionary<
            string, RegistrySpecLoader.CatalogIdentity>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            ["orders-error-codes.spec.json"] =
                new RegistrySpecLoader.CatalogIdentity("dom", "orders", "DOM_ORDERS_"),
            ["inventory-error-codes.spec.json"] =
                new RegistrySpecLoader.CatalogIdentity(
                    "dom", "inventory", "DOM_INVENTORY_"),
        };

        var orders = RegistrySpecLoader.ResolveIdentity(
            "orders-error-codes.spec.json", extras);
        orders.Should().NotBeNull();
        orders.Owner.Should().Be("dom");
        orders.DomainToken.Should().Be("orders");
        orders.DomainPrefix.Should().Be("DOM_ORDERS_");

        var inventory = RegistrySpecLoader.ResolveIdentity(
            "inventory-error-codes.spec.json", extras);
        inventory.Should().NotBeNull();
        inventory.Owner.Should().Be("dom");
        inventory.DomainToken.Should().Be("inventory");
        inventory.DomainPrefix.Should().Be("DOM_INVENTORY_");
    }

    // -----------------------------------------------------------------------
    // LoadAll - integration with real spec content
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadAll_ValidSpec_ReturnsEntriesWithDomain()
    {
        const string spec_json = """
        {
          "errorCodes": [
            {
              "code": "LIB_AUTH_BEARER_MISSING",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "BearerMissing",
              "factoryShape": "standard",
              "doc": "Missing."
            }
          ]
        }
        """;

        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "auth-error-codes.spec.json",
            Content: spec_json));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().BeEmpty();
        entries.Should().HaveCount(1);
        entries[0].Code.Should().Be("LIB_AUTH_BEARER_MISSING");
        entries[0].Domain.Should().Be("auth");
        entries[0].SpecFileName.Should().Be("auth-error-codes.spec.json");
    }

    [Fact]
    public void LoadAll_GenericSpec_AssignsDomainCommon()
    {
        const string spec_json = """
        {
          "errorCodes": [
            {
              "code": "GEN_NOT_FOUND",
              "httpStatus": 404,
              "category": "not_found",
              "userMessageKey": "TK.Common.Errors.NOT_FOUND",
              "factoryName": "NotFound",
              "factoryShape": "standard",
              "doc": "Not found."
            }
          ]
        }
        """;

        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "error-codes.spec.json",
            Content: spec_json));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().BeEmpty();
        entries.Should().HaveCount(1);
        entries[0].Domain.Should().Be("common");
    }

    [Fact]
    public void LoadAll_MalformedJson_EmitsDiagnostic_ReturnsNoEntries()
    {
        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "auth-error-codes.spec.json",
            Content: "{not valid json"));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().NotBeEmpty(
            because: "malformed JSON must produce a load diagnostic");
        diagnostics[0].DescriptorId.Should().Be(
            RegistryDiagnosticIds.MalformedRegistrySpec,
            because: "a malformed spec must be reported with D2ERC006, not any other diagnostic id");
        entries.Should().BeEmpty();
    }

    [Fact]
    public void LoadAll_EntryMissingRequiredRegistryFields_EmitsMalformedSpecDiagnostic()
    {
        // An entry that has the always-present fields but is missing the
        // required registry factory fields - category / userMessageKey /
        // factoryName / factoryShape.
        const string spec_json = """
        {
          "errorCodes": [
            {
              "code": "LIB_AUTH_BEARER_MISSING",
              "httpStatus": 401,
              "doc": "Missing bearer token."
            }
          ]
        }
        """;

        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "auth-error-codes.spec.json",
            Content: spec_json));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().NotBeEmpty(
            because: "an entry missing required registry fields must produce a diagnostic");
        diagnostics[0].DescriptorId.Should().Be(
            RegistryDiagnosticIds.MalformedRegistrySpec,
            because: "a missing-fields entry must be reported with D2ERC006, not any other diagnostic id");
        entries.Should().BeEmpty(
            because: "the malformed entry must be skipped");
    }

    [Fact]
    public void LoadAll_NonErrorCodeSpecFile_IsIgnored()
    {
        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "something-else.spec.json",
            Content: "{\"errorCodes\":[]}"));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().BeEmpty();
        entries.Should().BeEmpty(
            because: "non-error-code spec files must be silently ignored");
    }

    [Fact]
    public void LoadAll_CatalogInNeitherIdentityMap_ReportsD2ERC008AndLoadsNothing()
    {
        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "geo-error-codes.spec.json",
            Content: _GEO_CATALOG_JSON));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics);

        diagnostics.Should().ContainSingle();
        diagnostics[0].DescriptorId.Should().Be("D2ERC008");
        diagnostics[0].Args.Should().Equal("geo-error-codes.spec.json");
        entries.Should().BeEmpty(
            because: "a catalog nobody registered has no declared identity to load it under");
    }

    [Fact]
    public void LoadAll_CatalogInExtraIdentities_LoadsUnderTheDeclaredIdentity()
    {
        var extras = new System.Collections.Generic.Dictionary<
            string, RegistrySpecLoader.CatalogIdentity>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            ["geo-error-codes.spec.json"] =
                new RegistrySpecLoader.CatalogIdentity("lib", "geo", "LIB_GEO_"),
        };
        var specFiles = ImmutableArray.Create(new SpecFile(
            Path: "geo-error-codes.spec.json",
            Content: _GEO_CATALOG_JSON));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(specFiles, diagnostics, extras);

        diagnostics.Should().BeEmpty();
        entries.Should().ContainSingle();
        entries[0].Owner.Should().Be("lib");
        entries[0].Domain.Should().Be("geo");
        entries[0].DomainPrefix.Should().Be("LIB_GEO_");
    }

    [Fact]
    public void LoadAll_EmptySpecFiles_ReturnsNoEntries()
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var entries = RegistrySpecLoader.LoadAll(
            ImmutableArray<SpecFile>.Empty, diagnostics);

        diagnostics.Should().BeEmpty();
        entries.Should().BeEmpty();
    }
}
