// -----------------------------------------------------------------------
// <copyright file="PageRequestNormalizer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Applies a <see cref="ListPagingPolicy"/> and an allow-set to a wire
/// <see cref="PageRequest"/>. Multi-mode sort resolve: the cursor takes
/// precedence, then the client sort when it is allowed, else the policy's
/// default sort.
/// </summary>
/// <remarks>
/// This is the only place that decides whether a query is accepted, so every
/// query-shaped failure in the catalog is returned from here.
/// </remarks>
public static class PageRequestNormalizer
{
    /// <summary>
    /// Normalizes limit, sort, order, cursor and query against the policy, the
    /// allow-set and the search policy.
    /// </summary>
    /// <param name="request">Wire page request.</param>
    /// <param name="policy">Per-list policy (default sort + limits).</param>
    /// <param name="allowedSortFields">
    /// Allow-set carrying each field's permitted directions, already
    /// intersected with the sort catalog.
    /// </param>
    /// <param name="codec">Cursor codec.</param>
    /// <param name="searchPolicy">
    /// The surface's query-length bounds, or <see langword="null"/> on a
    /// surface that does not search. A request with a query against a null
    /// search policy fails with
    /// <c>LIB_PAGINATION_SEARCH_NOT_SUPPORTED</c>.
    /// </param>
    /// <param name="defaultDirectionWhenBothAllowed">
    /// Direction used when the client supplies a sort field that is not the
    /// policy's default sort field, omits the order, and the field permits
    /// both directions.
    /// </param>
    /// <returns>The normalized request, or a pagination failure.</returns>
    public static D2Result<NormalizedPageRequest?> Normalize(
        PageRequest? request,
        ListPagingPolicy policy,
        SortFieldAllowSet allowedSortFields,
        IKeysetCursorCodec codec,
        SearchPolicy? searchPolicy = null,
        SortDirection defaultDirectionWhenBothAllowed = SortDirection.Desc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(allowedSortFields);
        ArgumentNullException.ThrowIfNull(codec);

        if (request is null)
            return PaginationFailures<NormalizedPageRequest?>.RequestRequired();

        if (request.Limit is { } limit && limit <= 0)
            return PaginationFailures<NormalizedPageRequest?>.LimitInvalid();

        var effectiveLimit = request.Limit ?? policy.DefaultLimit;

        if (effectiveLimit > policy.MaxLimit)
            effectiveLimit = policy.MaxLimit;

        if (effectiveLimit <= 0)
            return PaginationFailures<NormalizedPageRequest?>.PagePolicyInvalid();

        var searchResult = ResolveQuery(request.Query, searchPolicy);

        if (searchResult.Failed)
            return D2Result<NormalizedPageRequest?>.BubbleFail(searchResult);

        var search = searchResult.Data;

        if (request.Cursor.Truthy())
        {
            return NormalizeCursorPage(
                request,
                allowedSortFields,
                codec,
                effectiveLimit,
                search);
        }

        var sortResult = ResolveSort(
            request.Sort,
            request.Order,
            policy.DefaultSort,
            allowedSortFields,
            defaultDirectionWhenBothAllowed,
            search);

        if (sortResult.Failed || sortResult.Data is null)
            return D2Result<NormalizedPageRequest?>.BubbleFail(sortResult);

        return D2Result<NormalizedPageRequest?>.Ok(
            new NormalizedPageRequest(effectiveLimit, null, sortResult.Data, search));
    }

    /// <summary>
    /// Backward-compatible overload whose allow-set is the policy's default
    /// sort field, permitting both directions. Prefer the allow-set overload
    /// for multi-mode lists.
    /// </summary>
    /// <param name="request">Wire page request.</param>
    /// <param name="policy">Per-list policy.</param>
    /// <param name="codec">Cursor codec.</param>
    /// <returns>The normalized request, or a pagination failure.</returns>
    public static D2Result<NormalizedPageRequest?> Normalize(
        PageRequest? request,
        ListPagingPolicy policy,
        IKeysetCursorCodec codec)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var allow = policy.DefaultSort.Field.Truthy()
            ? SortFieldAllowSet.BothDirections(policy.DefaultSort.Field)
            : SortFieldAllowSet.BothDirections();

        return Normalize(request, policy, allow, codec);
    }

    private static D2Result<SearchQuery?> ResolveQuery(
        string? rawQuery,
        SearchPolicy? searchPolicy)
    {
        if (rawQuery.Falsey())
            return D2Result<SearchQuery?>.Ok();

        // A surface with no search policy has no bounds to validate a query
        // against, so the request fails instead of being validated by nothing
        // or having its query dropped. A request whose query is silently
        // ignored returns a page of the whole list as though it had searched.
        if (searchPolicy is null)
            return PaginationFailures<SearchQuery?>.SearchNotSupported();

        return SearchQuery.Create(rawQuery, searchPolicy);
    }

    private static D2Result<NormalizedPageRequest?> NormalizeCursorPage(
        PageRequest request,
        SortFieldAllowSet allowedSortFields,
        IKeysetCursorCodec codec,
        int effectiveLimit,
        SearchQuery? search)
    {
        var decoded = codec.Decode(request.Cursor);

        if (decoded.Failed)
            return D2Result<NormalizedPageRequest?>.BubbleFail(decoded);

        if (decoded.Data is not { } cursor)
            return PaginationFailures<NormalizedPageRequest?>.CursorInvalid();

        if (cursor.Version != KeysetCursor.CURRENT_VERSION)
            return PaginationFailures<NormalizedPageRequest?>.CursorVersionUnsupported();

        if (!allowedSortFields.TryGet(cursor.Sort, out var cursorSpec))
            return PaginationFailures<NormalizedPageRequest?>.CursorSortNotAllowed();

        if (!SortDirectionWire.TryParse(cursor.Order, out var cursorDirection))
            return PaginationFailures<NormalizedPageRequest?>.CursorOrderInvalid();

        // A lock narrowed after this cursor was minted means the direction in
        // the cursor is no longer permitted, and paging has to restart at the
        // first page.
        if (cursorSpec.SoleDirection is { } sole && sole != cursorDirection)
            return PaginationFailures<NormalizedPageRequest?>.SortDirectionNotAllowed();

        var cursorSort = cursor.ToSortSpec();

        if (RelevanceWithoutQuery(cursorSort.Field, search))
            return PaginationFailures<NormalizedPageRequest?>.RelevanceRequiresQuery();

        if (request.Sort.Truthy()
            && !string.Equals(request.Sort, cursorSort.Field, StringComparison.Ordinal))
        {
            return PaginationFailures<NormalizedPageRequest?>.CursorSortMismatch();
        }

        if (request.Order.Truthy()
            && !string.Equals(
                request.Order,
                cursorSort.DirectionWire,
                StringComparison.OrdinalIgnoreCase))
        {
            return PaginationFailures<NormalizedPageRequest?>.CursorOrderMismatch();
        }

        // Absent on both sides is a match; present on one side only is not.
        if (!string.Equals(cursor.QueryToken, search?.QueryToken, StringComparison.Ordinal))
            return PaginationFailures<NormalizedPageRequest?>.CursorQueryMismatch();

        return D2Result<NormalizedPageRequest?>.Ok(
            new NormalizedPageRequest(effectiveLimit, cursor, cursorSort, search));
    }

    private static D2Result<SortSpec?> ResolveSort(
        string? clientSort,
        string? clientOrder,
        SortSpec defaultSort,
        SortFieldAllowSet allowedSortFields,
        SortDirection defaultDirectionWhenBothAllowed,
        SearchQuery? search)
    {
        // Order alone without a sort field is ignored; the default sort applies.
        if (clientSort.Falsey())
        {
            // A searching request that specifies no sort field is ordered by
            // relevance, the only ordering that ranks results by how well they
            // match the query. A request without a query keeps the policy's
            // default.
            if (search is not null
                && allowedSortFields.TryGet(SortFieldNames.RELEVANCE, out var rel))
            {
                return ResolveAgainst(
                    rel,
                    null,
                    defaultSort,
                    defaultDirectionWhenBothAllowed);
            }

            return D2Result<SortSpec?>.Ok(defaultSort);
        }

        if (!allowedSortFields.TryGet(clientSort, out var spec))
            return PaginationFailures<SortSpec?>.SortFieldNotAllowed();

        if (RelevanceWithoutQuery(spec.Field, search))
            return PaginationFailures<SortSpec?>.RelevanceRequiresQuery();

        return ResolveAgainst(spec, clientOrder, defaultSort, defaultDirectionWhenBothAllowed);
    }

    /// <summary>
    /// Whether a resolved sort field orders by relevance with no query to
    /// resolve it against.
    /// </summary>
    /// <remarks>
    /// Both paths check, because a cursor specifies a sort field the same way a
    /// request does and the cursor codec is unauthenticated. Relevance is never
    /// registered in the sort catalog, so a request that got past this check
    /// with no query would reach a catalog lookup that finds no entry and
    /// returns a code the catalog declares an internal fault - a status that
    /// no request or cursor input may produce.
    /// </remarks>
    private static bool RelevanceWithoutQuery(string field, SearchQuery? search)
        => search is null
            && string.Equals(field, SortFieldNames.RELEVANCE, StringComparison.Ordinal);

    private static D2Result<SortSpec?> ResolveAgainst(
        SortFieldSpec spec,
        string? clientOrder,
        SortSpec defaultSort,
        SortDirection defaultDirectionWhenBothAllowed)
    {
        if (clientOrder.Truthy())
        {
            if (!SortDirectionWire.TryParse(clientOrder, out var requested))
                return PaginationFailures<SortSpec?>.OrderInvalid();

            if (spec.SoleDirection is { } locked && locked != requested)
                return PaginationFailures<SortSpec?>.SortDirectionNotAllowed();

            return D2Result<SortSpec?>.Ok(new SortSpec(spec.Field, requested));
        }

        // Omitted order: the lock takes precedence where there is one,
        // otherwise the policy's own direction for its default sort field and
        // the caller-supplied both-allowed default for every other field.
        if (spec.SoleDirection is { } sole)
            return D2Result<SortSpec?>.Ok(new SortSpec(spec.Field, sole));

        var direction =
            string.Equals(spec.Field, defaultSort.Field, StringComparison.Ordinal)
                ? defaultSort.Direction
                : defaultDirectionWhenBothAllowed;

        return D2Result<SortSpec?>.Ok(new SortSpec(spec.Field, direction));
    }
}
