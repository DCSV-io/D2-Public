// -----------------------------------------------------------------------
// <copyright file="SettlementChannelFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using System.Reflection;
using RabbitMQ.Client;

/// <summary>Records settlement attempts without allocating a broker channel.</summary>
public class SettlementChannelFixture : DispatchProxy
{
    public bool Open { get; set; } = true;

    public bool FailSettlement { get; set; }

    public int Acknowledgments { get; private set; }

    public int Rejections { get; private set; }

    public static (IChannel Channel, SettlementChannelFixture Fixture) Create()
    {
        object proxy = Create<IChannel, SettlementChannelFixture>();
        return ((IChannel)proxy, (SettlementChannelFixture)proxy);
    }

    protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case "get_IsOpen":
                return Open;
            case nameof(IChannel.BasicAckAsync):
                Acknowledgments++;
                return Settlement();
            case nameof(IChannel.BasicNackAsync):
                Rejections++;
                return Settlement();
            case nameof(IAsyncDisposable.DisposeAsync):
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private object Settlement() => FailSettlement
        ? new ValueTask(Task.FromException(new InvalidOperationException("Settlement fixture failed.")))
        : ValueTask.CompletedTask;
}
