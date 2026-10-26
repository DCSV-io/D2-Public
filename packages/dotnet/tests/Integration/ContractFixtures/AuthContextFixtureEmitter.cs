// -----------------------------------------------------------------------
// <copyright file="AuthContextFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.AuthContext.Abstractions;
using Xunit;

/// <summary>
/// Emits IAuthContext property-surface fixtures. Each fixture's
/// <c>data.properties</c> field is the sorted list of camelCased property
/// names on the .NET <see cref="IAuthContext"/> interface and the interfaces
/// it inherits; the TypeScript <c>IAuthContext</c> typed shape has the same
/// property names. The fixture envelope's <c>scenario</c> field names the
/// conceptual auth state the fixture represents (unauthenticated,
/// authenticated user, service identity, or an impersonation flavor); it is
/// informational, and every scenario carries the same <c>data.properties</c>.
/// </summary>
public sealed class AuthContextFixtureEmitter
{
    private const string CATALOG = "auth-context";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Unauthenticated()
    {
        const string scenario = "unauthenticated";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_AuthenticatedUser()
    {
        const string scenario = "authenticated-user";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_ServiceIdentity()
    {
        const string scenario = "service-identity";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_ImpersonationConsent()
    {
        const string scenario = "impersonation-consent";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_ImpersonationForce()
    {
        const string scenario = "impersonation-force";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    /// <summary>
    /// Build the canonical fixture payload - the camelCase-name set of
    /// IAuthContext properties. The envelope already carries the
    /// scenario name. The property set is the same for every scenario:
    /// scenarios distinguish conceptual usage, not the type surface.
    /// </summary>
    /// <param name="scenario">Scenario annotation (unused).</param>
    /// <returns>Fixture payload dictionary.</returns>
    private static SortedDictionary<string, object?> BuildScenario(string scenario)
    {
        // The parameter is unused: every scenario emits the same property set.
        // The signature matches the BuildScenario of the other emitters.
        _ = scenario;

        var allNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in typeof(IAuthContext).GetProperties())
            allNames.Add(p.Name);
        foreach (var iface in typeof(IAuthContext).GetInterfaces())
        {
            foreach (var p in iface.GetProperties())
                allNames.Add(p.Name);
        }

        var properties = allNames
            .Select(CamelCase)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["properties"] = properties,
        };
    }

    private static string CamelCase(string pascal)
    {
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }
}
