// -----------------------------------------------------------------------
// <copyright file="OutboxPayloadPersonalDataCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using Xunit;

/// <summary>
/// Where a personal-value marker can sit in a payload type and still be found,
/// and the unmarked shapes the walk has to pass without a false refusal.
/// </summary>
public sealed class OutboxPayloadPersonalDataCheckTests
{
    [Fact]
    public void DeclaresPersonalData_AMarkedType_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedTypePayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedProperty_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedMemberPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedConstructorParameter_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedParameterPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedArrayElementType_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedArrayPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedGenericArgument_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(NestedMarkedPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedJsonIncludeField_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(JsonIncludeFieldPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedJsonIncludeInternalProperty_IsTrue()
        => OutboxPayloadPersonalDataCheck
            .DeclaresPersonalData(typeof(JsonIncludeInternalPropertyPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedDerivedTypeOfAPolymorphicMember_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(PolymorphicSlotPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AnUnmarkedGraphWithACycle_IsFalse()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(UnmarkedWidePayloadStub))
            .Should().BeFalse();

    [Fact]
    public void DeclaresPersonalData_AMarkedElementOfANonGenericCollectionType_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedElementListPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedKeyOfANonGenericDictionaryType_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(MarkedKeyMapPayloadStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AMarkedPropertyOnATypeWithItsOwnConverter_IsTrue()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(ConvertedMarkedValueStub))
            .Should().BeTrue();

    [Fact]
    public void DeclaresPersonalData_AnObjectMember_IsFalse()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(ObjectSlotPayloadStub))
            .Should().BeFalse("the type alone does not determine what an object-typed member holds");

    [Fact]
    public void CarriesPersonalData_AnObjectMemberPointingBackAtItsPayload_IsFalse()
    {
        // Without the per-walk set of visited instances this recursion does not
        // end; a finite result is the behavior under test.
        var payload = new ObjectSlotPayloadStub();
        payload.Detail = payload;
        payload.Items = [payload];

        OutboxPayloadPersonalDataCheck.CarriesPersonalData(payload).Should().BeFalse();
    }

    [Fact]
    public void CarriesPersonalData_AMarkedValueBehindACycle_IsTrue()
    {
        var payload = new ObjectSlotPayloadStub();
        payload.Detail = payload;
        payload.Items = [new MarkedTypePayloadStub("someone@example.test")];

        OutboxPayloadPersonalDataCheck.CarriesPersonalData(payload).Should().BeTrue();
    }

    [Fact]
    public void DeclaresPersonalData_AnUnmarkedRecord_IsFalse()
        => OutboxPayloadPersonalDataCheck.DeclaresPersonalData(typeof(FakeOutboxPayload))
            .Should().BeFalse();
}
