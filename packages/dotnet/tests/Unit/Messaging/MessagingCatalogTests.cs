// -----------------------------------------------------------------------
// <copyright file="MessagingCatalogTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Collections.Concurrent;
using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Integration.Messaging;
using DcsvIo.D2.Tests.Unit.Messaging.Fixtures;
using Xunit;

/// <summary>
/// Tests of the catalog lookup: the <c>[MqPub]</c> read, the exceptions it
/// throws, and the per-instance resolution cache, which lets one catalog return
/// a different descriptor than another for the same CLR type.
/// </summary>
public sealed class MessagingCatalogTests
{
    private static readonly MqMessageDescriptor sr_auditDescriptor =
        MessagingCatalogFixture.Plaintext(
            MessagingCatalogFixture.SAMPLE_AUDIT,
            typeof(SampleAuditEvent),
            "d2.test.sample-audit",
            "topic");

    [Fact]
    public void RequireDescriptor_ForATypeTheCatalogCarries_ReturnsItsDescriptor()
    {
        var catalog = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, sr_auditDescriptor));

        var descriptor = catalog.RequireDescriptor(typeof(SampleAuditEvent));

        descriptor.Should().BeSameAs(sr_auditDescriptor);
    }

    [Fact]
    public void RequireDescriptor_ForATypeWithNoMqPub_ThrowsNamingTheType()
    {
        var catalog = MessagingCatalogFixture.ForMessages();

        var act = () => catalog.RequireDescriptor(typeof(SamplePlainEvent));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*" + typeof(SamplePlainEvent).FullName + "*[MqPub]*");
    }

    [Fact]
    public void RequireDescriptor_ForAConstantTheCatalogLacks_ThrowsNamingTheConstant()
    {
        var catalog = MessagingCatalogFixture.ForMessages();

        var act = () => catalog.RequireDescriptor(typeof(SampleAuditEvent));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*" + MessagingCatalogFixture.SAMPLE_AUDIT + "*");
    }

    [Fact]
    public void RequireDescriptor_WhenTheEntryNamesADifferentClrType_ThrowsNamingBoth()
    {
        var mismatched = sr_auditDescriptor with
        {
            MessageTypeName = "DcsvIo.D2.Nowhere.SomeOtherEvent",
        };
        var catalog = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, mismatched));

        var act = () => catalog.RequireDescriptor(typeof(SampleAuditEvent));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DcsvIo.D2.Nowhere.SomeOtherEvent*");
    }

    [Fact]
    public void RequireDescriptor_WithANullType_Throws()
    {
        var catalog = MessagingCatalogFixture.ForMessages();

        var act = () => catalog.RequireDescriptor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireDescriptor_OnASecondCatalog_AnswersFromItsOwnEntries()
    {
        // The cache is per instance. A static cache keyed by CLR type alone
        // would return the first catalog's descriptor from the second catalog,
        // and this assertion fails if the cache is static.
        var other = sr_auditDescriptor with { Exchange = "d2.test.sample-audit-other" };
        var first = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, sr_auditDescriptor));
        var second = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, other));

        first.RequireDescriptor(typeof(SampleAuditEvent)).Exchange
            .Should().Be("d2.test.sample-audit");
        second.RequireDescriptor(typeof(SampleAuditEvent)).Exchange
            .Should().Be("d2.test.sample-audit-other");
    }

    [Fact]
    public void RequireDescriptor_OnACatalogBuiltWithAWithExpression_UsesTheNewEntries()
    {
        // The record copy constructor is hand-written, so the copy does not
        // receive the cache. The test resolves on the source first, so a copied
        // cache would hold an entry.
        var source = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, sr_auditDescriptor));
        source.RequireDescriptor(typeof(SampleAuditEvent));

        var replaced = sr_auditDescriptor with { Exchange = "d2.test.sample-audit-replaced" };
        var copy = source with
        {
            Messages = new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal)
            {
                [MessagingCatalogFixture.SAMPLE_AUDIT] = replaced,
            },
        };

        copy.RequireDescriptor(typeof(SampleAuditEvent)).Exchange
            .Should().Be("d2.test.sample-audit-replaced");
    }

    [Fact]
    public async Task RequireDescriptor_UnderConcurrentCallersOnOneCatalog_ReturnsOneDescriptor()
    {
        // All callers share one catalog, so they use the same cache at the same time.
        var catalog = MessagingCatalogFixture.ForMessages(
            (MessagingCatalogFixture.SAMPLE_AUDIT, sr_auditDescriptor));
        var seen = new ConcurrentBag<MqMessageDescriptor>();

        await Parallel.ForAsync(0, 64, (_, _) =>
        {
            seen.Add(catalog.RequireDescriptor(typeof(SampleAuditEvent)));
            return ValueTask.CompletedTask;
        });

        seen.Should().HaveCount(64);
        seen.Should().AllSatisfy(d => d.Should().BeSameAs(sr_auditDescriptor));
    }

    [Fact]
    public void Default_CarriesTheGeneratedFrameworkRegistries()
    {
        MessagingCatalog.Default.Messages
            .Should().BeSameAs(MqMessagesRegistry.ByConstant);
        MessagingCatalog.Default.Subscriptions
            .Should().BeSameAs(MqSubscriptionsRegistry.ByConstant);
    }

    [Fact]
    public void Default_CarriesNoProductMessageEntry()
    {
        // The default catalog has no message entries. A host that registers no
        // catalog gets the default one, and its first publish of a message
        // fails.
        MessagingCatalog.Default.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Default_CarriesNoProductSubscriptionEntry()
    {
        MessagingCatalog.Default.Subscriptions.Should().BeEmpty();
    }

    [Fact]
    public void Deconstruct_YieldsTheTwoDictionariesItWasBuiltFrom()
    {
        IReadOnlyDictionary<string, MqMessageDescriptor> messages =
            new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, MqSubscriptionDescriptor> subscriptions =
            new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal);
        var catalog = new MessagingCatalog(messages, subscriptions);

        var (gotMessages, gotSubscriptions) = catalog;

        gotMessages.Should().BeSameAs(messages);
        gotSubscriptions.Should().BeSameAs(subscriptions);
    }

    [Fact]
    public void Equality_IsBySharedDictionaryReference()
    {
        IReadOnlyDictionary<string, MqMessageDescriptor> messages =
            new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, MqSubscriptionDescriptor> subscriptions =
            new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal);
        var a = new MessagingCatalog(messages, subscriptions);
        var b = new MessagingCatalog(messages, subscriptions);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentDictionaries_IsFalse()
    {
        IReadOnlyDictionary<string, MqSubscriptionDescriptor> subscriptions =
            new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal);
        var a = new MessagingCatalog(
            new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal), subscriptions);
        var b = new MessagingCatalog(
            new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal), subscriptions);

        a.Should().NotBe(b);
        a.Equals((object?)b).Should().BeFalse();
    }

    [Fact]
    public void ToString_NamesTheRecordType()
    {
        var catalog = MessagingCatalogFixture.ForMessages();

        catalog.ToString().Should().StartWith(nameof(MessagingCatalog));
    }

    [Fact]
    public void IntegrationFixtureCatalog_ResolvesEveryFixtureMessageType()
    {
        var catalog = IntegrationMessageFixtures.Catalog;

        var plain = catalog.RequireDescriptor(typeof(IntegrationPlaintextEvent));
        plain.IsPlaintext.Should().BeTrue();
        plain.Exchange.Should().Be("d2.test.integration-plaintext");

        var audit = catalog.RequireDescriptor(typeof(IntegrationAuditEvent));
        audit.IsPlaintext.Should().BeFalse();
        audit.Exchange.Should().Be("d2.test.integration-audit");
        audit.Encryption.Should().Be(IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN);

        var broadcast = catalog.RequireDescriptor(typeof(BroadcastFixtureEvent));
        broadcast.IsPlaintext.Should().BeTrue();
        broadcast.ExchangeType.Should().Be("fanout");
    }
}
