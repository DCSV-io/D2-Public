// -----------------------------------------------------------------------
// <copyright file="RegistryOwnerPrefixDriftTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// Deliberate-drift pins for the owner-prefix / dual-registry contract:
/// wrong DomainPrefix, forged DOM in public aggregate, and cross-catalog
/// collision must hard-fail via D2ERC004/D2ERC005 (not silently pass).
/// </summary>
public sealed class RegistryOwnerPrefixDriftTests
{
    [Fact]
    public void Drift_WrongDomainPrefix_EmitsD2ERC005()
    {
        // Deliberate: auth catalog entry claims LIB_AUTH_ prefix identity but
        // has a bare AUTH_* wire code (pre-owner-prefix residual).
        var entries = new[]
        {
            MakeEntry(
                code: "AUTH_JWT_EXPIRED",
                owner: "lib",
                domain: "auth",
                domainPrefix: "LIB_AUTH_",
                specFileName: "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "wrong DomainPrefix / bare AUTH_* under LIB_AUTH_ must fire D2ERC005");
    }

    [Fact]
    public void Drift_ForgedDomCatalogInPublicAggregate_EmitsD2ERC005_WhenPrefixMismatched()
    {
        // Deliberate: public aggregate must not host unowned DOM_AUTH_* without
        // correct DOM identity. Forged DOM_AUTH code under LIB auth identity fails.
        var entries = new[]
        {
            MakeEntry(
                code: "GEN_NOT_FOUND",
                owner: "gen",
                domain: "common",
                domainPrefix: "GEN_",
                specFileName: "error-codes.spec.json"),
            MakeEntry(
                code: "DOM_AUTH_EXAMPLE_INVALID",
                owner: "lib",
                domain: "auth",
                domainPrefix: "LIB_AUTH_",
                specFileName: "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "forged DOM_AUTH_* under LIB_AUTH_ identity must fire D2ERC005");
    }

    [Fact]
    public void Drift_CrossCatalogCollision_EmitsD2ERC004()
    {
        // Deliberate: same wire code in GEN + LIB catalogs.
        var entries = new[]
        {
            MakeEntry(
                code: "GEN_NOT_FOUND",
                owner: "gen",
                domain: "common",
                domainPrefix: "GEN_",
                specFileName: "error-codes.spec.json"),
            MakeEntry(
                code: "GEN_NOT_FOUND",
                owner: "lib",
                domain: "auth",
                domainPrefix: "LIB_AUTH_",
                specFileName: "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
            because: "cross-catalog code collision must fire D2ERC004");
    }

    [Fact]
    public void Drift_OwnerMismatch_WrongPrefixOnGen_EmitsD2ERC005()
    {
        // Deliberate: GEN catalog entry with wrong owner DomainPrefix GEN_
        // but a LIB_AUTH_* code (a domain code placed in the generic catalog).
        var entries = new[]
        {
            MakeEntry(
                code: "LIB_AUTH_BEARER_MISSING",
                owner: "gen",
                domain: "common",
                domainPrefix: "GEN_",
                specFileName: "error-codes.spec.json"),
            MakeEntry(
                code: "LIB_AUTH_JWT_EXPIRED",
                owner: "lib",
                domain: "auth",
                domainPrefix: "LIB_AUTH_",
                specFileName: "auth-error-codes.spec.json"),
        };

        var diagnostics = RegistryCollisionChecker.Check(entries);

        diagnostics.Should().Contain(
            d => d.DescriptorId == RegistryDiagnosticIds.ReservedNamespaceViolation,
            because: "GEN entry lacking GEN_ prefix (smuggled LIB_) must fire D2ERC005");
    }

    private static RegistrySpecEntry MakeEntry(
        string code,
        string owner,
        string domain,
        string domainPrefix,
        string specFileName) =>
        new(
            Code: code,
            HttpStatus: 400,
            Category: "validation_failure",
            UserMessageKey: "TK.Common.Errors.VALIDATION_FAILED",
            FactoryName: "TestFactory",
            FactoryShape: "standard",
            Doc: "Deliberate drift fixture.",
            Domain: domain,
            Owner: owner,
            DomainPrefix: domainPrefix,
            SpecFileName: specFileName);
}
