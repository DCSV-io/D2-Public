// -----------------------------------------------------------------------
// <copyright file="ScopeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ScopeSpecLoader.Load"/>. The loader
/// validates only the JSON shape (D2SCP001 - malformed spec).
/// <see cref="ScopesEmitter"/> performs all semantic validation (naming
/// convention, enum values and so on), and its tests cover that separately.
/// </summary>
public sealed class ScopeSpecLoaderTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_HappyPath_ReturnsSpecWithEntries()
    {
        const string json = """
        {
          "scopes": [
            {
              "name": "self.read",
              "description": "Read self",
              "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] }
            }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Scopes.Should().HaveCount(1);
        var scope = result.Spec.Scopes[0];
        scope.Name.Should().Be("self.read");
        scope.Description.Should().Be("Read self");
        scope.ImpersonationBlocked.Should().BeFalse();
        scope.GrantedTo.Should().NotBeNull();
        scope.GrantedTo!.Should().ContainKey("*");
        scope.GrantedTo["*"].Should().BeEquivalentTo(["*"]);
    }

    [Fact]
    public void Load_AnonScopeWithoutGrantedTo_ReturnsSpecWithNullGrantedTo()
    {
        const string json = """
        {
          "scopes": [
            {
              "name": "anon.public.health",
              "impersonationBlocked": false
            }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].GrantedTo.Should().BeNull();
        result.Spec.Scopes[0].Description.Should().BeNull();
    }

    [Fact]
    public void Load_MultipleScopes_PreservesOrder()
    {
        const string json = """
        {
          "scopes": [
            { "name": "z.last",   "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } },
            { "name": "a.first",  "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } },
            { "name": "m.middle", "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Select(s => s.Name)
            .Should().ContainInOrder("z.last", "a.first", "m.middle");
    }

    // ----------------------------------------------------------------------
    // D2SCP001 - malformed JSON / schema-violating
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"a string\"")]
    [InlineData("[]")]
    public void Load_NonObjectRoot_EmitsD2SCP001(string json)
    {
        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingScopesArray_EmitsD2SCP001()
    {
        const string json = """{ "notScopes": [] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("scopes");
    }

    [Fact]
    public void Load_ScopesIsObjectNotArray_EmitsD2SCP001()
    {
        const string json = """{ "scopes": { "x": 1 } }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ScopeEntryIsString_EmitsD2SCP001()
    {
        const string json = """{ "scopes": ["not an object"] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("scopes[0]");
    }

    [Fact]
    public void Load_ScopeMissingName_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [ { "impersonationBlocked": false } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_ScopeNameIsNumber_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [
            { "name": 42, "impersonationBlocked": false } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ScopeMissingImpersonationBlocked_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [ { "name": "self.read" } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("impersonationBlocked");
    }

    [Fact]
    public void Load_ImpersonationBlockedIsString_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [
            { "name": "x.y", "impersonationBlocked": "true" } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_GrantedToIsArray_EmitsD2SCP001()
    {
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "impersonationBlocked": false,
              "grantedTo": [] }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("grantedTo");
    }

    [Fact]
    public void Load_GrantedToValueIsString_EmitsD2SCP001()
    {
        // A grantedTo entry value must be an array per the schema.
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "impersonationBlocked": false,
              "grantedTo": { "Admin": "Owner" } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_GrantedToRoleEntryIsNumber_EmitsD2SCP001()
    {
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "impersonationBlocked": false,
              "grantedTo": { "Admin": [42] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // ----------------------------------------------------------------------
    // Adversarial - extra unknown properties
    //
    // The schema sets additionalProperties to `false` (an editor-time gate), but
    // the loader only validates JSON shape. The loader silently ignores unknown
    // properties on a scope entry at load time; the schema gate catches them in
    // editors, IDEs and a JSON-Schema CLI.
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_UnknownExtraPropertyOnScope_IsIgnoredByLoader()
    {
        const string json = """
        {
          "scopes": [
            { "name": "self.read", "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] }, "extraField": "ignored", "anotherExtra": 42 }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Should().HaveCount(1);
        result.Spec.Scopes[0].Name.Should().Be("self.read");
    }

    [Fact]
    public void Load_DescriptionWrongType_IsTreatedAsAbsent()
    {
        // description is optional; the loader silently treats a non-string
        // value as absent and does not reject it, which keeps the loader
        // lenient on optional fields.
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "description": 42, "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].Description.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // The declared internal flag
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_InternalTrue_SetsTheFlag()
    {
        const string json = """
        {
          "scopes": [
            { "name": "internal.example.sign", "impersonationBlocked": true,
              "internal": true }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].Internal.Should().BeTrue();
    }

    [Fact]
    public void Load_InternalAbsent_LeavesTheFlagFalse()
    {
        // The paired case: an entry that declares nothing is not internal,
        // which shows that the reader in the test above does not return true
        // for every entry.
        const string json = """
        {
          "scopes": [
            { "name": "self.read", "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec!.Scopes[0].Internal.Should().BeFalse();
    }

    [Fact]
    public void Load_InternalFalse_LeavesTheFlagFalse()
    {
        const string json = """
        {
          "scopes": [
            { "name": "self.read", "impersonationBlocked": false,
              "internal": false, "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec!.Scopes[0].Internal.Should().BeFalse();
    }

    [Fact]
    public void Load_InternalWrongJsonType_LeavesTheFlagFalse()
    {
        // A string is not the declaration; only the JSON boolean true is. A
        // typo therefore fails closed into the D2SCP010 prefix disagreement and
        // does not exempt the entry from the grant-matrix requirement.
        const string json = """
        {
          "scopes": [
            { "name": "internal.example.sign", "impersonationBlocked": true,
              "internal": "true" }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].Internal.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Boundary - empty scopes array
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_EmptyScopesArray_ReturnsEmptySpec()
    {
        // The schema's minItems: 1 applies at editor time only; the loader
        // accepts an empty array.
        const string json = """{ "scopes": [] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Should().BeEmpty();
    }
}
