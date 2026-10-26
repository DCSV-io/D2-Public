// -----------------------------------------------------------------------
// <copyright file="BeforeFirstSaveHookFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox.Fixtures;

using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Runs one piece of work before the first save on its context reaches the
/// database, so a second writer can be placed between a read and the write
/// that follows it inside one call.
/// </summary>
/// <param name="beforeFirstSave">The work to run once, before the first save.</param>
internal sealed class BeforeFirstSaveHookFixture(Func<CancellationToken, Task> beforeFirstSave)
    : SaveChangesInterceptor
{
    private int _fired;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _fired, 1) == 0)
            await beforeFirstSave(cancellationToken);

        return result;
    }
}
