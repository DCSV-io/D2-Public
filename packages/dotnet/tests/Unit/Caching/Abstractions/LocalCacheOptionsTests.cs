// -----------------------------------------------------------------------
// <copyright file="LocalCacheOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Abstractions;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Caching;
using Xunit;

/// <summary>
/// Pins the production defaults on <see cref="LocalCacheOptions"/>.
/// <c>MaxEntries</c> caps process RSS when a service runs a single shared
/// <see cref="ILocalCache"/> singleton, <c>DefaultExpiration</c> is the
/// expiration used when a call passes <c>null</c>, and <c>KeyPrefix</c> is
/// empty (not <c>null</c>) so that string concatenation is safe. A "tune for
/// smaller services" change that drops <c>MaxEntries</c> by 10x, without
/// checking the services that rely on the default, would silently cause LRU
/// thrash in production only.
/// </summary>
public sealed class LocalCacheOptionsTests
{
    [Fact]
    public void Default_MaxEntries_Is100K()
    {
        var options = new LocalCacheOptions();

        options.MaxEntries.Should().Be(100_000);
    }

    [Fact]
    public void Default_DefaultExpiration_IsOneHour()
    {
        var options = new LocalCacheOptions();

        options.DefaultExpiration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Default_KeyPrefix_IsEmptyString()
    {
        var options = new LocalCacheOptions();

        // Pin: empty string, not null, because code that concatenates
        // `KeyPrefix + key` does so without a null guard.
        options.KeyPrefix.Should().Be(string.Empty);
    }

    [Fact]
    public void Properties_AreMutable_ForOptionsPatternBinding()
    {
        // Adversarial: `Microsoft.Extensions.Options` configure delegates need
        // settable properties to bind from configuration. This test pins that
        // MaxEntries, DefaultExpiration and KeyPrefix stay settable: a property
        // that becomes init-only silently breaks the binding.
        var options = new LocalCacheOptions
        {
            MaxEntries = 10_000,
            DefaultExpiration = TimeSpan.FromMinutes(15),
            KeyPrefix = "edge:",
        };

        options.MaxEntries.Should().Be(10_000);
        options.DefaultExpiration.Should().Be(TimeSpan.FromMinutes(15));
        options.KeyPrefix.Should().Be("edge:");
    }
}
