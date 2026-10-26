// -----------------------------------------------------------------------
// <copyright file="OtelSdkDisabledGateTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Internal;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using DcsvIo.D2.Telemetry.Internal;
using Xunit;

/// <summary>
/// The value cases pin the gate for their own flow, so no case writes the
/// process environment; one case reads the variable to show the unpinned gate
/// reads it. Pinned into the <c>LogLoggerStaticState</c> collection alongside
/// the integration tests that build a host through the same gate.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class OtelSdkDisabledGateTests
{
    [Fact]
    public void IsDisabled_Unset_ReturnsFalse()
    {
        WithPinnedValue(null, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("tRuE")]
    public void IsDisabled_TrueCaseInsensitive_ReturnsTrue(string raw)
    {
        WithPinnedValue(raw, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeTrue();
        });
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("disabled")]
    [InlineData("enabled")]
    [InlineData(" true")]
    [InlineData("true ")]
    public void IsDisabled_AnyOtherValue_ReturnsFalse(string raw)
    {
        WithPinnedValue(raw, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    [Fact]
    public void IsDisabled_EmptyString_ReturnsFalse()
    {
        WithPinnedValue(string.Empty, () =>
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        });
    }

    [Fact]
    public void IsDisabled_Unpinned_ReadsTheEnvironmentVariable()
    {
        var raw = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);

        OtelSdkDisabledGate.IsDisabled().Should().Be(
            string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pin_WhileOpen_ReadsThePinnedValueInPlaceOfTheEnvironment()
    {
        // The pin overrides the value in the environment, so the case gives the
        // same result on a host with the variable set or unset and writes nothing.
        var fromEnvironment = OtelSdkDisabledGate.IsDisabled();

        using (OtelSdkDisabledGate.Pin(fromEnvironment ? null : "TRUE"))
        {
            OtelSdkDisabledGate.IsDisabled().Should().Be(!fromEnvironment);
        }

        OtelSdkDisabledGate.IsDisabled().Should().Be(fromEnvironment);
    }

    [Fact]
    public void Pin_Disposed_RestoresThePreviousPin()
    {
        using var outer = OtelSdkDisabledGate.Pin("true");

        using (OtelSdkDisabledGate.Pin(null))
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeFalse();
        }

        OtelSdkDisabledGate.IsDisabled().Should().BeTrue();
    }

    [Fact]
    public async Task Pin_InOneFlow_DoesNotReachAFlowStartedBeforeItAsync()
    {
        using var outer = OtelSdkDisabledGate.Pin(null);

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var other = Task.Run(
            async () =>
            {
                await release.Task;

                return OtelSdkDisabledGate.IsDisabled();
            },
            TestContext.Current.CancellationToken);

        using (OtelSdkDisabledGate.Pin("true"))
        {
            OtelSdkDisabledGate.IsDisabled().Should().BeTrue();
            release.SetResult();

            (await other).Should().BeFalse("a pin reaches only the flow that set it");
        }
    }

    private static void WithPinnedValue(string? value, Action body)
    {
        using var pin = OtelSdkDisabledGate.Pin(value);

        body();
    }
}
