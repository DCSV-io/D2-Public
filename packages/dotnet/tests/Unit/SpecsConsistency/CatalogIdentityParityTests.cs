// -----------------------------------------------------------------------
// <copyright file="CatalogIdentityParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Registry;
using Xunit;

/// <summary>
/// A catalog's owner, domain token and wire prefix are declared once on the
/// catalog shell and restated in each registry loader's filename-keyed map -
/// one in the .NET registry generator, one in the TypeScript registry emitter.
/// This test asserts that the copies agree.
/// </summary>
/// <remarks>
/// This asserts the invariant the copies exist to satisfy rather than comparing
/// the maps: every registered code opens with the wire prefix its own identity
/// implies - the owner upper-cased, then the domain upper-cased for every owner
/// but <c>gen</c>, which carries no domain segment. A map that carries a
/// different owner, domain or prefix for a catalog than the shell does produces
/// a registry entry whose code and identity disagree, and that fails here.
/// </remarks>
public sealed class CatalogIdentityParityTests
{
    [Fact]
    public void EveryRegisteredCode_OpensWithItsOwnerSegment()
    {
        foreach (var info in ErrorCodeRegistry.All)
        {
            info.Code.Split('_')[0].Should().Be(
                info.Owner.ToUpper(CultureInfo.InvariantCulture),
                "'{0}' is owned by '{1}'",
                info.Code,
                info.Owner);
        }
    }

    [Fact]
    public void EveryRegisteredCode_OpensWithTheWirePrefixItsIdentityImplies()
    {
        // The owner check above leaves the domain segment unread, which is the
        // half a loader map carrying the wrong domain for a catalog would move.
        // A gen-owned catalog carries no domain segment; every other owner does.
        foreach (var info in ErrorCodeRegistry.All)
        {
            var owner = info.Owner.ToUpper(CultureInfo.InvariantCulture);
            var domain = info.Domain.ToUpper(CultureInfo.InvariantCulture);

            var expected = string.Equals(info.Owner, "gen", System.StringComparison.Ordinal)
                ? owner + "_"
                : owner + "_" + domain + "_";

            info.Code.Should().StartWith(
                expected,
                "'{0}' is owned by '{1}' in domain '{2}'",
                info.Code,
                info.Owner,
                info.Domain);
        }
    }

    [Fact]
    public void ThePaginationCatalog_IsRegisteredUnderTheIdentityItsShellDeclares()
    {
        // The catalog is named here instead of derived from the registry,
        // because it is the catalog the copies were most recently extended for.
        var paginationCodes = ErrorCodeRegistry.All
            .Where(i => i.Code.StartsWith("LIB_PAGINATION_", System.StringComparison.Ordinal))
            .ToList();

        paginationCodes.Should().NotBeEmpty();
        paginationCodes.Should().OnlyContain(i => i.Owner == "lib");
        paginationCodes.Should().OnlyContain(i => i.Domain == "pagination");
    }
}
