// -----------------------------------------------------------------------
// <copyright file="SortDirectionWireTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using Xunit;

public sealed class SortDirectionWireTests
{
    [Fact]
    public void AscendingConstant_PreservesWireValue()
    {
        SortDirectionWire.ASC.Should().Be("asc");
    }

    [Fact]
    public void DescendingConstant_PreservesWireValue()
    {
        SortDirectionWire.DESC.Should().Be("desc");
    }
}
