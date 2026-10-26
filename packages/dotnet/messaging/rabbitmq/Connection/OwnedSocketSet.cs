// -----------------------------------------------------------------------
// <copyright file="OwnedSocketSet.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using System.Collections.Concurrent;
using System.Net.Sockets;
using global::RabbitMQ.Client;

/// <summary>Owns raw sockets before connection acquisition can suspend.</summary>
internal sealed class OwnedSocketSet(Func<AddressFamily, ITcpClient> factory) : IDisposable
{
    private readonly ConcurrentDictionary<ITcpClient, byte> r_sockets = new();
    private int _closed;

    /// <summary>Gets a value indicating whether termination has closed admission.</summary>
    internal bool IsClosed => Volatile.Read(ref _closed) != 0;

    /// <summary>Gets the number of sockets still owned by this generation.</summary>
    internal int SocketCount => r_sockets.Count;

    /// <inheritdoc />
    public void Dispose() => Terminate();

    /// <summary>Creates and registers a socket before exposing it to the client.</summary>
    /// <param name="family">The requested address family.</param>
    /// <returns>The registered socket.</returns>
    internal ITcpClient Create(AddressFamily family)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        var socket = factory(family);
        try
        {
            socket.Client.LingerState = new LingerOption(false, 0);
            r_sockets.TryAdd(socket, 0);
            ObjectDisposedException.ThrowIf(IsClosed, this);
            return socket;
        }
        catch
        {
            r_sockets.TryRemove(socket, out _);
            socket.Close();
            throw;
        }
    }

    /// <summary>Closes admission and terminates every registered socket.</summary>
    internal void Terminate()
    {
        Interlocked.Exchange(ref _closed, 1);
        foreach (var socket in r_sockets.Keys)
        {
            if (r_sockets.TryRemove(socket, out _)) socket.Close();
        }
    }
}
