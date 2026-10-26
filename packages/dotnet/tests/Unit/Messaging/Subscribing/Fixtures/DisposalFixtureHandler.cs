// -----------------------------------------------------------------------
// <copyright file="DisposalFixtureHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>Companion handler for <see cref="DisposalFixtureMessage"/>.</summary>
internal sealed class DisposalFixtureHandler
    : BaseHandler<DisposalFixtureHandler, DisposalFixtureMessage, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">DI-resolved handler context.</param>
    public DisposalFixtureHandler(HandlerContext<DisposalFixtureHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        DisposalFixtureMessage input,
        CancellationToken ct)
        => new(D2Result<Unit>.Ok(Unit.Value));
}
