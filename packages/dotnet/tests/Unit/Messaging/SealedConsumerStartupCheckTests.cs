// -----------------------------------------------------------------------
// <copyright file="SealedConsumerStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// EncryptionDomains.FIXTURE_SEALED is the sealed fixture in the public catalog.

/// <summary>
/// Tests of <see cref="SealedConsumerStartupCheck"/>, the RabbitMQ library boot check that
/// crashes the host when a subscriber consumes a sealed domain and no keyed
/// <see cref="IPayloadOpener"/> is registered for that domain's consumer service, because
/// every delivery would go to the DLQ. The spec declares no sealed message, so the tests give
/// the check a <see cref="MessagingCatalog"/> of fixture message types whose descriptors
/// carry real sealed domain values.
/// </summary>
public sealed class SealedConsumerStartupCheckTests
{
    [Fact]
    public async Task StartAsync_SealedSubscriberWithoutOpener_CrashesBoot()
    {
        var catalog = SealedCatalog<SealedFixtureMessage>(
            SealedFixtureMessage.CONSTANT, EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, catalog, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*sealed encryption domain*payload-fixture-sealed*");
        ex.WithMessage("*backed by a managed keyring on the consumer host*");
    }

    [Fact]
    public async Task StartAsync_ForgottenSealingCall_StillCrashesBoot()
    {
        // AddD2MessagingRabbitMq registers the check, and the sealing call does not. A host
        // that omitted the sealed-encryption registration entirely, so it has no opener at
        // all, still fails at boot, the same as a host whose opener is missing.
        var catalog = SealedCatalog<SealedFixtureMessage>(
            SealedFixtureMessage.CONSTANT, EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, catalog, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_SealedSubscriberWithOpener_Passes()
    {
        var catalog = SealedCatalog<SealedFixtureMessage>(
            SealedFixtureMessage.CONSTANT, EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadOpener>("payload-fixture-sealed", new StubOpener());
        var sp = services.BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, catalog, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_PlaintextSubscriber_Unaffected()
    {
        var catalog = PlaintextCatalog<PlaintextFixtureMessage>(
            PlaintextFixtureMessage.CONSTANT);
        var registry = Registry<PlaintextFixtureMessage>("plaintext-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, catalog, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync("a plaintext subscriber needs no opener");
    }

    [Fact]
    public async Task StartAsync_SymmetricSubscriber_Unaffected()
    {
        // An unknown (symmetric-default) domain is not sealed, so no opener is required.
        var catalog = SealedCatalog<SymmetricFixtureMessage>(
            SymmetricFixtureMessage.CONSTANT, "payload-fixture-symmetric");
        var registry = Registry<SymmetricFixtureMessage>("symmetric-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, catalog, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync("a symmetric-domain subscriber is not a sealed consumer");
    }

    private static MessagingCatalog SealedCatalog<TMessage>(string constant, string domain) =>
        CatalogOf(
            constant,
            new MqMessageDescriptor(
                Constant: constant,
                MessageTypeName: typeof(TMessage).FullName!,
                Exchange: "d2.test.events",
                ExchangeType: "topic",
                Encryption: domain,
                EncryptionReason: null,
                DefaultRoutingKey: "test.event"));

    private static MessagingCatalog PlaintextCatalog<TMessage>(string constant) =>
        CatalogOf(
            constant,
            new MqMessageDescriptor(
                Constant: constant,
                MessageTypeName: typeof(TMessage).FullName!,
                Exchange: "d2.test.events",
                ExchangeType: "fanout",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "test fixture",
                DefaultRoutingKey: string.Empty));

    private static MessagingCatalog CatalogOf(
        string constant, MqMessageDescriptor descriptor) => new(
        new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal)
        {
            [constant] = descriptor,
        },
        new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal));

    private static SubscriberRegistry Registry<TMessage>(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "SealedSub",
            MessageTypeName: typeof(TMessage).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistry([
            new SealedTestRegistration(
                HandlerType: typeof(SealedFixtureHandler),
                MessageType: typeof(TMessage),
                Descriptor: descriptor,
                ResolvedQueueName: queueName),
        ]);
    }

    private sealed record SealedTestRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;

    // Fixture message and handler doubles. None of them is a production message.
    [MqPub(SealedFixtureMessage.CONSTANT)]
    private sealed class SealedFixtureMessage
    {
        public const string CONSTANT = "SealedFixture";
    }

    [MqPub(PlaintextFixtureMessage.CONSTANT)]
    private sealed class PlaintextFixtureMessage
    {
        public const string CONSTANT = "PlaintextFixture";
    }

    [MqPub(SymmetricFixtureMessage.CONSTANT)]
    private sealed class SymmetricFixtureMessage
    {
        public const string CONSTANT = "SymmetricFixture";
    }

    private sealed class SealedFixtureHandler;

    // The check only probes whether a registration exists (IsKeyedService) and never invokes Open.
    private sealed class StubOpener : IPayloadOpener
    {
        public byte[] Open(ReadOnlySpan<byte> framed) => throw new NotImplementedException();
    }
}
