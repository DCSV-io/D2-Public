// -----------------------------------------------------------------------
// <copyright file="FakeSystemWorkScopeFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Context.Abstractions;

/// <summary>
/// Hands out a scope over a service provider the test supplies, standing in for
/// the establishment machinery a composed host brings. It records how many scopes
/// were opened and how many were disposed, so a leak shows up as a mismatch
/// rather than as a slow test.
/// </summary>
/// <param name="services">What the scope resolves from.</param>
internal sealed class FakeSystemWorkScopeFactory(IServiceProvider services)
    : ISystemWorkScopeFactory
{
    /// <summary>Gets how many scopes were opened.</summary>
    public int Opened { get; private set; }

    /// <summary>Gets how many scopes were disposed.</summary>
    public int Disposed { get; private set; }

    /// <summary>Gets the cancellation token the last call received.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <inheritdoc/>
    public ValueTask<ISystemWorkScope> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        Opened++;
        LastToken = cancellationToken;

        return ValueTask.FromResult<ISystemWorkScope>(
            new Scope(services, () => Disposed++));
    }

    private sealed class Scope(IServiceProvider services, Action onDispose) : ISystemWorkScope
    {
        public IServiceProvider Services { get; } = services;

        public ValueTask DisposeAsync()
        {
            onDispose();

            return ValueTask.CompletedTask;
        }
    }
}
