// -----------------------------------------------------------------------
// <copyright file="MeasurementFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

/// <summary>
/// One measurement the collector saw, with the handler name its tags carried so
/// a test can pick out the measurements its own handler type produced.
/// </summary>
/// <param name="InstrumentName">The instrument the measurement was recorded on.</param>
/// <param name="Value">The recorded value.</param>
/// <param name="HandlerName">The <c>d2.handler.name</c> tag, or null when absent.</param>
internal sealed record MeasurementFixture(
    string InstrumentName,
    object Value,
    string? HandlerName = null);
