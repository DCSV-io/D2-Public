// -----------------------------------------------------------------------
// <copyright file="RabbitMqFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Configurations;
using JetBrains.Annotations;
using Testcontainers.RabbitMq;
using Xunit;

/// <summary>
/// Shared Testcontainers RabbitMQ fixture. One container per xunit
/// collection; every test in the collection shares it for speed. Each test
/// uses unique queue/exchange names (or cleans up its own topology) to stay
/// isolated.
/// </summary>
/// <remarks>
/// The image leaves <c>/var/lib/rabbitmq</c> mode 1777 and points <c>HOME</c> at it for
/// every user, while the broker itself runs as uid 999. The first Erlang node to start
/// creates <c>.erlang.cookie</c> mode 0400 under its own uid, so any root-side
/// <c>docker exec</c> of a RabbitMQ CLI that lands during boot leaves a cookie the broker
/// cannot read, and the node dies with
/// <c>Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces</c>. Seeding the cookie
/// before the container starts removes the creation race: nobody has to create the file.
/// </remarks>
[MustDisposeResource(false)]
public sealed class RabbitMqFixture : IAsyncLifetime
{
    // TEST-INFRA: up to 3 startup attempts, 5 s backoff - guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    private const string _COOKIE_PATH = "/var/lib/rabbitmq/.erlang.cookie";

    // The account the image's entrypoint drops to, read from the image itself:
    // `docker run --rm --entrypoint sh rabbitmq:4.1.7-management -c 'id rabbitmq'`
    // reports uid=999 gid=999. The Alpine variants this fixture used before
    // numbered the same account 100:101, so the tag and the uid and gid
    // constants change together - a cookie owned by an account the image does
    // not have is unreadable to the broker, and the node dies on the eacces
    // above.
    private const uint _RABBITMQ_UID = 999;
    private const uint _RABBITMQ_GID = 999;
    private const int _COOKIE_LENGTH = 20;

    // Erlang rejects a cookie file any account other than its owner can reach.
    private const UnixFileModes _COOKIE_MODE = UnixFileModes.UserRead;

    // The cookie protects no reachable port: the container publishes the AMQP port
    // only, never the Erlang distribution port.
    private static readonly string sr_nodeCookie =
        RandomNumberGenerator.GetHexString(_COOKIE_LENGTH);

    private RabbitMqContainer _container = BuildContainer();

    /// <summary>Gets the AMQP URI for the running container (amqp://...).</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Gets the broker hostname for direct ConnectionFactory wiring.</summary>
    public string Hostname => _container.Hostname;

    /// <summary>Gets the dynamically-mapped AMQP port.</summary>
    public ushort AmqpPort => _container.GetMappedPublicPort(5672);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        for (var attempt = 1; attempt <= _STARTUP_ATTEMPTS; attempt++)
        {
            try
            {
                await _container.StartAsync();
                return;
            }
            catch (Exception) when (attempt < _STARTUP_ATTEMPTS)
            {
                await _container.DisposeAsync();
                await Task.Delay(_STARTUP_BACKOFF_MS);
                _container = BuildContainer();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static RabbitMqContainer BuildContainer() =>
        new RabbitMqBuilder("rabbitmq:4.1.7-management")
            .WithResourceMapping(
                Encoding.ASCII.GetBytes(sr_nodeCookie),
                _COOKIE_PATH,
                _RABBITMQ_UID,
                _RABBITMQ_GID,
                _COOKIE_MODE)
            .Build();
}
