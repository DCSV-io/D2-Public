// -----------------------------------------------------------------------
// <copyright file="LoggerMessageDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Telemetry;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using Xunit;

/// <summary>
/// PII-safety contract for the messaging-stack <c>[LoggerMessage]</c> log
/// delegates. Several real exceptions in this stack carry sensitive content
/// in their <c>Message</c> property - most notably
/// <c>BrokerUnreachableException</c>, which embeds the AMQP URI password.
/// Sinks (Serilog / OpenTelemetry / etc.) format the exception via
/// <c>ex.ToString()</c> and persist the resulting string verbatim. The
/// delegates that observe these exceptions must therefore take a
/// <c>string exType</c> + <c>string? where</c> instead of <c>Exception</c>.
/// </summary>
public sealed class LoggerMessageDelegateContractTests
{
    public static TheoryData<Type, string> LeakProneLogDelegates => new()
    {
        { typeof(MessagingLog), nameof(MessagingLog.PublishTransientFailure) },
        { typeof(MessagingLog), nameof(MessagingLog.PublishTerminalFailure) },
        { typeof(MessagingLog), nameof(MessagingLog.PublishReturned) },
        { typeof(MessagingLog), nameof(MessagingLog.PublishUnroutableTerminal) },
        { typeof(RabbitMqConnectionLog), nameof(RabbitMqConnectionLog.ReconnectAttemptFailed) },
        { typeof(RabbitMqConnectionLog), nameof(RabbitMqConnectionLog.ConnectionCloseFailed) },
        { typeof(ChannelPoolLog), nameof(ChannelPoolLog.ChannelCloseFailed) },
        { typeof(SubscriberLog), nameof(SubscriberLog.HandlerThrew) },
        { typeof(SubscriberLog), nameof(SubscriberLog.BoundaryFailure) },
        { typeof(SubscriberLog), nameof(SubscriberLog.DlqRepublishFailed) },
        { typeof(SubscriberLog), nameof(SubscriberLog.DlqSettlementFailed) },
        { typeof(SubscriberLog), nameof(SubscriberLog.AckFailed) },
        { typeof(TopologyLog), nameof(TopologyLog.DeclarationFailed) },
    };

    [Theory]
    [MemberData(nameof(LeakProneLogDelegates))]
    public void LeakProneLogDelegate_DoesNotTakeRawException(Type logType, string method)
    {
        var info = logType.GetMethod(
            method, BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull("log delegate must exist on the partial class");

        var hasExceptionParam = info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType));

        hasExceptionParam.Should().BeFalse(
            $"{logType.Name}.{method} must take string exType, not Exception - "
            + "passing Exception causes log sinks to format ex.ToString() and "
            + "leak ex.Message contents (e.g. AMQP URI password from "
            + "BrokerUnreachableException) into structured logs.");
    }

    [Fact]
    public void HostStartupFaulted_AcceptsExceptionForFaultLogger()
    {
        // HostStartupFaulted is the ContinueWith-only sink for an unobserved
        // background-task fault, and the operator needs the full stack to
        // debug it. This fault path never sees exceptions derived from user
        // input, so the PII guard does not apply and the delegate takes an
        // Exception. The test pins that choice: it fails if a later
        // consistency refactor makes the delegate stop taking the Exception.
        var info = typeof(SubscriberLog).GetMethod(
            nameof(SubscriberLog.HostStartupFaulted),
            BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull();
        info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType))
            .Should().BeTrue();
    }

    [Fact]
    public void DeclarationFailedFaultSink_AcceptsExceptionForFaultLogger()
    {
        // DeclarationFailedFaultSink is the ContinueWith fault sink for the
        // background topology-declaration task in TopologyHostedService.
        // The reason is the one for HostStartupFaulted: the operator needs
        // the full stack of an unobserved background fault. The inline catch
        // path in DefaultTopologyDeclarer uses the sanitized
        // DeclarationFailed delegate (pinned in LeakProneLogDelegates above)
        // instead, because OperationInterruptedException.Message from
        // RabbitMQ.Client can include broker-side text.
        var info = typeof(TopologyLog).GetMethod(
            nameof(TopologyLog.DeclarationFailedFaultSink),
            BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull();
        info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType))
            .Should().BeTrue();
    }
}
