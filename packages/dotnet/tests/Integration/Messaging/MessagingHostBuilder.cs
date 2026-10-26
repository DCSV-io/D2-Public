// -----------------------------------------------------------------------
// <copyright file="MessagingHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Wires a real <see cref="IHost"/> with <c>AddD2MessagingRabbitMq</c>
/// pointed at the test container. Encapsulates the host setup boilerplate.
/// </summary>
internal static class MessagingHostBuilder
{
    /// <summary>
    /// Builds + starts a host wired to the broker. Returns a
    /// <see cref="MessagingTestHost"/>; disposing it with <c>await using</c>
    /// stops the host before it is disposed. Registers an in-process keyring
    /// for <see cref="IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN"/>,
    /// the domain of the encrypted fixture message.
    /// </summary>
    /// <param name="fixture">The Testcontainers fixture.</param>
    /// <param name="configure">Optional further DI configuration (subscribers, etc.).</param>
    /// <param name="activeKid">Active kid for the fixture-domain keyring.</param>
    /// <param name="extraKeys">
    /// Additional kids the keyring can decrypt (rotation testing). Defaults to
    /// an empty set - only the active kid is registered.
    /// </param>
    /// <param name="deleteQueuesOnDispose">
    /// Whether teardown deletes the queues this host declared. Pass <c>false</c> when a
    /// LATER host in the same test must find those queues still there - deleting them
    /// would leave the later host declaring a fresh entity, which succeeds for a
    /// different reason and silently stops testing what the case claims to test.
    /// </param>
    public static async Task<MessagingTestHost> BuildAndStartAsync(
        RabbitMqFixture fixture,
        Action<IServiceCollection>? configure = null,
        string activeKid = "kid-active",
        IReadOnlyDictionary<string, byte[]>? extraKeys = null,
        bool deleteQueuesOnDispose = true)
    {
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [activeKid] = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES),
        };
        if (extraKeys is not null)
        {
            foreach (var kvp in extraKeys)
                keys[kvp.Key] = kvp.Value;
        }

        // This integration host wires a static in-process test keyring via the raw
        // AddD2EncryptionFor seam, which is deny-by-default under the encryption-source
        // startup check and would crash a non-Development host. Declaring Development is
        // the intended escape hatch (a dev/test host with no managed key lifecycle) - the
        // check downgrades to a warning. ValidateScopes / ValidateOnBuild are pinned to the
        // pre-check (Production) behavior so declaring Development does not also change what
        // this host exercises.
        var hostBuilder = Host.CreateDefaultBuilder()
            .UseEnvironment(Environments.Development)
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            })
            .ConfigureServices((_, services) =>
            {
                services.AddD2EncryptionFor(
                    IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN,
                    _ => new PayloadCryptoKeyring(
                        activeKid: activeKid,
                        keys: keys,
                        aadContext: Encoding.UTF8.GetBytes(
                            "d2/" + IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN)));

                // Handler stack: HandlerContext<T> for handler activation +
                // a per-scope MutableRequestContext registered as both the
                // mutable type and the IRequestContext interface (consumer
                // dispatch builds an empty context per delivery).
                services.AddD2Handler();
                services.AddScoped<MutableRequestContext>();
                services.AddScoped<IRequestContext>(
                    sp => sp.GetRequiredService<MutableRequestContext>());

                // The fixture catalog in place of the framework default, so the
                // fixture message types resolve their descriptors. Registered
                // before the transport, whose own registration is a TryAdd.
                services.AddSingleton(IntegrationMessageFixtures.Catalog);

                services.AddD2MessagingRabbitMq(
                    configureConnection: o =>
                    {
                        o.ConnectionUri = fixture.ConnectionString;
                    });

                configure?.Invoke(services);
            });

        var host = hostBuilder.Build();

        // Names captured from the registry, which DI populates at registration time, so
        // teardown does not have to resolve anything from a stopping host.
        var declared = host.Services.GetRequiredService<SubscriberRegistry>().All
            .Select(r => r.ResolvedQueueName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // The owner exists before the host is started, so every path out of the start and
        // the waits below has something that stops, disposes and deletes. Constructing it
        // afterwards left a started host unowned whenever a readiness wait was canceled or
        // faulted: never stopped, never disposed, its queues never deleted.
        var testHost = new MessagingTestHost(
            host,
            deleteQueuesOnDispose
                ? () => DeleteDeclaredQueuesAsync(fixture.ConnectionString, declared)
                : null);

        try
        {
            await host.StartAsync();

            // Wait for connection ready before returning, so a publish or
            // subscribe right after the return is safe. The fixture's broker is
            // up, so the signal completes; the runner's token ends an aborted run.
            var conn = host.Services.GetRequiredService<ID2Connection>();
            await conn.ReadyTask.WaitAsync(TestContext.Current.CancellationToken);

            // ConsumerHostedService starts subscribers in a background task.
            // Wait for every channel to finish BasicConsume so the first
            // publish cannot race the queue binding.
            var consumerHost = host.Services.GetServices<IHostedService>()
                .OfType<ConsumerHostedService>()
                .Single();
            await consumerHost.ReadyTask.WaitAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await testHost.DisposeAsync();
            throw;
        }

        return testHost;
    }

    /// <summary>
    /// Deletes the queues a finished test declared, with their dead-letter queues, on a
    /// short-lived connection of its own.
    /// </summary>
    /// <remarks>
    /// These queues are durable and nothing deleted them, so they accumulated across a run
    /// and stayed bound to the shared test exchanges. While any leftover queue is bound, a
    /// publish to a shared exchange always matches something, so the broker never reports
    /// an unroutable message and a mandatory publish cannot show whether the queue under
    /// test was reachable.
    /// </remarks>
    /// <param name="connectionString">The broker URI.</param>
    /// <param name="queueNames">The queues this host declared.</param>
    private static async Task DeleteDeclaredQueuesAsync(
        string connectionString, IReadOnlyCollection<string> queueNames)
    {
        if (queueNames.Count == 0) return;

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        foreach (var queue in queueNames)
        {
            await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);
            await channel.QueueDeleteAsync(
                DlqNaming.DlqFor(queue), ifUnused: false, ifEmpty: false);
        }
    }
}
