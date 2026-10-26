// -----------------------------------------------------------------------
// <copyright file="SampleAuditHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>
/// Test handler for the closed-generic dispatcher build path that
/// <see cref="HandlerDispatcherFactoryTests"/> verifies. Nothing invokes the
/// handler body, because those tests assert only the dispatcher type.
/// </summary>
public sealed class SampleAuditHandler : BaseHandler<SampleAuditHandler, SampleAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public SampleAuditHandler(HandlerContext<SampleAuditHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        SampleAuditEvent input, CancellationToken ct)
    {
        return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
    }
}
