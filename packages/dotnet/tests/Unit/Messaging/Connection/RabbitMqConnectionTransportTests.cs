// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionTransportTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Connection;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// The broker address that the connection options accept at composition: an
/// address whose scheme is not <c>amqps</c> is rejected unless its host is
/// loopback or the options set
/// <see cref="RabbitMqConnectionOptions.AllowUnencryptedTransport"/>. The main
/// connection and the dead-letter transport both read the validated options, so
/// a rejection reaches each of them.
/// </summary>
public sealed class RabbitMqConnectionTransportTests
{
    private const string _PASSWORD = "broker-secret-7f3a";

    private const string _REMOTE_PLAIN = "amqp://svc:" + _PASSWORD + "@rabbitmq.internal:5672/d2";

    private const string _REMOTE_ENCRYPTED = "amqps://svc:" + _PASSWORD + "@rabbitmq.internal:5671/d2";

    [Fact]
    public void ConnectionOptions_PlainAmqpToARemoteHost_FailsValidation()
    {
        using var provider = Compose(o => o.ConnectionUri = _REMOTE_PLAIN);

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("amqps")
            .And.NotContain(_PASSWORD, "the refusal must not carry the credential it refused");
    }

    [Fact]
    public void ConnectionOptions_PlainAmqpToARemoteHost_FailsTheStartupValidator()
    {
        // ValidateOnStart registers the check with the startup validator, which
        // the host runs before any hosted service starts. For a host, that is
        // what rejecting at composition means.
        using var provider = Compose(o => o.ConnectionUri = _REMOTE_PLAIN);

        var validator = provider.GetRequiredService<IStartupValidator>();

        var validate = () => validator.Validate();

        validate.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void MainConnection_PlainAmqpToARemoteHost_IsRefused()
    {
        using var provider = Compose(o => o.ConnectionUri = _REMOTE_PLAIN);

        IServiceProvider services = provider;

        var resolve = () => services.GetRequiredService<ID2Connection>();

        resolve.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void DeadLetterTransport_PlainAmqpToARemoteHost_IsRefused()
    {
        // ConsumerHostedService copies the connection options for the dedicated
        // dead-letter transport in its constructor, so the copy comes only
        // from options that passed validation.
        using var provider = Compose(o => o.ConnectionUri = _REMOTE_PLAIN);
        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();

        var construct = () => NewConsumerHost(scopes, options);

        construct.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("amqp://svc:" + _PASSWORD + "@localhost:5672/")]
    [InlineData("amqp://svc:" + _PASSWORD + "@127.0.0.1:5672/")]
    [InlineData("amqp://svc:" + _PASSWORD + "@[::1]:5672/")]
    public void ConnectionOptions_PlainAmqpToALoopbackHost_PassesValidation(string uri)
    {
        using var provider = Compose(o => o.ConnectionUri = uri);

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().NotThrow("a loopback hop never leaves the machine");
    }

    [Fact]
    public void ConnectionOptions_AmqpsToARemoteHost_PassesValidation()
    {
        using var provider = Compose(o => o.ConnectionUri = _REMOTE_ENCRYPTED);

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().NotThrow();
    }

    [Fact]
    public void ConnectionOptions_NoConnectionUri_PassesValidation()
    {
        // No address is configured, so there is no transport to reject.
        // Starting the connection still rejects it, as it did before this check
        // existed.
        using var provider = Compose(null);

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().NotThrow();
    }

    [Fact]
    public async Task Connection_NoConnectionUri_StartReconnectLoopThrows()
    {
        // The options pass validation without an address, and
        // StartReconnectLoop throws for the missing address.
        await using var provider = Compose(null);
        var connection = provider.GetRequiredService<ID2Connection>();

        var start = connection.StartReconnectLoop;

        start.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionUri is required*");
    }

    [Fact]
    public void ConnectionOptions_AnAddressThatIsNotAnAbsoluteUri_FailsValidation()
    {
        using var provider = Compose(o => o.ConnectionUri = "rabbitmq.internal");

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().Throw<OptionsValidationException>(
            "an address with no scheme cannot be shown to be encrypted");
    }

    [Fact]
    public void ConnectionOptions_PlainAmqpToARemoteHostWithAllowUnencryptedTransport_PassesValidation()
    {
        // AllowUnencryptedTransport is the opt-in that lets a plain amqp
        // address to a remote host pass validation. Without that option, this
        // test fails to compile.
        using var provider = Compose(o =>
        {
            o.ConnectionUri = _REMOTE_PLAIN;
            o.AllowUnencryptedTransport = true;
        });

        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        var resolve = () => options.Value;

        resolve.Should().NotThrow("the host opted in to an unencrypted transport");
    }

    [Fact]
    public void CopyConnectionSettings_CopiesTheAddressTheClientNameAndTheTransportChoice()
    {
        // The copy keeps the address, which RevealConnectionUri returns, the
        // client name and the transport choice. Without the copy method and
        // the reveal method, this test fails to compile.
        var original = new RabbitMqConnectionOptions
        {
            ConnectionUri = _REMOTE_PLAIN,
            ClientProvidedName = "copy-fixture",
            AllowUnencryptedTransport = true,
        };

        var copy = original.CopyConnectionSettings();

        copy.Should().NotBeSameAs(original);
        copy.RevealConnectionUri().Should().Be(_REMOTE_PLAIN);
        copy.ClientProvidedName.Should().Be("copy-fixture");
        copy.AllowUnencryptedTransport.Should().BeTrue();
    }

    [Fact]
    public async Task DeadLetterTransport_PlainAmqpToALoopbackHost_IsAdmitted()
    {
        using var provider = Compose(o => o.ConnectionUri = "amqp://svc:" + _PASSWORD + "@localhost:5672/");
        var options = provider.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();

        await using var host = NewConsumerHost(
            provider.GetRequiredService<IServiceScopeFactory>(), options);

        host.Should().NotBeNull("the dead-letter copy was made from options that passed");
    }

    private static ServiceProvider Compose(Action<RabbitMqConnectionOptions>? configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(configureConnection: configure);

        return services.BuildServiceProvider();
    }

    private static ConsumerHostedService NewConsumerHost(
        IServiceScopeFactory scopes, IOptions<RabbitMqConnectionOptions> options)
    {
        var registry = new SubscriberRegistry([]);

        return new ConsumerHostedService(
            new ReadyConnectionStub(),
            registry,
            scopes,
            new HandlerDispatcherFactory(registry, MessagingCatalog.Default),
            MessagingCatalog.Default,
            new NoOpTopologyDeclarerStub(),
            NullLoggerFactory.Instance,
            NullLogger<ConsumerHostedService>.Instance,
            options,
            Options.Create(new RabbitMqSubscriberOptions()));
    }
}
