// -----------------------------------------------------------------------
// <copyright file="ScopeElevation.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;

/// <summary>
/// Internal lease that unions composition scopes onto a
/// <see cref="MutableRequestContext"/> for a bounded window and removes exactly
/// its own contribution on dispose. Leases over one context compose: a scope one
/// lease elevated stays in force until every lease that asked for it has been
/// disposed, and a scope the ambient set already contained is never removed.
/// </summary>
/// <remarks>
/// Every scope set this type publishes is built before it is assigned and never
/// mutated afterwards, so a reader of <c>ctx.Scopes</c> gets a set that no code
/// writes to. The same holds for the per-scope lease counts beside it. The gate
/// below makes the swap of one such pair for the next atomic.
/// </remarks>
internal static class ScopeElevation
{
    /// <summary>
    /// One gate per context, holding the lease counts and guarding the swap
    /// alone. The replacement set and counts are built outside the gate from the
    /// pair the builder read, and the swap takes effect only while both are
    /// still the published ones, so two leases over one context cannot lose each
    /// other's write.
    /// </summary>
    private static readonly ConditionalWeakTable<MutableRequestContext, ElevationGate>
        sr_gates = new();

    /// <summary>
    /// Unions <paramref name="scopes"/> onto ambient Scopes and returns a
    /// disposable that releases this lease's claim on those scopes on Dispose
    /// (dispose-twice safe; the release happens even when the try body throws).
    /// A scope another live lease already elevated stays in force until that
    /// lease is disposed too.
    /// </summary>
    /// <param name="ctx">Mutable request context for this request scope.</param>
    /// <param name="scopes">Scopes to push for the lease window.</param>
    /// <returns>Restore lease.</returns>
    [MustDisposeResource]
    internal static IDisposable Push(MutableRequestContext ctx, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(scopes);

        var gate = sr_gates.GetOrCreateValue(ctx);

        while (true)
        {
            var current = ctx.Scopes;
            var counts = gate.Counts;
            var elevated = new HashSet<string>(current, StringComparer.Ordinal);
            var nextCounts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
            var claimed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scope in scopes)
            {
                if (!scope.Truthy() || claimed.Contains(scope))
                    continue;

                if (nextCounts.TryGetValue(scope, out var held))
                {
                    // Another live lease elevated it. Take a claim so it
                    // survives that lease's dispose.
                    nextCounts[scope] = held + 1;
                    elevated.Add(scope);
                    claimed.Add(scope);
                }
                else if (elevated.Add(scope))
                {
                    nextCounts[scope] = 1;
                    claimed.Add(scope);
                }

                // Otherwise the ambient set contained the scope before any
                // lease did, so no lease may remove it and none takes a claim.
            }

            lock (gate)
            {
                if (!ReferenceEquals(ctx.Scopes, current)
                    || !ReferenceEquals(gate.Counts, counts))
                {
                    continue;
                }

                ctx.Scopes = elevated;
                gate.Counts = nextCounts;
            }

            return new ScopeElevationLease(ctx, gate, claimed);
        }
    }

    /// <summary>
    /// Per-context elevation state: the lease count per scope, and the object
    /// every swap of that state locks on.
    /// </summary>
    private sealed class ElevationGate
    {
        /// <summary>
        /// Gets or sets how many live leases claim each elevated scope. Every
        /// published dictionary is built before assignment and never mutated
        /// afterwards.
        /// </summary>
        public IReadOnlyDictionary<string, int> Counts { get; set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private sealed class ScopeElevationLease : IDisposable
    {
        private readonly ElevationGate r_gate;
        private MutableRequestContext? _ctx;
        private IReadOnlySet<string>? _claimed;

        public ScopeElevationLease(
            MutableRequestContext ctx,
            ElevationGate gate,
            IReadOnlySet<string> claimed)
        {
            _ctx = ctx;
            r_gate = gate;
            _claimed = claimed;
        }

        public void Dispose()
        {
            var ctx = _ctx;
            var claimed = _claimed;

            if (ctx is null || claimed is null)
                return;

            _ctx = null;
            _claimed = null;

            if (claimed.Count == 0)
                return;

            while (true)
            {
                var current = ctx.Scopes;
                var counts = r_gate.Counts;
                var restored = new HashSet<string>(current, StringComparer.Ordinal);
                var nextCounts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
                var changed = false;

                foreach (var scope in claimed)
                {
                    if (!nextCounts.TryGetValue(scope, out var held))
                        continue;

                    if (held > 1)
                    {
                        nextCounts[scope] = held - 1;
                        changed = true;
                        continue;
                    }

                    nextCounts.Remove(scope);
                    restored.Remove(scope);
                    changed = true;
                }

                if (!changed)
                    return;

                lock (r_gate)
                {
                    if (!ReferenceEquals(ctx.Scopes, current)
                        || !ReferenceEquals(r_gate.Counts, counts))
                    {
                        continue;
                    }

                    ctx.Scopes = restored;
                    r_gate.Counts = nextCounts;
                }

                return;
            }
        }
    }
}
