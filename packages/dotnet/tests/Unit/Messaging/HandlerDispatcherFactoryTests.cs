// -----------------------------------------------------------------------
// <copyright file="HandlerDispatcherFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Result;
using Xunit;

public sealed class HandlerDispatcherFactoryTests
{
    [Fact]
    public void GetForQueue_KnownQueue_BuildsTypedDispatcher()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("orders.q"));
        var factory = new HandlerDispatcherFactory(registry, MessagingCatalog.Default);

        var dispatcher = factory.GetForQueue("orders.q");
        dispatcher.Should().NotBeNull();
        dispatcher.Should()
            .BeOfType<TypedHandlerDispatcher<SampleAuditHandler, SampleAuditEvent>>();
    }

    [Fact]
    public void GetForQueue_RepeatedCall_ReturnsCachedInstance()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("orders.q"));
        var factory = new HandlerDispatcherFactory(registry, MessagingCatalog.Default);

        var first = factory.GetForQueue("orders.q");
        var second = factory.GetForQueue("orders.q");
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void GetForQueue_UnknownQueue_Throws()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("orders.q"));
        var factory = new HandlerDispatcherFactory(registry, MessagingCatalog.Default);

        var act = () => factory.GetForQueue("missing.q");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing.q*");
    }

    [Fact]
    public void Ctor_NullRegistry_Throws()
    {
        var act = () => new HandlerDispatcherFactory(null!, MessagingCatalog.Default);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullCatalog_Throws()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("orders.q"));

        var act = () => new HandlerDispatcherFactory(registry, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetForQueue_HandlerDoesNotSatisfyBaseHandlerConstraint_Throws()
    {
        // The registration has HandlerType=string, which does not derive from
        // BaseHandler<,,>. MakeGenericType throws ArgumentException because the
        // where-clauses of TypedHandlerDispatcher are not satisfied. The
        // factory has no try/catch around MakeGenericType, so the
        // ArgumentException reaches the caller. The test verifies that the
        // misconfiguration fails with an exception at the first dispatch and
        // is not hidden.
        var bad = new TestRegistration(
            HandlerType: typeof(string),
            MessageType: typeof(string),
            Descriptor: new MqSubscriptionDescriptor(
                Constant: "Bad",
                MessageTypeName: typeof(string).FullName!,
                QueueName: "bad.q",
                Pattern: QueuePattern.CompetingConsumer,
                RoutingKeyBinding: string.Empty,
                Prefetch: 10,
                Idempotency: false,
                TieredRetry: null),
            ResolvedQueueName: "bad.q");
        var registry = BuildRegistry(bad);
        var factory = new HandlerDispatcherFactory(registry, MessagingCatalog.Default);

        var act = () => factory.GetForQueue("bad.q");
        act.Should().Throw<ArgumentException>(
            "constraint failure must surface, not be swallowed");
    }

    private static SubscriberRegistry BuildRegistry(params ISubscriberRegistration[] regs)
        => new(regs);

    private static ISubscriberRegistration BuildRegistration<TSub, TIn>(string queueName)
        where TSub : BaseHandler<TSub, TIn, Unit>
        where TIn : class
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(TIn).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null);
        return new TestRegistration(
            HandlerType: typeof(TSub),
            MessageType: typeof(TIn),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    private sealed record TestRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;
}
