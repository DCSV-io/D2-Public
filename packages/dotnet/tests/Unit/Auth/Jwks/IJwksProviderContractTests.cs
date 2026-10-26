// -----------------------------------------------------------------------
// <copyright file="IJwksProviderContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Jwks;

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Contract pinning for <see cref="IJwksProvider"/>. The interface is a
/// versioned contract, and a signature change here is a breaking change. A
/// signature change also stops every implementation of the interface from
/// compiling. These tests fail first, in the abstractions test layer.
/// </summary>
public sealed class IJwksProviderContractTests
{
    [Fact]
    public void GetKeysAsync_HasExpectedShape()
    {
        var method = typeof(IJwksProvider).GetMethod(nameof(IJwksProvider.GetKeysAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask<D2Result<JwksKeySetSnapshot>>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].HasDefaultValue.Should().BeTrue(
            "GetKeysAsync's CancellationToken should default to default(CancellationToken)");
    }

    [Fact]
    public void RefreshAsync_HasExpectedShape()
    {
        var method = typeof(IJwksProvider).GetMethod(nameof(IJwksProvider.RefreshAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask<D2Result>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].HasDefaultValue.Should().BeTrue(
            "RefreshAsync's CancellationToken should default to default(CancellationToken)");
    }

    [Fact]
    public void Interface_IsPublic()
    {
        typeof(IJwksProvider).IsPublic.Should().BeTrue();
        typeof(IJwksProvider).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Interface_HasExactlyTwoMethods()
    {
        // Adversarial: fails on an accidental method addition or removal on the
        // contract surface. To expand the contract on purpose, change this
        // count and add a corresponding shape test.
        var methods = typeof(IJwksProvider).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        methods.Should().HaveCount(2);
    }
}
