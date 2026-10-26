// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionSourceStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Tests.Unit.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Deny-by-default coverage for <see cref="SealedEncryptionSourceStartupCheck"/> - the sealed
/// provenance guard: an unmarked (static) sealed recipient crashes a non-Development host,
/// a recipient marked Managed passes, and a Development host logs-and-proceeds.
/// </summary>
public sealed class SealedEncryptionSourceStartupCheckTests
{
    private const string _SERVICE_ID = "fixture-sealed-recipient";

    [Fact]
    public async Task UnmarkedRecipient_NonDevelopment_Throws()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{_SERVICE_ID}*static*");
    }

    [Fact]
    public async Task ManagedMarkedRecipient_NonDevelopment_Passes()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.Managed);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync("a recipient marked Managed passes in every env");
    }

    [Fact]
    public async Task UnmarkedRecipient_NonDevelopment_ExceptionStatesManagedRegistration()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage(
                $"Sealed encryption recipient '{_SERVICE_ID}' is backed by a static sealing-key "
                + "source rather than a managed, rotation-aware keyring. Back its sealer with a managed "
                + "keyring and mark it Managed via MarkD2EncryptionSource before starting a "
                + "non-Development host.");
    }

    [Fact]
    public async Task ManagedAndStaticMarkers_NonDevelopment_Throws()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.Managed);
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.StaticFactory);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ManagedMarkedRecipient_NonDevelopment_LogsPassedEntry()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.Managed);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
            });

        var logger = new TestLogger<SealedEncryptionSourceStartupCheck>();

        await CheckWithLogger(provider, logger).StartAsync(CancellationToken.None);

        logger.Entries.Should().ContainSingle(e => e.EventId.Id == 2)
            .Which.Message.Should().Be(
                $"Sealed encryption recipient {_SERVICE_ID} is managed-sourced - "
                + "provenance check passed.");
    }

    [Fact]
    public async Task UnmarkedRecipient_Development_LogsWarningNamingManagedRegistration()
    {
        var provider = BuildProvider(
            isDevelopment: true,
            services => services.AddD2SealedEncryptionRecipient(_SERVICE_ID));

        var logger = new TestLogger<SealedEncryptionSourceStartupCheck>();

        await CheckWithLogger(provider, logger).StartAsync(CancellationToken.None);

        logger.Entries.Should().ContainSingle(e => e.EventId.Id == 3)
            .Which.Message.Should().Be(
                $"Sealed encryption recipient {_SERVICE_ID} is backed by a static / unmarked "
                + "source rather than a managed, rotation-aware keyring. Permitted only in a "
                + "Development host; a non-Development host crashes. Back its sealer with a "
                + "managed keyring and mark it Managed via MarkD2EncryptionSource.");
    }

    [Fact]
    public async Task StaticFactoryMarkedRecipient_NonDevelopment_Throws()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.MarkD2EncryptionSource(_SERVICE_ID, EncryptionKeyringSource.StaticFactory);
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UnmarkedRecipient_Development_LogsAndProceeds()
    {
        var provider = BuildProvider(
            isDevelopment: true,
            services =>
            {
                services.AddD2SealedEncryptionRecipient(_SERVICE_ID);
                services.AddD2SealedEncryptionSourceCheck();
            });

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync("a Development host tolerates a static source (loud log)");
    }

    [Fact]
    public async Task NoRecipients_NoOps()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services => services.AddD2SealedEncryptionSourceCheck());

        var act = () => RunCheckAsync(provider);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SourceCheck_CalledTwice_RegistersHostedServiceOnce()
    {
        var provider = BuildProvider(
            isDevelopment: false,
            services =>
            {
                services.AddD2SealedEncryptionSourceCheck();
                services.AddD2SealedEncryptionSourceCheck();
            });

        provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionSourceStartupCheck>()
            .Should().ContainSingle();
    }

    private static ServiceProvider BuildProvider(
        bool isDevelopment, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment(isDevelopment));
        configure(services);

        return services.BuildServiceProvider();
    }

    private static SealedEncryptionSourceStartupCheck CheckWithLogger(
        ServiceProvider provider, TestLogger<SealedEncryptionSourceStartupCheck> logger)
        => new(provider, provider.GetRequiredService<SealedEncryptionRegistry>(), logger);

    private static async Task RunCheckAsync(ServiceProvider provider)
    {
        var check = provider.GetServices<IHostedService>()
            .OfType<SealedEncryptionSourceStartupCheck>()
            .Single();

        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
    }

    private sealed class StubEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            isDevelopment ? Environments.Development : Environments.Production;

        public string ApplicationName { get; set; } = "D2.Tests";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
