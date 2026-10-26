// -----------------------------------------------------------------------
// <copyright file="TransientPublishClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using RabbitMQ.Client.Exceptions;
using Xunit;

public sealed class TransientPublishClassifierTests
{
    [Fact]
    public void IsTransient_BrokerUnavailable_True()
    {
        var ex = new BrokerUnavailableException("offline");
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_AlreadyClosed_True()
    {
        // The public constructor of RabbitMQ.Client's AlreadyClosedException
        // takes a ShutdownEventArgs, which is hard to construct in a unit test.
        // The classifier inspects only the runtime type, so an uninitialized
        // instance is enough.
        var ex = (AlreadyClosedException)RuntimeHelpers.GetUninitializedObject(
            typeof(AlreadyClosedException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_OperationInterrupted_True()
    {
        var ex = (OperationInterruptedException)RuntimeHelpers.GetUninitializedObject(
            typeof(OperationInterruptedException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_TimeoutException_True()
    {
        TransientPublishClassifier.IsTransient(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_BrokerUnreachable_True()
    {
        // BrokerUnreachableException is a case in the classifier switch. This
        // test fails if a refactor removes that case.
        var ex = (BrokerUnreachableException)RuntimeHelpers.GetUninitializedObject(
            typeof(BrokerUnreachableException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_InvalidOperation_False()
    {
        // A programmer error, such as a missing [MqPub], is not transient. The
        // classifier returns false so that the error surfaces immediately.
        TransientPublishClassifier.IsTransient(new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ArgumentException_False()
    {
        TransientPublishClassifier.IsTransient(new ArgumentException("bad")).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_NullArg_ReturnsFalse()
    {
        // The switch on null matches the default arm. Then the switch in
        // RetryHelper.IsTransientException matches its default arm and returns
        // false. The publisher never passes a null exception, but if one
        // arrived, the result is "not transient" and the classifier does not
        // throw a NullReferenceException.
        TransientPublishClassifier.IsTransient(null!).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_PublishException_NotReturn_True()
    {
        // A broker NACK on a confirm happens when the queue is full, during a
        // mirror sync or during a restart. A PublishException with
        // IsReturn=false is transient, so a retry with backoff is worthwhile.
        // The IsReturn property is read-only, so the test uses an
        // uninitialized instance, whose default makes IsReturn false.
        var ex = (PublishException)RuntimeHelpers.GetUninitializedObject(
            typeof(PublishException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_PublishException_Return_True()
    {
        // A basic.return means the publish matched no queue. It is transient
        // in the case that loses messages: two hosts declare the same binding
        // at once and both get bind-ok before the broker writes the exchange
        // index. That window closes on its own, so a retry succeeds. A binding
        // that is absent stays absent, and the publish fails once the attempts
        // run out.
        var ex = new PublishReturnException(
            publishSequenceNumber: 1,
            message: "returned",
            exchange: "d2.test.exchange",
            routingKey: "probe",
            replyCode: 312,
            replyText: "NO_ROUTE");

        ex.IsReturn.Should().BeTrue("the fixture must be the returned variant");
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }
}
