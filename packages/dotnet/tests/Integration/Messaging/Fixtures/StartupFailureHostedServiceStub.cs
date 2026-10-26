// -----------------------------------------------------------------------
// <copyright file="StartupFailureHostedServiceStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging.Fixtures;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Fails its own startup and reports its disposal. The provider disposes it only when
/// the host itself is disposed, so the callback shows whether a host whose startup
/// threw was still disposed.
/// </summary>
/// <param name="onDisposed">Invoked once, when the service provider disposes this.</param>
internal sealed class StartupFailureHostedServiceStub(Action onDisposed)
    : IHostedService, IDisposable
{
    /// <summary>The message the thrown exception carries.</summary>
    public const string FAILURE_MESSAGE = "startup failure stub";

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException(FAILURE_MESSAGE);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => onDisposed();
}
