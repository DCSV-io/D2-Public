// -----------------------------------------------------------------------
// <copyright file="MessagingErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.ErrorCodes.Registry;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Messaging.RabbitMq.Errors;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// The messaging-transport error-code catalog: its constants, its emitted
/// factories and its place in the merged registry.
/// </summary>
public sealed class MessagingErrorCodesTests
{
    /// <summary>
    /// Gets every code the catalog declares, paired with the status and category
    /// the specification gives it. Every entry describes a transport that could
    /// not carry the message, so every entry is a 503.
    /// </summary>
    public static TheoryData<string, int, ErrorCategory> Catalog => new()
    {
        {
            MessagingErrorCodes.LIB_MESSAGING_PUBLISH_UNROUTABLE,
            503,
            ErrorCategory.InfrastructureUnavailable
        },
    };

    [Fact]
    public void EveryConstant_IsItsOwnWireValue()
    {
        foreach (var field in ConstantFields())
            field.GetRawConstantValue().Should().Be(field.Name);
    }

    [Fact]
    public void PublishUnroutable_WireValue_IsLibMessagingPublishUnroutable()
    {
        MessagingErrorCodes.LIB_MESSAGING_PUBLISH_UNROUTABLE
            .Should().Be("LIB_MESSAGING_PUBLISH_UNROUTABLE");
    }

    [Fact]
    public void EveryConstant_CarriesTheOwnerAndDomainPrefix()
    {
        foreach (var field in ConstantFields())
            field.Name.Should().StartWith("LIB_MESSAGING_");
    }

    [Fact]
    public void AllCodes_IsEveryDeclaredConstant()
    {
        MessagingErrorCodes.AllCodes.Should()
            .BeEquivalentTo(ConstantFields().Select(f => f.Name));
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void GetHttpStatus_ReturnsTheDeclaredStatus(
        string code,
        int status,
        ErrorCategory category)
    {
        _ = category;
        MessagingErrorCodes.GetHttpStatus(code).Should().Be(status);
    }

    [Fact]
    public void GetHttpStatus_UnknownCode_FallsBackToServerFault()
    {
        MessagingErrorCodes.GetHttpStatus("LIB_MESSAGING_NOT_A_CODE").Should().Be(500);
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void EveryCode_IsProducedByItsFactory(
        string code,
        int status,
        ErrorCategory category)
    {
        var failure = InvokeFactory(code);

        failure.ErrorCode.Should().Be(code);
        ((int)failure.StatusCode).Should().Be(status);
        failure.Category.Should().Be(category);
        failure.Success.Should().BeFalse();
    }

    [Fact]
    public void PublishUnroutable_IsDistinguishableFromAnUnreachableBroker()
    {
        // Both are 503 infrastructure failures. PublishUnroutable has its own
        // error code, so an operator can tell a missing binding from an
        // unreachable broker.
        var unroutable = MessagingTransportFailures.PublishUnroutable();
        var unreachable = D2Result.ServiceUnavailable();

        unroutable.StatusCode.Should().Be(unreachable.StatusCode);
        unroutable.Category.Should().Be(unreachable.Category);
        unroutable.ErrorCode.Should().NotBe(unreachable.ErrorCode);
    }

    [Fact]
    public void PublishUnroutable_CarriesTheGenericTransportMessageKey()
    {
        // The message tells the person only that the service could not take the
        // message. The topology detail is for the operator, and it is in the
        // error code and the logs.
        var failure = MessagingTransportFailures.PublishUnroutable();

        failure.Messages.Should().ContainSingle();
        failure.Messages[0].Key.Should().Be(TK.Common.Errors.SERVICE_UNAVAILABLE.Key);
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void EveryCode_IsInTheMergedRegistry(
        string code,
        int status,
        ErrorCategory category)
    {
        ErrorCodeRegistry.TryResolve(code, out var info).Should().BeTrue();
        info.Code.Should().Be(code);
        info.HttpStatus.Should().Be(status);
        info.Category.Should().Be(category);
        info.Owner.Should().Be("lib");
        info.Domain.Should().Be("messaging");
    }

    [Fact]
    public void TypedFactory_CarriesTheSameCodeAsItsNonGenericTwin()
    {
        var untyped = MessagingTransportFailures.PublishUnroutable();
        var typed = MessagingTransportFailures<string>.PublishUnroutable();

        typed.ErrorCode.Should().Be(untyped.ErrorCode);
        typed.StatusCode.Should().Be(untyped.StatusCode);
        typed.Category.Should().Be(untyped.Category);
        typed.Data.Should().BeNull();
    }

    [Fact]
    public void Factory_SuppliedMessages_ReplaceTheDefault()
    {
        var bound = new[] { TK.Common.Errors.UNKNOWN };
        var failure = MessagingTransportFailures.PublishUnroutable(bound);

        failure.Messages.Should().ContainSingle();
        failure.Messages[0].Key.Should().Be(TK.Common.Errors.UNKNOWN.Key);
    }

    private static IReadOnlyList<FieldInfo> ConstantFields() =>
        typeof(MessagingErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToArray();

    private static D2Result InvokeFactory(string code)
    {
        var factoryName = ToPascal(code["LIB_MESSAGING_".Length..]);
        var method = typeof(MessagingTransportFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(m => m.Name == factoryName && !m.IsGenericMethod);

        method.Should().NotBeNull();
        return (D2Result)method.Invoke(null, [null])!;
    }

    private static string ToPascal(string screamingSnake)
    {
        var parts = screamingSnake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => p[0] + p[1..].ToLowerInvariant()));
    }
}
