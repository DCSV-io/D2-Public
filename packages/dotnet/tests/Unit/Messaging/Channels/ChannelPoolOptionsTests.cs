// -----------------------------------------------------------------------
// <copyright file="ChannelPoolOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Channels;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using Xunit;

/// <summary>
/// Tests of the defaults and overrides of <see cref="ChannelPoolOptions"/>. The
/// idle-TTL setting controls the eviction of stale pool channels. The tests pin
/// the default and the override path. A change to either fails a test, so the
/// steady-state pool behavior does not change without a test failing.
/// </summary>
public sealed class ChannelPoolOptionsTests
{
    [Fact]
    public void IdleTtl_DefaultIsFiveMinutes()
    {
        new ChannelPoolOptions().IdleTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void IdleTtl_OverrideTakesEffect()
    {
        var opts = new ChannelPoolOptions { IdleTtl = TimeSpan.FromMinutes(1) };
        opts.IdleTtl.Should().Be(TimeSpan.FromMinutes(1));
    }
}
