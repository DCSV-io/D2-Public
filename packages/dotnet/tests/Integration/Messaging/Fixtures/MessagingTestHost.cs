// -----------------------------------------------------------------------
// <copyright file="MessagingTestHost.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging.Fixtures;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Owns a started <see cref="IHost"/> for one test and shuts it down on
/// <c>await using</c>. Neither <c>IHost.Dispose</c> nor
/// <c>IHost.DisposeAsync</c> runs <see cref="IHost.StopAsync"/>, so a host that
/// is only disposed never cancels its consumers, never drains an in-flight
/// delivery callback, and never closes its subscriber channels - the broker
/// connection is torn down under them instead.
/// </summary>
internal sealed class MessagingTestHost : IAsyncDisposable
{
    /// <summary>Bound on the graceful stop so a stuck consumer cannot hold a
    /// test open; the host is disposed either way.</summary>
    private static readonly TimeSpan sr_stopTimeout = TimeSpan.FromSeconds(30);

    private readonly IHost r_host;
    private readonly Func<Task>? r_cleanup;
    private bool _stopped;

    /// <summary>Wraps a started host.</summary>
    /// <param name="host">The started host to own.</param>
    /// <param name="cleanup">
    /// Runs after the host has stopped, to delete the topology this host declared. Every
    /// CompetingConsumer queue these tests declare is durable and was never deleted, so a
    /// run accumulated more than twenty leftover queues still bound to the shared test
    /// exchanges. They take no deliveries, but a publish to a shared exchange always
    /// matches SOMETHING, so a mandatory publish could not detect an unroutable message:
    /// a message for the queue under test is still routable to the leftovers, so the
    /// broker never returns it.
    /// </param>
    public MessagingTestHost(IHost host, Func<Task>? cleanup = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        r_host = host;
        r_cleanup = cleanup;
    }

    /// <summary>Gets the host's root service provider.</summary>
    public IServiceProvider Services => r_host.Services;

    /// <summary>Stops the host. Safe to call more than once; a later
    /// <see cref="DisposeAsync"/> will not stop it again.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task StopAsync(CancellationToken ct)
    {
        if (_stopped) return;

        _stopped = true;
        await r_host.StopAsync(ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
        {
            _stopped = true;
            using var cts = new CancellationTokenSource(sr_stopTimeout);
            try
            {
                await r_host.StopAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // The stop budget expired; fall through to disposal so the
                // connection and the container still come down.
            }
        }

        if (r_cleanup is not null)
        {
            try
            {
                await r_cleanup();
            }
            catch (Exception)
            {
                // Best-effort hygiene - a broker that is already going away must not fail
                // the test that just passed.
            }
        }

        r_host.Dispose();
    }
}
