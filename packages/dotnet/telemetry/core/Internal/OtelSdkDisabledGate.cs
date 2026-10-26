// -----------------------------------------------------------------------
// <copyright file="OtelSdkDisabledGate.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Internal;

/// <summary>
/// Internal helper that reads the <c>OTEL_SDK_DISABLED</c> env var and
/// returns whether the OTel SDK setup should short-circuit. The env var
/// is the OpenTelemetry-canonical kill switch - honored by the SDK
/// itself in some scenarios. The predicate is in one place so
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/> and
/// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
/// observe identical semantics (case-insensitive equality with the
/// literal <c>"true"</c>; any other value - including <c>"1"</c>,
/// <c>"yes"</c>, etc. - is treated as "not disabled").
/// </summary>
internal static class OtelSdkDisabledGate
{
    private const string _ENABLED_TRIGGER_VALUE = "true";

    private static readonly AsyncLocal<RawValue?> sr_override = new();

    /// <summary>
    /// Returns <c>true</c> when the
    /// <see cref="D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR"/> env
    /// var is set to <c>"true"</c> (case-insensitive). Returns <c>false</c>
    /// for unset / empty / any other literal value.
    /// </summary>
    /// <returns>
    /// <c>true</c> when callers should short-circuit OTel registration;
    /// otherwise <c>false</c>.
    /// </returns>
    internal static bool IsDisabled()
    {
        var raw = sr_override.Value is { } pinned
            ? pinned.Value
            : Environment.GetEnvironmentVariable(D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);

        return string.Equals(raw, _ENABLED_TRIGGER_VALUE, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Makes <see cref="IsDisabled"/> read <paramref name="raw"/> in place of
    /// the environment variable, for the calling asynchronous flow only, until
    /// the returned scope is disposed. A flow started before the pin, or
    /// outside it, keeps the read it had.
    /// </summary>
    /// <param name="raw">The value to read; <c>null</c> reads as unset.</param>
    /// <returns>The scope that restores the previous read when disposed.</returns>
    internal static IDisposable Pin(string? raw)
    {
        var prior = sr_override.Value;
        sr_override.Value = new RawValue(raw);

        return new PinScope(prior);
    }

    private sealed record RawValue(string? Value);

    private sealed class PinScope(RawValue? prior) : IDisposable
    {
        public void Dispose() => sr_override.Value = prior;
    }
}
