// -----------------------------------------------------------------------
// <copyright file="ScopeElevationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// ScopeElevation.Push + BaseHandler.ElevateScopes lease regression suite.
/// </summary>
[Collection("HandlerTelemetrySerial")]
public sealed class ScopeElevationTests
{
    [Fact]
    public void Push_UnionsScopes_And_Dispose_Restores()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        using (ScopeElevation.Push(ctx, "internal.example.session.end"))
        {
            ctx.Scopes.Should().Contain("product.read");
            ctx.Scopes.Should().Contain("internal.example.session.end");
        }

        ctx.Scopes.Should().Equal(new[] { "product.read" });
        ctx.Scopes.Should().NotContain("internal.example.session.end");
    }

    [Fact]
    public void Push_DisposeTwice_Safe()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "a" },
        };

        var lease = ScopeElevation.Push(ctx, "b");
        lease.Dispose();
        var act = () => lease.Dispose();
        act.Should().NotThrow();
        ctx.Scopes.Should().Equal(new[] { "a" });
    }

    [Fact]
    public void Push_EmptyScopes_NoOpUnion_StillRestores()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "only" },
        };

        using (ScopeElevation.Push(ctx))
        {
            ctx.Scopes.Should().Equal(new[] { "only" });
        }

        ctx.Scopes.Should().Equal(new[] { "only" });
    }

    [Fact]
    public void Push_ScopeAlreadyGranted_SurvivesDispose()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "already" },
        };

        using (ScopeElevation.Push(ctx, "already", "added"))
        {
            ctx.Scopes.Should().BeEquivalentTo(new[] { "already", "added" });
        }

        ctx.Scopes.Should().Equal(new[] { "already" });
    }

    [Fact]
    public void Push_TwoOverlappingLeases_EachRemovesOnlyTheScopesItAdded()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "base" },
        };

        var first = ScopeElevation.Push(ctx, "first");
        var second = ScopeElevation.Push(ctx, "second");

        first.Dispose();

        // A snapshot restore would drop "second" here and leave "first" behind
        // after the second dispose below.
        ctx.Scopes.Should().BeEquivalentTo(new[] { "base", "second" });

        second.Dispose();

        ctx.Scopes.Should().Equal(new[] { "base" });
    }

    [Fact]
    public void Push_TwoLeasesOverTheSameScope_HoldItUntilBothAreDisposed()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "base" },
        };

        var first = ScopeElevation.Push(ctx, "shared");
        var second = ScopeElevation.Push(ctx, "shared");

        first.Dispose();

        // The second lease asked for a scope the first had already published.
        // A lease that counts only the scopes it added itself skips that scope
        // here, and the second lease spends the rest of its window unelevated.
        ctx.Scopes.Should().BeEquivalentTo(new[] { "base", "shared" });

        second.Dispose();

        ctx.Scopes.Should().Equal(new[] { "base" });
    }

    [Fact]
    public void Push_ThreeLeasesOverTheSameScope_ReleaseInAnyOrder()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "base" },
        };

        var first = ScopeElevation.Push(ctx, "shared");
        var second = ScopeElevation.Push(ctx, "shared", "own");
        var third = ScopeElevation.Push(ctx, "shared");

        second.Dispose();
        ctx.Scopes.Should().BeEquivalentTo(new[] { "base", "shared" });

        third.Dispose();
        ctx.Scopes.Should().BeEquivalentTo(new[] { "base", "shared" });

        first.Dispose();
        ctx.Scopes.Should().Equal(new[] { "base" });
    }

    [Fact]
    public async Task Push_ManyConcurrentLeasesOverOneContext_LeaveTheBaseSetBehind()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "base" },
        };

        var tasks = new Task[32];

        for (var i = 0; i < tasks.Length; i++)
        {
            // Half the tasks share one scope name, so overlapping claims on one
            // scope run concurrently with claims nobody else holds.
            var scope = i % 2 == 0
                ? "elevated.shared"
                : "elevated." + i.ToString(CultureInfo.InvariantCulture);

            tasks[i] = Task.Run(() =>
            {
                for (var round = 0; round < 50; round++)
                {
                    using (ScopeElevation.Push(ctx, scope))
                    {
                        var inWindow = ctx.Scopes;
                        inWindow.Should().Contain("base");
                        inWindow.Should().Contain(scope);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        ctx.Scopes.Should().Equal(new[] { "base" });
    }

    [Fact]
    public void Push_ThrowInTry_StillRestores()
    {
        var ctx = new MutableRequestContext
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "base" },
        };

        try
        {
            using (ScopeElevation.Push(ctx, "elevated"))
            {
                ctx.Scopes.Should().Contain("elevated");
                throw new InvalidOperationException("boom");
            }
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        ctx.Scopes.Should().Equal(new[] { "base" });
        ctx.Scopes.Should().NotContain("elevated");
    }

    [Fact]
    public async Task ElevateScopes_PushUnion_Require_And_Restore()
    {
        var mutable = new MutableRequestContext
        {
            TraceId = "trace-elevate",
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        IReadOnlySet<string>? duringExecute = null;

        var handler = new ElevatingHandlerFixture(
            new HandlerContext<ElevatingHandlerFixture>(
                mutable,
                NullLogger<ElevatingHandlerFixture>.Instance),
            async (_, _) =>
            {
                duringExecute = mutable.Scopes;
                await Task.Yield();
                return D2Result<string?>.Ok("ok");
            },
            new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(StringComparer.Ordinal) { "product.read" }),
                ElevateScopes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "internal.example.session.end",
                },
            });

        var result = await handler.HandleAsync("in", CancellationToken.None);

        result.Success.Should().BeTrue();
        duringExecute.Should().NotBeNull();
        duringExecute.Should().Contain("product.read");
        duringExecute.Should().Contain("internal.example.session.end");
        mutable.Scopes.Should().Equal(new[] { "product.read" });
        mutable.Scopes.Should().NotContain("internal.example.session.end");
    }

    [Fact]
    public async Task ElevateScopes_NonMutable_Forbidden()
    {
        var request = new TestRequestContext
        {
            TraceId = "trace-nonmutable",
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        var handler = new ElevatingHandlerFixture(
            new HandlerContext<ElevatingHandlerFixture>(
                request,
                NullLogger<ElevatingHandlerFixture>.Instance),
            (_, _) => new ValueTask<D2Result<string?>>(D2Result<string?>.Ok("ok")),
            new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(StringComparer.Ordinal) { "product.read" }),
                ElevateScopes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "internal.example.session.end",
                },
            });

        var result = await handler.HandleAsync("in", CancellationToken.None);

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ElevateScopes_WithNoScopeRequirement_Forbidden()
    {
        // A ScopeRequirement holding no scopes cannot be constructed, so a null
        // requirement is the only unpaired case the pipeline can meet.
        var mutable = new MutableRequestContext
        {
            TraceId = "trace-unpaired-elevate",
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        var handler = new ElevatingHandlerFixture(
            new HandlerContext<ElevatingHandlerFixture>(
                mutable,
                NullLogger<ElevatingHandlerFixture>.Instance),
            (_, _) => new ValueTask<D2Result<string?>>(D2Result<string?>.Ok("ok")),
            new HandlerOptions
            {
                ScopeRequirement = null,
                ElevateScopes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "internal.example.session.end",
                },
            });

        var result = await handler.HandleAsync("in", CancellationToken.None);

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
        mutable.Scopes.Should().Equal(new[] { "product.read" });
    }

    [Fact]
    public async Task ElevateScopes_NamingTheHandlersOwnRequiredScope_StillForbidsTheCaller()
    {
        // The scope gate reads the ambient set BEFORE the elevation union is
        // pushed. Were the two evaluated the other way round, a handler whose
        // ElevateScopes lists its own required scope would admit every caller.
        var mutable = new MutableRequestContext
        {
            TraceId = "trace-elevate-cannot-satisfy-require",
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        var handler = new ElevatingHandlerFixture(
            new HandlerContext<ElevatingHandlerFixture>(
                mutable,
                NullLogger<ElevatingHandlerFixture>.Instance),
            (_, _) => new ValueTask<D2Result<string?>>(D2Result<string?>.Ok("ok")),
            new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        "internal.example.session.end",
                    }),
                ElevateScopes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "internal.example.session.end",
                },
            });

        var result = await handler.HandleAsync("in", CancellationToken.None);

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
        mutable.Scopes.Should().Equal(new[] { "product.read" });
    }

    [Fact]
    public async Task ElevateScopes_ThrowInExecute_StillRestores()
    {
        var mutable = new MutableRequestContext
        {
            TraceId = "trace-throw",
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "product.read" },
        };

        var handler = new ElevatingHandlerFixture(
            new HandlerContext<ElevatingHandlerFixture>(
                mutable,
                NullLogger<ElevatingHandlerFixture>.Instance),
            (_, _) => throw new InvalidOperationException("handler boom"),
            new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(StringComparer.Ordinal) { "product.read" }),
                ElevateScopes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "internal.example.session.end",
                },
            });

        var result = await handler.HandleAsync("in", CancellationToken.None);

        result.Success.Should().BeFalse();
        mutable.Scopes.Should().Equal(new[] { "product.read" });
        mutable.Scopes.Should().NotContain("internal.example.session.end");
    }

    private sealed class ElevatingHandlerFixture
        : BaseHandler<ElevatingHandlerFixture, string, string>
    {
        private readonly Func<string, CancellationToken, ValueTask<D2Result<string?>>> r_body;
        private readonly HandlerOptions r_options;
        private int _executeCallCount;

        public ElevatingHandlerFixture(
            HandlerContext<ElevatingHandlerFixture> ctx,
            Func<string, CancellationToken, ValueTask<D2Result<string?>>> body,
            HandlerOptions options)
            : base(ctx)
        {
            r_body = body;
            r_options = options;
        }

        public int ExecuteCallCount => _executeCallCount;

        protected override HandlerOptions DefaultOptions => r_options;

        protected override ValueTask<D2Result<string?>> ExecuteAsync(
            string input,
            CancellationToken ct)
        {
            Interlocked.Increment(ref _executeCallCount);
            return r_body(input, ct);
        }
    }
}
