// -----------------------------------------------------------------------
// <copyright file="EfMessageOutboxTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

/// <summary>
/// The staging seam. The row joins the caller's unit of work and commits
/// nothing, so the change and the message that announces it become durable
/// together or not at all.
/// </summary>
public sealed class EfMessageOutboxTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 9, 30);

    [Fact]
    public async Task StageAsync_ADefinedKind_IsOk()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        staged.IsOk.Should().BeTrue();
    }

    /// <summary>
    /// The reason the seam exists. A row added but not committed is discarded by
    /// a rollback along with the change it was staged beside.
    /// </summary>
    [Fact]
    public async Task StageAsync_JoinsTheUnitOfWorkAndCommitsNothing()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        db.ChangeTracker.Entries<OutboxMessageRow>()
            .Should().ContainSingle().Which.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task StageAsync_ThenTheCallerRollsBack_LeavesNoRow()
    {
        // The counterpart of the previous test: the row is only durable when the
        // caller's own save makes it so.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        (await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(0);
    }

    [Fact]
    public async Task StageAsync_ThenTheCallerSaves_MakesTheRowDurable()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task StageAsync_StoresTheKindByName()
    {
        // The name rather than the number, so a consumer adding a member above an
        // existing one does not re-point every staged row at another dispatcher.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.OtherThingHappened,
            Payload(),
            TestContext.Current.CancellationToken);

        Tracked(db).Kind.Should().Be(nameof(FakeOutboxKind.OtherThingHappened));
    }

    [Fact]
    public async Task StageAsync_StoresTheStagedInstantFromTheClock()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        Tracked(db).StagedAt.Should().Be(sr_now);
    }

    [Fact]
    public async Task StageAsync_StoresTheRowAsStaged()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        Tracked(db).Status.Should().Be(OutboxMessageStatus.Staged);
    }

    [Fact]
    public async Task StageAsync_StoresTheRowWithNoAttemptsMade()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        Tracked(db).Attempts.Should().Be(0);
    }

    [Fact]
    public async Task StageAsync_StoresTheRowAttemptableNow()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        Tracked(db).NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task StageAsync_SerializesThePayloadThroughTheSharedOptions()
    {
        // The caller never writes the text, so no call site can invent a second
        // payload format for the same table.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new FakeOutboxPayload(Guid.Empty, "a value", Absent: null),
            TestContext.Current.CancellationToken);

        Tracked(db).Payload.Should()
            .Be("""{"subjectId":"00000000-0000-0000-0000-000000000000","detail":"a value"}""");
    }

    [Fact]
    public async Task StageAsync_MintsAVersionSevenIdentity()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);

        Tracked(db).Id.Version.Should().Be(7);
    }

    [Fact]
    public async Task StageAsync_TwoMessages_AddsBothToTheSameUnitOfWork()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened, Payload(), TestContext.Current.CancellationToken);
        _ = await outbox.StageAsync(
            FakeOutboxKind.OtherThingHappened,
            Payload(),
            TestContext.Current.CancellationToken);

        db.ChangeTracker.Entries<OutboxMessageRow>().Should().HaveCount(2);
    }

    [Fact]
    public async Task StageAsync_AKindOutsideTheEnum_IsValidationFailed()
    {
        // A cast integer is a caller mistake. Staged, it would be a row no
        // dispatcher claims, retried for the whole window and then abandoned.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            (FakeOutboxKind)99, Payload(), TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_AKindOutsideTheEnum_AddsNoRow()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            (FakeOutboxKind)99, Payload(), TestContext.Current.CancellationToken);

        db.ChangeTracker.Entries<OutboxMessageRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_APayloadTheSerializerRefuses_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new SelfReferencingPayloadStub(),
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_APayloadTheSerializerRefuses_AddsNoRow()
    {
        // The row must not reach the table: the relay would read it back, fail
        // every attempt, and abandon it an hour later.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        _ = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new SelfReferencingPayloadStub(),
            TestContext.Current.CancellationToken);

        db.ChangeTracker.Entries<OutboxMessageRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_APayloadDeclaringAPersonalMember_IsValidationFailedAndAddsNoRow()
    {
        // The row is exempt from anonymization, so a personal value staged here
        // outlives its subject's erasure for the whole retention window.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new MarkedMemberPayloadStub(Guid.CreateVersion7(), "someone@example.test"),
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
        db.ChangeTracker.Entries<OutboxMessageRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_APayloadWhoseListElementsDeclareAPersonalMember_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new NestedMarkedPayloadStub(
                Guid.CreateVersion7(),
                [new MarkedMemberPayloadStub(Guid.CreateVersion7(), "someone@example.test")]),
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_APayloadMarkedOnlyOnItsConstructorParameter_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new MarkedParameterPayloadStub(Guid.CreateVersion7(), "someone@example.test"),
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_AMarkedValueInAnObjectProperty_IsValidationFailedAndAddsNoRow()
    {
        // The serializer writes an object-typed member by the runtime type of
        // its value, so the marker is found on the instance, not the type.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub
            {
                Detail = new MarkedMemberPayloadStub(Guid.CreateVersion7(), "someone@example.test"),
            },
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
        db.ChangeTracker.Entries<OutboxMessageRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_AMarkedValueInAnObjectListElement_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub
            {
                Items = ["plain", new MarkedTypePayloadStub("someone@example.test")],
            },
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_AMarkedValueInAnObjectDictionaryValue_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub
            {
                Named = new() { ["contact"] = new MarkedTypePayloadStub("someone@example.test") },
            },
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_AMarkedValueInANonGenericCollection_IsValidationFailed()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub
            {
                Loose = [new MarkedTypePayloadStub("someone@example.test")],
            },
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_AnObjectValueWithItsOwnConverterAndAnObjectMember_IsValidationFailed()
    {
        // Nothing describes what the converter writes from the object-typed
        // member, so the value is refused whatever the member holds.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub { Detail = new ConvertedValueStub { Inner = "plain" } },
            TestContext.Current.CancellationToken);

        staged.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_UnmarkedValuesInObjectSlots_IsOkAndStagesTheRow()
    {
        // The admitting case over the same slots: a member written by its
        // runtime type is not refused for its declared type, only for a marked
        // value it holds.
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var staged = await outbox.StageAsync(
            FakeOutboxKind.ThingHappened,
            new ObjectSlotPayloadStub
            {
                Detail = Payload(),
                Items = ["plain", 42, new object()],
                Named = new() { ["count"] = 3, ["payload"] = Payload() },
                Loose = ["plain", Payload()],
                Document = new JsonObject { ["note"] = "plain" },
            },
            TestContext.Current.CancellationToken);

        staged.IsOk.Should().BeTrue();
        db.ChangeTracker.Entries<OutboxMessageRow>().Should().ContainSingle();
    }

    [Fact]
    public async Task StageAsync_ANullPayload_Throws()
    {
        await using var db = NewDb();
        var outbox = NewOutbox(db);

        var act = async () => await outbox.StageAsync<FakeOutboxKind, FakeOutboxPayload>(
            FakeOutboxKind.ThingHappened, null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static FakeOutboxPayload Payload()
        => new(Guid.CreateVersion7(), "a value", Absent: null);

    private static OutboxMessageRow Tracked(OutboxProbeDbContextFixture db)
        => db.ChangeTracker.Entries<OutboxMessageRow>().Single().Entity;

    private static EfMessageOutbox<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>
        NewOutbox(OutboxProbeDbContextFixture db)
        => new(db, new TestClock(sr_now));

    private static OutboxProbeDbContextFixture NewDb()
        => new(new DbContextOptionsBuilder<OutboxProbeDbContextFixture>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);
}
