// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionOptionsRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Connection;

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Tests.Unit.Auth;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// The broker credential inside <see cref="RabbitMqConnectionOptions"/> is kept in a
/// private field behind one reveal method: no public property returns it, the
/// type is marked for redaction, neither <c>ToString</c> nor a serializer
/// renders it, and the reveal method has a fixed set of production callers.
/// </summary>
/// <remarks>
/// The cases that call the reveal method fail to compile without it; the other
/// cases assert the redaction markers, the missing public getter, and the
/// <c>ToString</c> and serializer output. The address is a private field and not
/// a public auto-property, so a serializer does not read it.
/// </remarks>
public sealed class RabbitMqConnectionOptionsRedactionTests
{
    private const string _PASSWORD = "broker-secret-91c2";

    private const string _URI = "amqps://svc:" + _PASSWORD + "@rabbitmq.internal:5671/d2";

    private const string _REVEAL_SYMBOL = "RevealConnectionUri";

    // The file that declares the reveal method.
    private static readonly string[] sr_definitionFiles =
    [
        "RabbitMqConnectionOptions.cs",
    ];

    // The production files allowed to call it: where the shared connection and the
    // dead-letter transport build a connection to the broker. Adding an entry here
    // is a deliberate, reviewed widening of where the credential is read.
    private static readonly string[] sr_allowedCallerFiles =
    [
        "RabbitMqConnection.cs",
        "DlqRepublisher.cs",
    ];

    [Fact]
    public void RabbitMqConnectionOptions_IsMarkedForSecretRedaction()
    {
        var marker = typeof(RabbitMqConnectionOptions).GetCustomAttribute<RedactDataAttribute>();

        marker.Should().NotBeNull();
        marker.Reason.Should().Be(RedactReason.SecretInformation);
    }

    [Fact]
    public void ConnectionUri_IsMarkedForSecretRedaction()
    {
        var property = typeof(RabbitMqConnectionOptions).GetProperty(
            nameof(RabbitMqConnectionOptions.ConnectionUri));

        property.Should().NotBeNull();
        var marker = property.GetCustomAttribute<RedactDataAttribute>();
        marker.Should().NotBeNull();
        marker.Reason.Should().Be(RedactReason.SecretInformation);
    }

    [Fact]
    public void EveryDeclaredStringField_IsMarkedForSecretRedaction()
    {
        // The declared string fields are where the credential is held. The
        // compiler's backing fields belong to the public properties, which hold
        // no credential.
        var fields = typeof(RabbitMqConnectionOptions)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(string)
                && f.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToList();

        fields.Should().NotBeEmpty();
        fields.Should().AllSatisfy(field =>
        {
            var marker = field.GetCustomAttribute<RedactDataAttribute>();
            marker.Should().NotBeNull(field.Name);
            marker.Reason.Should().Be(RedactReason.SecretInformation, field.Name);
        });
    }

    [Fact]
    public void ConnectionUri_HasNoPublicGetter()
    {
        var property = typeof(RabbitMqConnectionOptions).GetProperty(
            nameof(RabbitMqConnectionOptions.ConnectionUri));

        property.Should().NotBeNull();
        property.GetGetMethod(nonPublic: true).Should().BeNull(
            "the address is read only through the reveal method");
    }

    [Fact]
    public void ConnectionUri_IsNotFilledByConfigurationBinding()
    {
        // The binder skips a property with no public getter, so configuration
        // does not set the address. The address is set in the
        // configureConnection delegate.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionUri"] = _URI,
                ["ClientProvidedName"] = "bound-fixture",
            })
            .Build();
        var options = new RabbitMqConnectionOptions();

        configuration.Bind(options);

        options.ClientProvidedName.Should().Be("bound-fixture");
        options.RevealConnectionUri().Should().BeEmpty();
    }

    [Fact]
    public void RevealConnectionUri_ReturnsTheAddressThatWasSet()
    {
        var options = new RabbitMqConnectionOptions { ConnectionUri = _URI };

        options.RevealConnectionUri().Should().Be(_URI);
    }

    [Fact]
    public void ToString_DoesNotRenderTheCredential()
    {
        var options = new RabbitMqConnectionOptions { ConnectionUri = _URI };

        options.ToString().Should().NotContain(_PASSWORD);
    }

    [Fact]
    public void JsonSerialization_DoesNotRenderTheCredential()
    {
        var options = new RabbitMqConnectionOptions { ConnectionUri = _URI };

        var json = JsonSerializer.Serialize(options);

        json.Should().NotContain(_PASSWORD).And.NotContain("rabbitmq.internal");
    }

    [Fact]
    public void RevealConnectionUri_HasNoUnexpectedProductionCaller()
    {
        var root = TestPaths.PublicPackagesDotnetRoot();
        var callers = new List<string>();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcludedPath(file))
                continue;

            var fileName = Path.GetFileName(file);

            if (sr_definitionFiles.Contains(fileName, StringComparer.Ordinal))
                continue;

            if (!File.ReadAllText(file).Contains(_REVEAL_SYMBOL, StringComparison.Ordinal))
                continue;

            if (sr_allowedCallerFiles.Contains(fileName, StringComparer.Ordinal))
                callers.Add(fileName);
            else
                offenders.Add(file);
        }

        offenders.Should().BeEmpty(
            "the reveal method reads the broker credential, and only the listed connection "
            + "builders may call it. Offenders: " + string.Join(", ", offenders));
        callers.Should().BeEquivalentTo(
            sr_allowedCallerFiles,
            "a listed caller that no longer calls the reveal method is a stale entry");
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace('\\', '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Generated/", StringComparison.Ordinal)
            || normalized.Contains(".g.cs", StringComparison.Ordinal)
            || normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase);
    }
}
