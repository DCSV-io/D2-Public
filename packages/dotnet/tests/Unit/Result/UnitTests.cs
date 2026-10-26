// -----------------------------------------------------------------------
// <copyright file="UnitTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the canonical-singleton invariant of <see cref="Unit"/>. Unit is the
/// generic argument for <c>D2Result&lt;Unit&gt;</c> where the success state
/// carries no payload, such as a subscriber or a fire-and-forget command. If
/// <see cref="Unit.Value"/> differed from <c>default(Unit)</c>, code that
/// depends on the singleton identity (cache de-duplication, equality checks)
/// would break without an error; these tests fail in that case.
/// </summary>
public sealed class UnitTests
{
    [Fact]
    public void Value_IsDefaultStruct()
    {
        Unit.Value.Should().Be(default(Unit));
    }

    [Fact]
    public void Equality_IsRecordStructByValue()
    {
        var a = Unit.Value;
        var b = Unit.Value;
        var c = default(Unit);

        a.Should().Be(b);
        a.Should().Be(c);
        (a == b).Should().BeTrue();
        (a == c).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_IsStableAcrossInstances()
    {
        Unit.Value.GetHashCode().Should().Be(default(Unit).GetHashCode());
        Unit.Value.GetHashCode().Should().Be(Unit.Value.GetHashCode());
    }

    [Fact]
    public void UseAsResultPayload_OkSurfacesUnitValue()
    {
        // Typical shape: a handler returning D2Result<Unit>.
        var result = D2Result<Unit>.Ok(Unit.Value);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(Unit.Value);
    }
}
