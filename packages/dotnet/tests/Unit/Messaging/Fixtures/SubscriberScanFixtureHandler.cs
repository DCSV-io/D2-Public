// -----------------------------------------------------------------------
// <copyright file="SubscriberScanFixtureHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Fixtures;

using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Result;

/// <summary>Test fixture: the one <c>[MqSub]</c> handler in this assembly, so
/// that the assembly scan finds a handler. Its constant is a fixture constant
/// that no shipped spec declares, so whether the constant resolves depends on
/// the catalog that the scan receives.</summary>
[MqSub(MessagingCatalogFixture.SCAN_SUBSCRIPTION)]
public sealed class SubscriberScanFixtureHandler
    : BaseHandler<SubscriberScanFixtureHandler, SubscriberScanFixtureEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public SubscriberScanFixtureHandler(
        HandlerContext<SubscriberScanFixtureHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        SubscriberScanFixtureEvent input, CancellationToken ct) =>
        new(D2Result<Unit>.Ok(Unit.Value));
}
