// -----------------------------------------------------------------------
// <copyright file="AmqpReplyFilterFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging.Fixtures;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Xunit;

/// <summary>Forwards AMQP traffic while withholding a selected server reply.</summary>
internal sealed class AmqpReplyFilterFixture : IAsyncLifetime
{
    private readonly Uri r_broker;
    private readonly TcpListener r_listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource r_stop = new();
    private readonly SemaphoreSlim r_replyGate = new(1, 1);

    // r_replyGate guards the withheld frames and destination writes.
    private readonly Queue<(Stream Destination, byte[] Header, byte[] Payload)> r_withheld = new();

    private readonly TaskCompletionSource r_suppressed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task? _pump;
    private int _suppressedMethod;
    private int _disposed;

    internal AmqpReplyFilterFixture(string broker) => r_broker = new Uri(broker);

    public string ConnectionString => new UriBuilder(r_broker)
    {
        Host = "127.0.0.1",
        Port = ((IPEndPoint)r_listener.LocalEndpoint).Port,
    }.Uri.ToString();

    public Task Suppressed => r_suppressed.Task;

    public ValueTask InitializeAsync()
    {
        r_listener.Start();
        _pump = AcceptConnectionsAsync(r_stop.Token);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await r_stop.CancelAsync();
        r_listener.Stop();

        if (_pump is not null)
        {
            try
            {
                await _pump;
            }
            catch (OperationCanceledException) when (r_stop.IsCancellationRequested)
            {
            }
            catch (IOException) when (r_stop.IsCancellationRequested)
            {
            }
            catch (SocketException) when (r_stop.IsCancellationRequested)
            {
            }
        }

        r_stop.Dispose();
        r_replyGate.Dispose();
    }

    internal async Task ReleaseAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await r_replyGate.WaitAsync(deadline.Token);

        try
        {
            Volatile.Write(ref _suppressedMethod, 0);
            while (r_withheld.TryDequeue(out var frame))
            {
                try
                {
                    await frame.Destination.WriteAsync(frame.Header, deadline.Token);
                    await frame.Destination.WriteAsync(frame.Payload, deadline.Token);
                }
                catch (IOException)
                {
                    // A terminated dedicated transport no longer accepts held replies.
                }
                catch (ObjectDisposedException)
                {
                    // A completed forwarding task has already disposed this stream.
                }
            }
        }
        finally
        {
            r_replyGate.Release();
        }
    }

    internal void SuppressConfirm() => Volatile.Write(ref _suppressedMethod, (60 << 16) | 80);

    internal void SuppressChannelOpen() => Volatile.Write(ref _suppressedMethod, (20 << 16) | 11);

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        var connections = new List<Task>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await r_listener.AcceptTcpClientAsync(ct);
                if (connections.Count >= 16)
                {
                    client.Dispose();
                    throw new InvalidOperationException("AMQP fixture connection budget exceeded.");
                }

                connections.Add(ForwardAsync(client, ct));
            }
        }
        finally
        {
            await r_stop.CancelAsync();
            await Task.WhenAll(connections);
        }
    }

    private async Task ForwardAsync(TcpClient accepted, CancellationToken ct)
    {
        using var client = accepted;
        using var upstream = new TcpClient();

        // Nagle's algorithm is off on both legs. With it on, each frame's
        // payload waits behind the header write until the peer's delayed
        // acknowledgment arrives, which on Linux adds about 40 ms to every reply.
        client.NoDelay = true;
        upstream.NoDelay = true;
        await upstream.ConnectAsync(r_broker.Host, r_broker.Port, ct);
        await using var incoming = client.GetStream();
        await using var outgoing = upstream.GetStream();
        using var pumps = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var upload = incoming.CopyToAsync(outgoing, pumps.Token);
        var download = FilterRepliesAsync(outgoing, incoming, pumps.Token);
        await Task.WhenAny(upload, download);
        await pumps.CancelAsync();

        try
        {
            await Task.WhenAll(upload, download);
        }
        catch (OperationCanceledException) when (pumps.IsCancellationRequested)
        {
        }
        catch (IOException) when (pumps.IsCancellationRequested)
        {
            // Socket termination ends either direction before the other pump observes cancellation.
        }
    }

    private async Task FilterRepliesAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var header = new byte[7];

        while (!ct.IsCancellationRequested)
        {
            var first = await source.ReadAsync(header.AsMemory(0, 1), ct);
            if (first == 0) return;

            await source.ReadExactlyAsync(header.AsMemory(1), ct);
            var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(3));
            if (size > 1_048_576) throw new InvalidDataException("AMQP test frame exceeds limit.");

            var payload = new byte[checked((int)size + 1)];
            await source.ReadExactlyAsync(payload, ct);
            var method = header[0] == 1 && size >= 4
                ? BinaryPrimitives.ReadInt32BigEndian(payload)
                : 0;

            await r_replyGate.WaitAsync(ct);

            try
            {
                if (method != 0 && method == Volatile.Read(ref _suppressedMethod))
                {
                    r_withheld.Enqueue((destination, header.ToArray(), payload));
                    r_suppressed.TrySetResult();
                    continue;
                }

                await destination.WriteAsync(header, ct);
                await destination.WriteAsync(payload, ct);
            }
            finally
            {
                r_replyGate.Release();
            }
        }
    }
}
