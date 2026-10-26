// -----------------------------------------------------------------------
// <copyright file="InMemoryActivityExporterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System.Diagnostics;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using OpenTelemetry;
using Xunit;

/// <summary>
/// Pins <see cref="InMemoryActivityExporter.WaitForAsync"/>: it returns at once
/// for an activity already captured, wakes on a later export from another
/// thread, returns the captured set when the timeout passes, and honors
/// cancellation. Each test owns its exporter and activities, so no assertion
/// reads process-global state.
/// </summary>
public sealed class InMemoryActivityExporterTests : IDisposable
{
    private static readonly TimeSpan sr_longTimeout = TimeSpan.FromSeconds(30);

    private readonly InMemoryActivityExporter r_exporter = new();
    private readonly List<Activity> r_owned = [];

    [Fact]
    public void WaitForAsync_MatchAlreadyCaptured_ReturnsCompletedTask()
    {
        var target = NewActivity("target");
        ExportOne(target);

        var wait = r_exporter.WaitForAsync(
            a => a == target,
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        wait.IsCompletedSuccessfully.Should().BeTrue(
            because: "a match already captured is found by the first check, "
            + "before the method awaits anything.");
    }

    [Fact]
    public async Task WaitForAsync_MatchAlreadyCaptured_ReturnsEveryCapturedActivity()
    {
        var other = NewActivity("other");
        var target = NewActivity("target");
        ExportOne(other);
        ExportOne(target);

        var captured = await r_exporter.WaitForAsync(
            a => a == target,
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        captured.Should().Equal(other, target);
    }

    [Fact]
    public async Task WaitForAsync_MatchExportedLaterFromAnotherThread_ReturnsWithMatch()
    {
        var target = NewActivity("target");
        var wait = r_exporter.WaitForAsync(
            a => a == target,
            sr_longTimeout,
            TestContext.Current.CancellationToken);
        wait.IsCompleted.Should().BeFalse();

        await Task.Run(() => ExportOne(target), TestContext.Current.CancellationToken);
        var captured = await wait;

        captured.Should().Contain(target);
    }

    [Fact]
    public async Task WaitForAsync_NonMatchingExport_KeepsWaitingUntilMatch()
    {
        var other = NewActivity("other");
        var target = NewActivity("target");
        var wait = r_exporter.WaitForAsync(
            a => a == target,
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        await Task.Run(() => ExportOne(other), TestContext.Current.CancellationToken);
        var pendingAfterOther = !wait.IsCompleted;
        await Task.Run(() => ExportOne(target), TestContext.Current.CancellationToken);
        await wait;

        pendingAfterOther.Should().BeTrue(
            because: "an export with no match wakes the waiter, which checks "
            + "again and keeps waiting.");
    }

    [Fact]
    public async Task WaitForAsync_ExportBetweenCheckAndWait_WakesTheWaiter()
    {
        // The predicate exports the target while the first check is still
        // running, so the export runs after the check has read the captured
        // set and before the method awaits. A waiter that read the signal
        // after the check would wait until the timeout and never pass the
        // target to the predicate; this one wakes, checks again and finds it.
        var seed = NewActivity("seed");
        var target = NewActivity("target");
        ExportOne(seed);
        var exportedFromCheck = false;
        var sawTarget = false;

        await r_exporter.WaitForAsync(
            a =>
            {
                if (!exportedFromCheck)
                {
                    exportedFromCheck = true;
                    ExportOne(target);
                }

                sawTarget |= a == target;
                return a == target;
            },
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        sawTarget.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForAsync_NoMatchBeforeTimeout_ReturnsCapturedSetWithoutThrowing()
    {
        var other = NewActivity("other");
        ExportOne(other);

        var captured = await r_exporter.WaitForAsync(
            a => a.OperationName == "never-exported",
            TimeSpan.FromMilliseconds(50),
            TestContext.Current.CancellationToken);

        captured.Should().Equal(other);
    }

    [Fact]
    public async Task WaitForAsync_ZeroTimeoutWithNoMatch_ReturnsCapturedSet()
    {
        var other = NewActivity("other");
        ExportOne(other);

        var captured = await r_exporter.WaitForAsync(
            a => a.OperationName == "never-exported",
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        captured.Should().Equal(other);
    }

    [Fact]
    public async Task WaitForAsync_TokenCanceledWhileWaiting_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var wait = r_exporter.WaitForAsync(
            a => a.OperationName == "never-exported",
            sr_longTimeout,
            token);

        await cts.CancelAsync();
        var act = async () => await wait;

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForAsync_TokenAlreadyCanceled_ThrowsEvenWhenMatchCaptured()
    {
        var target = NewActivity("target");
        ExportOne(target);
        var token = new CancellationToken(canceled: true);

        var act = () => r_exporter.WaitForAsync(a => a == target, sr_longTimeout, token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForAsync_NullPredicate_ThrowsArgumentNull()
    {
        var act = () => r_exporter.WaitForAsync(
            null!,
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    [Fact]
    public async Task WaitForAsync_NegativeTimeout_ThrowsArgumentOutOfRange()
    {
        var act = () => r_exporter.WaitForAsync(
            _ => true,
            TimeSpan.FromTicks(-1),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    [Fact]
    public async Task WaitForAsync_PredicateThrows_PropagatesTheException()
    {
        ExportOne(NewActivity("other"));

        var act = () => r_exporter.WaitForAsync(
            _ => throw new InvalidOperationException("predicate failure"),
            sr_longTimeout,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("predicate failure");
    }

    [Fact]
    public async Task WaitForAsync_ConcurrentWaitersAndExporters_EachWaiterGetsItsOwnMatch()
    {
        const int participant_count = 32;
        var targets = Enumerable.Range(0, participant_count)
            .Select(i => NewActivity($"target-{i}"))
            .ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = TestContext.Current.CancellationToken;

        var waiters = targets
            .Select(target => Task.Run(
                async () =>
                {
                    await start.Task;
                    var captured = await r_exporter.WaitForAsync(
                        a => a == target,
                        sr_longTimeout,
                        token);
                    return captured.Contains(target);
                },
                token))
            .ToArray();
        var exporters = targets
            .Select(target => Task.Run(
                async () =>
                {
                    await start.Task;
                    ExportOne(target);
                },
                token))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(exporters);
        var found = await Task.WhenAll(waiters);

        found.Should().OnlyContain(f => f);
    }

    [Fact]
    public void Reset_AfterExports_EmptiesTheSnapshot()
    {
        ExportOne(NewActivity("other"));

        r_exporter.Reset();

        r_exporter.Snapshot().Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        r_exporter.Dispose();

        foreach (var activity in r_owned)
            activity.Dispose();
    }

    private Activity NewActivity(string operationName)
    {
        var activity = new Activity(operationName);
        r_owned.Add(activity);
        return activity;
    }

    private void ExportOne(Activity activity) =>
        r_exporter.Export(new Batch<Activity>([activity], 1));
}
