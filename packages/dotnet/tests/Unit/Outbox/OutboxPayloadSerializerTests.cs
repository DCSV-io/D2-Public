// -----------------------------------------------------------------------
// <copyright file="OutboxPayloadSerializerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using Xunit;

/// <summary>
/// The payload format is a persisted contract: a row written by one version of a
/// consumer has to stay readable by the next, and nothing about a public-API
/// report can see a change to the serializer options.
/// </summary>
/// <remarks>
/// The golden below pins the exact bytes the serializer writes. Changing the
/// naming policy, the null-handling or the indentation fails a test whose
/// message states the compatibility requirement, rather than silently making the
/// backlog written by the running version unreadable by the version replacing it.
/// </remarks>
public sealed class OutboxPayloadSerializerTests
{
    private const string _GOLDEN =
        """{"subjectId":"0198f2c1-4a5b-7c2d-8e3f-a1b2c3d4e5f6","detail":"a value"}""";

    private static readonly Guid sr_subject =
        Guid.Parse("0198f2c1-4a5b-7c2d-8e3f-a1b2c3d4e5f6");

    [Fact]
    public void Options_SerializeAFixedPayload_ProducesTheGoldenBytes()
    {
        var text = JsonSerializer.Serialize(
            new FakeOutboxPayload(sr_subject, "a value", Absent: null),
            OutboxPayloadSerializer.Options);

        Encoding.UTF8.GetBytes(text).Should().Equal(
            Encoding.UTF8.GetBytes(_GOLDEN),
            "the staged payload format is a persisted contract - rows written by "
            + "the running version must stay readable by the version replacing it, "
            + "so a change to these options is a compatibility decision rather than "
            + "a formatting one");
    }

    [Fact]
    public void Options_PropertyNamingPolicy_IsCamelCase()
        => OutboxPayloadSerializer.Options.PropertyNamingPolicy.Should()
            .BeSameAs(JsonNamingPolicy.CamelCase);

    [Fact]
    public void Options_DefaultIgnoreCondition_OmitsNullProperties()
        => OutboxPayloadSerializer.Options.DefaultIgnoreCondition.Should()
            .Be(JsonIgnoreCondition.WhenWritingNull);

    [Fact]
    public void Options_WriteIndented_IsFalse()
        => OutboxPayloadSerializer.Options.WriteIndented.Should().BeFalse();

    [Fact]
    public void Deserialize_TheGoldenText_RoundTripsEveryPresentMember()
    {
        var read = OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(_GOLDEN);

        read.Should().Be(new FakeOutboxPayload(sr_subject, "a value", Absent: null));
    }

    [Fact]
    public void Deserialize_TheJsonNullLiteral_AnswersNull()
    {
        // A dispatcher reads this as a failure rather than as an empty message,
        // so the member is nullable and does not throw here.
        OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>("null").Should().BeNull();
    }

    [Fact]
    public void Deserialize_TextThatIsNotJson_Throws()
    {
        var act = () => OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>("not json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_EmptyText_Throws()
    {
        var act = () => OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(string.Empty);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_TruncatedJson_Throws()
    {
        var act = () => OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(
            """{"subjectId":"0198f2c1-4a5b-7c2d-8e3f-a1b2c3d4e5f6",""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AMemberOfTheWrongType_Throws()
    {
        // The stored text is a persisted contract, so a shape that does not match
        // it is a fault the dispatcher must see rather than a silent default.
        var act = () => OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(
            """{"subjectId":42,"detail":"a value"}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AnUnknownMember_IsIgnoredRatherThanRefused()
    {
        // Forward compatibility: a row staged by a newer consumer that added a
        // member must still be deliverable by an instance running the old code.
        const string newer = """
            {"subjectId":"0198f2c1-4a5b-7c2d-8e3f-a1b2c3d4e5f6","detail":"a value","addedLater":true}
            """;

        var read = OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(newer);

        read.Should().Be(new FakeOutboxPayload(sr_subject, "a value", Absent: null));
    }

    [Fact]
    public void Deserialize_TextCarryingNonAsciiContent_RoundTripsIt()
    {
        // A payload is consumer data, and a consumer's data is not ASCII. The
        // escaping the default encoder applies must survive the round trip. The
        // literal is written as codepoint escapes so the source stays ASCII.
        const string detail = "\u00e9\u4e2d\ud83d\ude00";

        var text = JsonSerializer.Serialize(
            new FakeOutboxPayload(sr_subject, detail, Absent: null),
            OutboxPayloadSerializer.Options);

        var read = OutboxPayloadSerializer.Deserialize<FakeOutboxPayload>(text);

        read.Should().NotBeNull();
        read.Detail.Should().Be(detail);
    }
}
