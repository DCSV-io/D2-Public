// -----------------------------------------------------------------------
// <copyright file="OwnedSocketSetTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using System.Net.Sockets;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using global::RabbitMQ.Client;
using Xunit;

/// <summary>Checks socket registration on both sides of the retirement transition.</summary>
public sealed class OwnedSocketSetTests
{
    [Fact]
    public void Terminate_AfterRegistration_ClosesEveryOwnedSocket()
    {
        var factory = new ConnectionFactory();
        using var owned = new OwnedSocketSet(factory.SocketFactory);
        var first = owned.Create(AddressFamily.InterNetwork);
        var second = owned.Create(AddressFamily.InterNetwork);
        var firstHandle = first.Client.SafeHandle;
        var secondHandle = second.Client.SafeHandle;
        owned.SocketCount.Should().Be(2);
        owned.Terminate();
        owned.Terminate();
        firstHandle.IsClosed.Should().BeTrue();
        secondHandle.IsClosed.Should().BeTrue();
        owned.SocketCount.Should().Be(0);
    }

    [Fact]
    public void Create_AfterTermination_DoesNotInvokeFactory()
    {
        var calls = 0;
        using var owned = new OwnedSocketSet(_ =>
        {
            calls++;
            throw new InvalidOperationException("Factory must not run.");
        });
        owned.Terminate();
        var createSocket = owned.Create;
        Action create = () => createSocket(AddressFamily.InterNetwork);
        create.Should().Throw<ObjectDisposedException>();
        calls.Should().Be(0);
    }

    [Fact]
    public void Create_TerminationDuringFactory_ClosesLateSocketBeforeExposure()
    {
        var factory = new ConnectionFactory();
        var socket = factory.SocketFactory(AddressFamily.InterNetwork);
        var handle = socket.Client.SafeHandle;
        var retirement = new TaskCompletionSource<Action>();
        using var owned = new OwnedSocketSet(_ =>
        {
            retirement.Task.GetAwaiter().GetResult()();
            return socket;
        });
        retirement.SetResult(owned.Terminate);
        var createSocket = owned.Create;
        Action create = () => createSocket(AddressFamily.InterNetwork);
        create.Should().Throw<ObjectDisposedException>();
        handle.IsClosed.Should().BeTrue();
        owned.SocketCount.Should().Be(0);
    }
}
