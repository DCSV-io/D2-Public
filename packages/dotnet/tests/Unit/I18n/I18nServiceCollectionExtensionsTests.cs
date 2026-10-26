// -----------------------------------------------------------------------
// <copyright file="I18nServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class I18nServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2I18n_RegistersSupportedLocalesAsSingleton()
    {
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        services.AddD2I18n(EmptyConfig(), dir.Path);

        using var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredService<SupportedLocales>();
        var second = sp.GetRequiredService<SupportedLocales>();

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void AddD2I18n_RegistersTranslatorAsSingleton()
    {
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        services.AddD2I18n(EmptyConfig(), dir.Path);

        using var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredService<ITranslator>();
        var second = sp.GetRequiredService<ITranslator>();

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void AddD2I18n_ResolvedTranslator_ActuallyTranslatesKnownKey()
    {
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        services.AddD2I18n(EmptyConfig(), dir.Path);

        using var sp = services.BuildServiceProvider();
        var translator = sp.GetRequiredService<ITranslator>();

        translator.T("en-US", TK.Common.Errors.NOT_FOUND).Should().Be("Not found.");
    }

    [Fact]
    public void AddD2I18n_ConfigurationDrivesSupportedLocales()
    {
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        services.AddD2I18n(
            ConfigWith(("PUBLIC_DEFAULT_LOCALE", "fr-FR")),
            dir.Path);

        using var sp = services.BuildServiceProvider();
        var sl = sp.GetRequiredService<SupportedLocales>();

        sl.Base.Should().Be("fr-FR");
    }

    [Fact]
    public void AddD2I18n_NullServices_Throws()
    {
        var act = () => I18nServiceCollectionExtensions.AddD2I18n(
            null!,
            EmptyConfig());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2I18n_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2I18n(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2I18n_ReturnsServicesForChaining()
    {
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        var returned = services.AddD2I18n(EmptyConfig(), dir.Path);

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2I18n_CalledTwice_RegistersOnceViaTryAdd()
    {
        // TryAddSingleton semantics: the second call does nothing, and the
        // first registration stays.
        using var dir = NewTempCatalogWithEnUs();
        var services = new ServiceCollection();

        services.AddD2I18n(EmptyConfig(), dir.Path);
        services.AddD2I18n(EmptyConfig(), dir.Path);

        // Each type has exactly one registration.
        services.Should().ContainSingle(d => d.ServiceType == typeof(SupportedLocales));
        services.Should().ContainSingle(d => d.ServiceType == typeof(ITranslator));
    }

    [Fact]
    public void AddD2I18n_DefaultMessagesDirectory_UsesAppContextBaseDirectorySlashMessages()
    {
        // The build copies the message catalogs to AppContext.BaseDirectory/messages.
        // The translator registered with no directory argument returns the en-US value
        // that catalog holds for the key.
        var catalog = Path.Combine(AppContext.BaseDirectory, "messages", "en-US.json");
        using var document = JsonDocument.Parse(File.ReadAllText(catalog));
        var expected = document.RootElement
            .GetProperty(TK.Common.Errors.NOT_FOUND.Key)
            .GetString();
        var services = new ServiceCollection();
        services.AddD2I18n(EmptyConfig());

        using var sp = services.BuildServiceProvider();
        var translator = sp.GetRequiredService<ITranslator>();

        translator.T("en-US", TK.Common.Errors.NOT_FOUND).Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static IConfiguration EmptyConfig()
        => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigWith(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static TempCatalog NewTempCatalogWithEnUs()
    {
        var dir = new TempCatalog();
        var json = System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string> { ["common_errors_NOT_FOUND"] = "Not found." });
        File.WriteAllText(Path.Combine(dir.Path, "en-US.json"), json);
        return dir;
    }

    [MustDisposeResource]
    private sealed class TempCatalog : IDisposable
    {
        public TempCatalog()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "d2-i18n-di-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
            catch (IOException)
            {
                // A file lock during parallel test cleanup can block the delete.
                // The cleanup is best-effort.
            }
        }
    }
}
