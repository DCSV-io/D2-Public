// -----------------------------------------------------------------------
// <copyright file="PublicRegistrySourceGenFenceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using System.IO;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// Public IP fence: the public registry-source-gen package must not hard-code
/// the catalog identities, package ids or message-key defaults that a host owns.
/// </summary>
public sealed class PublicRegistrySourceGenFenceTests
{
    [Fact]
    public void PublicEmitConfig_IsPublicHostOnly()
    {
        RegistryEmitConfig.Public.TargetAssemblyName.Should().Be(
            "DcsvIo.D2.ErrorCodes.Registry");
        RegistryEmitConfig.Public.MessageKeyClassName.Should().Be("TK");
        RegistryEmitConfig.Public.MessageKeyUsingNamespace.Should().Be("DcsvIo.D2.I18n");
        RegistryEmitConfig.Public.PrivateMessageKeyRemapFromPrefix.Should().BeNull();
        RegistryEmitConfig.Public.TargetAssemblyName.Should().NotContain("Private");
    }

    [Fact]
    public void PublicIdentityMap_ExcludesPrivateCatalogFilenames()
    {
        RegistrySpecLoader.ResolveIdentity("auth-core-error-codes.spec.json")
            .Should().BeNull();
        RegistrySpecLoader.ResolveIdentity("keycustodian-error-codes.spec.json")
            .Should().BeNull();
    }

    [Fact]
    public void PublicSourceTree_HasNoPrivateCatalogOrPackageIdLiterals()
    {
        // Disk gate: public/packages/dotnet/error-codes/registry-source-gen/*.cs
        // must not embed the catalog file names or package ids that a host owns.
        var dir = Path.Combine(
            TestPaths.PublicPackagesDotnetRoot(),
            "error-codes",
            "registry-source-gen");
        Directory.Exists(dir).Should().BeTrue();

        // Host-owned catalog file names, package id and message-key type - not
        // generic DOM_/owner-prefix wire examples, which are public law.
        var banned = new[]
        {
            "auth-core-error-codes",
            "keycustodian-error-codes",
            "DcsvIo.D2.Private.ErrorCodes",
            "PrivateTK",
            "TK.Keycustodian",
        };

        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (var token in banned)
            {
                text.Should().NotContain(
                    token,
                    because: $"{Path.GetFileName(file)} must not embed private token '{token}'");
            }
        }
    }
}
