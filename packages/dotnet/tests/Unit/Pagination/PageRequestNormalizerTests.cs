// -----------------------------------------------------------------------
// <copyright file="PageRequestNormalizerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using Xunit;

public sealed class PageRequestNormalizerTests
{
    private static readonly SortSpec sr_default =
        new(Field: "joinedAt", Direction: SortDirection.Desc);

    private static readonly ListPagingPolicy sr_policy =
        new(DefaultSort: sr_default, DefaultLimit: 50, MaxLimit: 100);

    private static readonly SortFieldAllowSet sr_allow =
        SortFieldAllowSet.BothDirections("joinedAt", "email", "username", "displayName");

    /// <summary>
    /// The allow-set a searching descriptor produces: the sortable fields plus
    /// the emitted relevance specification, locked to descending.
    /// </summary>
    private static readonly SortFieldAllowSet sr_searchAllow =
        SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Both),
            new SortFieldSpec("email", AllowedDirections.Both),
            new SortFieldSpec(SortFieldNames.RELEVANCE, AllowedDirections.Descending));

    private static readonly IKeysetCursorCodec sr_codec = new OpaqueKeysetCursorCodec();

    [Fact]
    public void PolicyConstants_PinsLimitsAndCursorVersion()
    {
        ListPagingPolicy.DEFAULT_LIMIT.Should().Be(50);
        ListPagingPolicy.MAX_LIMIT.Should().Be(100);
        sr_policy.DefaultLimit.Should().Be(50);
        sr_policy.MaxLimit.Should().Be(100);
        sr_policy.DefaultSort.Field.Should().Be("joinedAt");
        KeysetCursor.CURRENT_VERSION.Should().Be(1);
    }

    [Fact]
    public void DirectionWire_Asc_Is_SortDirectionWireAsc()
    {
        new SortSpec("joinedAt", SortDirection.Asc).DirectionWire
            .Should().Be(SortDirectionWire.ASC);
    }

    [Fact]
    public void DirectionWire_Desc_Is_SortDirectionWireDesc()
    {
        new SortSpec("joinedAt", SortDirection.Desc).DirectionWire
            .Should().Be(SortDirectionWire.DESC);
    }

    [Fact]
    public void Token_Uses_DirectionWire()
    {
        new SortSpec("joinedAt", SortDirection.Desc).Token
            .Should().Be("joinedAt:desc");
    }

    [Fact]
    public void Normalize_OmittedLimit_UsesDefault()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, null),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Limit.Should().Be(50);
        result.Data.Cursor.Should().BeNull();
        result.Data.AppliedSort.Should().Be(sr_default);
    }

    [Fact]
    public void Normalize_MinLimit_Ok()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 1),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Limit.Should().Be(1);
    }

    [Fact]
    public void Normalize_OverMax_ClampsToMaxLimit()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 101),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Limit.Should().Be(100);
    }

    [Fact]
    public void Normalize_LimitEqualsMax_Ok()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 100),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Limit.Should().Be(100);
    }

    [Fact]
    public void Normalize_ZeroOrNegativeLimit_LimitInvalid()
    {
        PageRequestNormalizer.Normalize(new PageRequest(null, 0), sr_policy, sr_allow, sr_codec)
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_LIMIT_INVALID);
        PageRequestNormalizer.Normalize(new PageRequest(null, -1), sr_policy, sr_allow, sr_codec)
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_LIMIT_INVALID);
    }

    [Fact]
    public void Normalize_AbsentRequest_RequestRequired()
    {
        PageRequestNormalizer.Normalize(null!, sr_policy, sr_allow, sr_codec)
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_REQUEST_REQUIRED);
    }

    [Fact]
    public void Normalize_EmptyCursor_TreatedAsNoCursor()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(string.Empty, 10),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Cursor.Should().BeNull();
    }

    [Fact]
    public void Resolve_DefaultSort_WhenNoClientSort()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Field.Should().Be("joinedAt");
        result.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void Resolve_ClientSortAllowed_InAllowSetAndCatalog()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: SortDirectionWire.ASC),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Field.Should().Be("email");
        result.Data.AppliedSort.Direction.Should().Be(SortDirection.Asc);
        result.Data.AppliedSort.DirectionWire.Should().Be(SortDirectionWire.ASC);
    }

    [Fact]
    public void Order_Omitted_UsesDescOrSoleDirectionLock()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void BothDirs_Default_Unless_DirectionLock()
    {
        // Both directions are allowed; an omitted order uses
        // defaultDirectionWhenBothAllowed (Desc). No separate decorator.
        var descDefault = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            sr_policy,
            sr_allow,
            sr_codec,
            defaultDirectionWhenBothAllowed: SortDirection.Desc);

        descDefault.Success.Should().BeTrue();
        descDefault.Data!.AppliedSort.Field.Should().Be("email");
        descDefault.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);

        var ascDefault = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            sr_policy,
            sr_allow,
            sr_codec,
            defaultDirectionWhenBothAllowed: SortDirection.Asc);

        ascDefault.Success.Should().BeTrue();
        ascDefault.Data!.AppliedSort.Direction.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void Order_Omitted_MatchingDefaultField_UsesDefaultDirection()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "joinedAt"),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void Order_Unparseable_OrderInvalid()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: "sideways"),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_ORDER_INVALID);
    }

    [Fact]
    public void Order_Alone_Ignores_UsesDefaultSort()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: null, Order: SortDirectionWire.ASC),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Should().Be(sr_default);
    }

    [Fact]
    public void DisallowedField_SortFieldNotAllowed()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "notAField"),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED);
    }

    [Fact]
    public void Order_Accepts_SortDirectionWire_Consts_CaseInsensitive()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: "ASC"),
            sr_policy,
            sr_allow,
            sr_codec).Data!.AppliedSort.Direction.Should().Be(SortDirection.Asc);

        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: "DeSc"),
            sr_policy,
            sr_allow,
            sr_codec).Data!.AppliedSort.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void Resolve_CursorWins_ClientMismatch_CursorSortMismatch()
    {
        var enc = sr_codec.Encode(
            new KeysetCursor(
                1,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D")));

        PageRequestNormalizer.Normalize(
            new PageRequest(enc.Data, 10, Sort: "email"),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_SORT_MISMATCH);
    }

    [Fact]
    public void Normalize_CursorVersionNotOne_CursorVersionUnsupported()
    {
        var json =
            "{\"version\":2,\"sort\":\"joinedAt\",\"primaryKey\":\"2020-01-01T00:00:00Z\",\"tieBreakerId\":\""
            + Guid.CreateVersion7().ToString("D")
            + "\",\"order\":\"desc\"}";
        var wire = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        PageRequestNormalizer.Normalize(
            new PageRequest(wire, 10),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_VERSION_UNSUPPORTED);
    }

    [Fact]
    public void Normalize_CursorSortOutsideAllowSet_CursorSortNotAllowed()
    {
        var enc = sr_codec.Encode(
            new KeysetCursor(
                1,
                "createdAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D")));

        PageRequestNormalizer.Normalize(
            new PageRequest(enc.Data, 10),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_SORT_NOT_ALLOWED);
    }

    [Fact]
    public void Codec_RoundTrip_PreservesKeysAndOrder()
    {
        var original = new KeysetCursor(
            1,
            "joinedAt",
            "2024-06-01T12:34:56.789Z",
            Guid.CreateVersion7().ToString("D"),
            SortDirectionWire.ASC);

        var enc = sr_codec.Encode(original);
        enc.Success.Should().BeTrue();

        var dec = sr_codec.Decode(enc.Data);
        dec.Success.Should().BeTrue();
        dec.Data.Should().Be(original);
        dec.Data!.ToSortSpec().Direction.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void Codec_Decode_NullOrEmpty_CursorInvalid()
    {
        sr_codec.Decode(null).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
        sr_codec.Decode(string.Empty).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
        sr_codec.Decode("   ").ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public void Codec_InvalidBase64OrJson_CursorInvalid()
    {
        sr_codec.Decode("%%%not-base64%%%").ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
        sr_codec.Decode("YWJj").ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public void Codec_Encode_VersionNotOne_CursorNotEncodable()
    {
        var result = sr_codec.Encode(
            new KeysetCursor(
                2,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D")));

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_NOT_ENCODABLE);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public void Codec_Encode_EmptySortOrTieBreak_CursorNotEncodable()
    {
        var id = Guid.CreateVersion7().ToString("D");

        sr_codec.Encode(new KeysetCursor(1, string.Empty, "2020-01-01T00:00:00Z", id))
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_NOT_ENCODABLE);
        sr_codec.Encode(new KeysetCursor(1, "joinedAt", "2020-01-01T00:00:00Z", string.Empty))
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_NOT_ENCODABLE);
    }

    [Fact]
    public void Codec_Encode_AbsentPrimaryKey_CursorNotEncodable()
    {
        // A host that built the cursor without a primary key at all. That is
        // the internal fault the code is declared for.
        var id = Guid.CreateVersion7().ToString("D");

        sr_codec.Encode(new KeysetCursor(1, "joinedAt", null!, id))
            .ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_NOT_ENCODABLE);
    }

    [Fact]
    public void Codec_RoundTrip_EmptyPrimaryKey_IsOrdinaryData()
    {
        // A boundary row whose string sort column is the empty string. The
        // server produced that value from its own table, so it must mint an
        // ordinary cursor - returning an internal-fault status here would page
        // an operator over a listing that is working.
        var id = Guid.CreateVersion7().ToString("D");
        var minted = sr_codec.Encode(new KeysetCursor(1, "name", string.Empty, id));

        minted.Success.Should().BeTrue();
        minted.Data.Should().NotBeNullOrWhiteSpace();

        var decoded = sr_codec.Decode(minted.Data);

        decoded.Success.Should().BeTrue();
        decoded.Data!.PrimaryKey.Should().Be(string.Empty);
        decoded.Data.TieBreakerId.Should().Be(id);
    }

    [Fact]
    public void Codec_Decode_CursorWithNoPrimaryKeyField_CursorInvalid()
    {
        // The other side of the empty key being ordinary data: a cursor whose
        // JSON never carried the field at all is still refused, because that is
        // a cursor nothing this library minted.
        var json =
            "{\"version\":1,\"sort\":\"joinedAt\",\"tieBreakerId\":\""
            + Guid.CreateVersion7().ToString("D")
            + "\",\"order\":\"desc\"}";
        var wire = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        sr_codec.Decode(wire).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public void Codec_Encode_NullCursor_Throws()
    {
        var act = () => sr_codec.Encode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Codec_RoundTrip_CarriesTheQueryToken()
    {
        var original = new KeysetCursor(
            1,
            "joinedAt",
            "2024-06-01T12:34:56.789Z",
            Guid.CreateVersion7().ToString("D"),
            SortDirectionWire.ASC,
            "a-query-token");

        var dec = sr_codec.Decode(sr_codec.Encode(original).Data);

        dec.Success.Should().BeTrue();
        dec.Data!.QueryToken.Should().Be("a-query-token");
    }

    [Fact]
    public void Codec_Decode_CursorMintedBeforeTheQueryToken_DecodesWithItAbsent()
    {
        // A cursor minted before the field existed carries no `queryToken`, and
        // neither does a non-search page, so the cursor format version does not
        // move for it.
        var json =
            "{\"version\":1,\"sort\":\"joinedAt\",\"primaryKey\":\"2020-01-01T00:00:00Z\","
            + "\"tieBreakerId\":\"" + Guid.CreateVersion7().ToString("D")
            + "\",\"order\":\"desc\"}";
        var wire = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var dec = sr_codec.Decode(wire);

        dec.Success.Should().BeTrue();
        dec.Data!.QueryToken.Should().BeNull();
    }

    // -----------------------------------------------------------------
    // Failure identity: each failure has its own code that a caller can branch on.
    // -----------------------------------------------------------------
    [Fact]
    public void Normalize_PolicyYieldsNoUsableLimit_PagePolicyInvalid()
    {
        var broken = new ListPagingPolicy(sr_default, DefaultLimit: 0, MaxLimit: 0);
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, null),
            broken,
            sr_allow,
            sr_codec);

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_PAGE_POLICY_INVALID);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public void Normalize_MalformedCursor_CursorInvalid()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest("%%%not-base64%%%", 10),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public void Normalize_CursorOrderUnparseable_CursorOrderInvalid()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(EncodeRawCursor(1, "joinedAt", "sideways"), 10),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_ORDER_INVALID);
    }

    [Fact]
    public void Normalize_RequestOrderDiffersFromCursor_CursorOrderMismatch()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(
                EncodeRawCursor(1, "joinedAt", "desc"),
                10,
                Order: SortDirectionWire.ASC),
            sr_policy,
            sr_allow,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_ORDER_MISMATCH);
    }

    // -----------------------------------------------------------------
    // The direction lock.
    // -----------------------------------------------------------------
    [Fact]
    public void Normalize_LockedField_OmittedOrder_UsesTheSoleDirection()
    {
        var locked = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Both),
            new SortFieldSpec("email", AllowedDirections.Ascending));

        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            sr_policy,
            locked,
            sr_codec,
            defaultDirectionWhenBothAllowed: SortDirection.Desc);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Direction.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void Normalize_LockedField_WrongOrderSupplied_SortDirectionNotAllowed()
    {
        var locked = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Both),
            new SortFieldSpec("email", AllowedDirections.Ascending));

        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: SortDirectionWire.DESC),
            sr_policy,
            locked,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    [Fact]
    public void Normalize_LockedField_MatchingOrderSupplied_Applies()
    {
        var locked = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Both),
            new SortFieldSpec("email", AllowedDirections.Ascending));

        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email", Order: SortDirectionWire.ASC),
            sr_policy,
            locked,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Direction.Should().Be(SortDirection.Asc);
    }

    [Fact]
    public void Normalize_CursorOrderNoLongerPermitted_SortDirectionNotAllowed()
    {
        // A deploy that narrows a field from both directions to one leaves live
        // cursors carrying the direction that is no longer permitted.
        var narrowed = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Ascending));

        PageRequestNormalizer.Normalize(
            new PageRequest(EncodeRawCursor(1, "joinedAt", "desc"), 10),
            sr_policy,
            narrowed,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    // -----------------------------------------------------------------
    // The query and its cursor binding.
    // -----------------------------------------------------------------

    /// <summary>
    /// A surface that declared no search policy has no bounds to check a query
    /// against, so the caller is refused. Against a normalizer that throws, the
    /// caller's request leaves the handler as an unhandled exception and the
    /// wire responds with 500 for what the caller sent.
    /// </summary>
    [Fact]
    public void Normalize_QueryWithNoSearchPolicy_SearchNotSupported()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Query: "ada"),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Failed.Should().BeTrue();
        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_NOT_SUPPORTED);
    }

    /// <summary>
    /// The admitting neighbor over the same call site: the same surface with no
    /// search policy normalizes a request that carries no query, so the refusal
    /// above is about the query rather than about the absent policy.
    /// </summary>
    [Fact]
    public void Normalize_NoQueryAndNoSearchPolicy_Normalizes()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10),
            sr_policy,
            sr_allow,
            sr_codec);

        result.Success.Should().BeTrue();
        result.Data!.Search.Should().BeNull();
    }

    [Fact]
    public void Normalize_QueryBelowMinimumLength_SearchQueryTooShort()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Query: "ad"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT);
    }

    [Fact]
    public void Normalize_QueryAboveMaximumLength_SearchQueryTooLong()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Query: new string('a', 129)),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_LONG);
    }

    [Fact]
    public void Normalize_SearchingRequestWithNoSortField_OrdersByRelevance()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Query: "ada"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Field.Should().Be(SortFieldNames.RELEVANCE);
        result.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void Normalize_QuerylessRequestWithNoSortField_KeepsTheDefaultSort()
    {
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Should().Be(sr_default);
    }

    [Fact]
    public void Normalize_RelevanceWithNoQuery_RelevanceRequiresQuery()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: SortFieldNames.RELEVANCE),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY);
    }

    [Fact]
    public void Normalize_AscendingRelevance_SortDirectionNotAllowed()
    {
        PageRequestNormalizer.Normalize(
            new PageRequest(
                null,
                10,
                Sort: SortFieldNames.RELEVANCE,
                Order: SortDirectionWire.ASC,
                Query: "ada"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    [Fact]
    public void Normalize_CursorFromAnotherQuery_CursorQueryMismatch()
    {
        var minted = SearchQuery.Create("ada", new SearchPolicy()).Data!;
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D"),
                SortDirectionWire.DESC,
                minted.QueryToken));

        PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10, Query: "grace"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_QUERY_MISMATCH);
    }

    [Fact]
    public void Normalize_QueryCursorPresentedWithNoQuery_CursorQueryMismatch()
    {
        var minted = SearchQuery.Create("ada", new SearchPolicy()).Data!;
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D"),
                SortDirectionWire.DESC,
                minted.QueryToken));

        PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_QUERY_MISMATCH);
    }

    [Fact]
    public void Normalize_QuerylessCursorPresentedWithAQuery_CursorQueryMismatch()
    {
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D")));

        PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10, Query: "ada"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy()).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_QUERY_MISMATCH);
    }

    [Fact]
    public void Normalize_RelevanceCursorPresentedWithNoQuery_RelevanceRequiresQuery()
    {
        // The cursor codec is unauthenticated, so this cursor is one a caller
        // can construct: relevance, descending, no query token, sent with no
        // query. Every other gate on the cursor path passes it - the allow-set
        // holds relevance, the descending lock is satisfied, and absent-on-both
        // -sides is a query-token match - and the sort field it names is never
        // registered in the catalog, so what it reached before this guard was a
        // code the catalog declares an internal fault.
        // The cursor carries a descending order, which the cursor type defaults to
        // and the emitted relevance lock permits.
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                SortFieldNames.RELEVANCE,
                "0.5",
                Guid.CreateVersion7().ToString("D")));

        var result = PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy());

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Normalize_RelevanceCursorPresentedWithItsQuery_Resumes()
    {
        // The other side of the guard: relevance on a cursor is legitimate when
        // the request that carries it also carries the query the cursor was
        // minted from, so the guard must not refuse this one.
        var minted = SearchQuery.Create("ada", new SearchPolicy()).Data!;
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                SortFieldNames.RELEVANCE,
                "0.5",
                Guid.CreateVersion7().ToString("D"),
                SortDirectionWire.DESC,
                minted.QueryToken));

        var result = PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10, Query: "ada"),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.AppliedSort.Field.Should().Be(SortFieldNames.RELEVANCE);
    }

    [Fact]
    public void Normalize_CursorFromTheSameQuery_Resumes()
    {
        var minted = SearchQuery.Create("Ada  Lovelace", new SearchPolicy()).Data!;
        var wire = sr_codec.Encode(
            new KeysetCursor(
                1,
                "joinedAt",
                "2020-01-01T00:00:00Z",
                Guid.CreateVersion7().ToString("D"),
                SortDirectionWire.DESC,
                minted.QueryToken));

        // Retyped with different spacing and case: the same search, so the
        // caller keeps their place.
        var result = PageRequestNormalizer.Normalize(
            new PageRequest(wire.Data, 10, Query: " ada lovelace "),
            sr_policy,
            sr_searchAllow,
            sr_codec,
            new SearchPolicy());

        result.Success.Should().BeTrue();
        result.Data!.Search!.QueryToken.Should().Be(minted.QueryToken);
    }

    [Fact]
    public void Normalize_NullPolicyAllowSetOrCodec_Throws()
    {
        var request = new PageRequest(null, 10);

        var nullPolicy = () =>
            PageRequestNormalizer.Normalize(request, null!, sr_allow, sr_codec);
        var nullAllow = () =>
            PageRequestNormalizer.Normalize(request, sr_policy, null!, sr_codec);
        var nullCodec = () =>
            PageRequestNormalizer.Normalize(request, sr_policy, sr_allow, null!);

        nullPolicy.Should().Throw<ArgumentNullException>();
        nullAllow.Should().Throw<ArgumentNullException>();
        nullCodec.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Normalize_PolicyOverload_AllowsOnlyTheDefaultSortField()
    {
        var allowed = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "joinedAt"),
            sr_policy,
            sr_codec);

        var refused = PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            sr_policy,
            sr_codec);

        allowed.Success.Should().BeTrue();
        refused.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED);
    }

    [Fact]
    public void Normalize_PolicyOverload_EmptyDefaultSortField_AllowsNothing()
    {
        var emptyDefault = new ListPagingPolicy(
            new SortSpec(" ", SortDirection.Desc));

        PageRequestNormalizer.Normalize(
            new PageRequest(null, 10, Sort: "email"),
            emptyDefault,
            sr_codec).ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED);
    }

    private static string EncodeRawCursor(int version, string sort, string order)
    {
        var json =
            $"{{\"version\":{version},\"sort\":\"{sort}\","
            + "\"primaryKey\":\"2020-01-01T00:00:00Z\",\"tieBreakerId\":\""
            + Guid.CreateVersion7().ToString("D")
            + $"\",\"order\":\"{order}\"}}";

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
