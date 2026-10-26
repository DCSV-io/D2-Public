// -----------------------------------------------------------------------
// <copyright file="StaticOptionsMonitorFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using Microsoft.Extensions.Options;

/// <summary>
/// An <see cref="IOptionsMonitor{TOptions}"/> holding one value, returned for
/// every name. The relay reads its settings by name, and a test that drives it
/// directly supplies the one set it configured.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
/// <param name="value">The value every read returns.</param>
internal sealed class StaticOptionsMonitorFixture<TOptions>(TOptions value)
    : IOptionsMonitor<TOptions>
{
    /// <inheritdoc/>
    public TOptions CurrentValue => value;

    /// <inheritdoc/>
    public TOptions Get(string? name) => value;

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
