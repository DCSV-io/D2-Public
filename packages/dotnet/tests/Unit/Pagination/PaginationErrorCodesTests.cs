// -----------------------------------------------------------------------
// <copyright file="PaginationErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.ErrorCodes.Registry;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// The pagination error-code catalog: its constants, its emitted factories and
/// its place in the merged registry.
/// </summary>
public sealed class PaginationErrorCodesTests
{
    /// <summary>
    /// Gets every code the catalog declares, paired with the status and category the
    /// specification gives it. A 400 describes something the caller sent; a 500
    /// describes a deployment that is wrong.
    /// </summary>
    public static TheoryData<string, int, ErrorCategory> Catalog => new()
    {
        {
            PaginationErrorCodes.LIB_PAGINATION_REQUEST_REQUIRED,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_LIMIT_INVALID,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_PAGE_POLICY_INVALID,
            500,
            ErrorCategory.InternalError
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_VERSION_UNSUPPORTED,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_SORT_NOT_ALLOWED,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_ORDER_INVALID,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_SORT_MISMATCH,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_ORDER_MISMATCH,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_ORDER_INVALID,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_NOT_ENCODABLE,
            500,
            ErrorCategory.InternalError
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SORT_CATALOG_EMPTY,
            500,
            ErrorCategory.InternalError
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_REGISTERED,
            500,
            ErrorCategory.InternalError
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_KEY_UNDECODABLE,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_LONG,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_CURSOR_QUERY_MISMATCH,
            400,
            ErrorCategory.ValidationFailure
        },
        {
            PaginationErrorCodes.LIB_PAGINATION_SEARCH_NOT_SUPPORTED,
            400,
            ErrorCategory.ValidationFailure
        },
    };

    [Fact]
    public void EveryConstant_IsItsOwnWireValue()
    {
        foreach (var field in ConstantFields())
            field.GetRawConstantValue().Should().Be(field.Name);
    }

    [Fact]
    public void EveryConstant_CarriesTheOwnerAndDomainPrefix()
    {
        foreach (var field in ConstantFields())
            field.Name.Should().StartWith("LIB_PAGINATION_");
    }

    [Fact]
    public void AllCodes_IsEveryDeclaredConstant()
    {
        PaginationErrorCodes.AllCodes.Should()
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
        PaginationErrorCodes.GetHttpStatus(code).Should().Be(status);
    }

    [Fact]
    public void GetHttpStatus_UnknownCode_FallsBackToServerFault()
    {
        PaginationErrorCodes.GetHttpStatus("LIB_PAGINATION_NOT_A_CODE").Should().Be(500);
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

    [Theory]
    [MemberData(nameof(Catalog))]
    public void EveryCode_CarriesItsOwnMessageKey(
        string code,
        int status,
        ErrorCategory category)
    {
        _ = status;
        _ = category;

        var failure = InvokeFactory(code);
        var expected = "TK.Common.Pagination." + code["LIB_PAGINATION_".Length..];

        failure.Messages.Should().ContainSingle();
        failure.Messages[0].Key.Should().Be(ResolveKey(expected));
    }

    [Fact]
    public void MessageKeys_AreDistinctAcrossTheCatalog()
    {
        var keys = PaginationErrorCodes.AllCodes
            .Select(code => InvokeFactory(code).Messages[0].Key)
            .ToArray();

        keys.Should().OnlyHaveUniqueItems();
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
        info.Domain.Should().Be("pagination");
    }

    [Fact]
    public void TypedFactory_CarriesTheSameCodeAsItsNonGenericTwin()
    {
        var untyped = PaginationFailures.CursorInvalid();
        var typed = PaginationFailures<string>.CursorInvalid();

        typed.ErrorCode.Should().Be(untyped.ErrorCode);
        typed.StatusCode.Should().Be(untyped.StatusCode);
        typed.Category.Should().Be(untyped.Category);
        typed.Data.Should().BeNull();
    }

    [Fact]
    public void Factory_SuppliedMessages_ReplaceTheDefault()
    {
        var bound = new[] { TK.Common.Pagination.LIMIT_INVALID };
        var failure = PaginationFailures.CursorInvalid(bound);

        failure.Messages.Should().ContainSingle();
        failure.Messages[0].Key.Should().Be(TK.Common.Pagination.LIMIT_INVALID.Key);
    }

    [Fact]
    public void ServerFaultFailures_CarryNoInputErrorDetail()
    {
        // No detail travels with a 500, so a host defect does not disclose
        // configuration to a caller.
        foreach (var code in PaginationErrorCodes.AllCodes)
        {
            if (PaginationErrorCodes.GetHttpStatus(code) != 500)
                continue;

            var failure = InvokeFactory(code);
            failure.InputErrors.Should().BeNullOrEmpty();
        }
    }

    private static IReadOnlyList<FieldInfo> ConstantFields() =>
        typeof(PaginationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToArray();

    private static D2Result InvokeFactory(string code)
    {
        var factoryName = ToPascal(code["LIB_PAGINATION_".Length..]);
        var method = typeof(PaginationFailures).GetMethod(
            factoryName,
            BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();
        return (D2Result)method.Invoke(null, [null])!;
    }

    private static string ResolveKey(string symbolPath)
    {
        var segments = symbolPath.Split('.');
        var domain = char.ToLowerInvariant(segments[1][0]) + segments[1][1..];
        var category = char.ToLowerInvariant(segments[2][0]) + segments[2][1..];
        return $"{domain}_{category}_{segments[3]}";
    }

    private static string ToPascal(string screamingSnake)
    {
        var parts = screamingSnake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => p[0] + p[1..].ToLowerInvariant()));
    }
}
