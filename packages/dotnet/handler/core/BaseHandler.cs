// -----------------------------------------------------------------------
// <copyright file="BaseHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Result;

/// <summary>
/// Abstract base for every handler in the platform - CQRS handlers, repo
/// handlers, messaging consumers, scheduled jobs and any other
/// handler-shaped operation. Subclasses implement <see cref="ExecuteAsync"/>
/// with the actual logic; the sealed observability pipeline in
/// <see cref="RunCorePipelineAsync"/> provides everything else (scope
/// pre-check, activity span, log scope, stopwatch, metrics, universal
/// try/catch).
/// </summary>
/// <typeparam name="TSelf">
/// CRTP self-type - used for the <see cref="HandlerContext{T}"/> typed logger
/// + the <see cref="HandlerTelemetry"/> tag value.
/// </typeparam>
/// <typeparam name="TInput">
/// The handler input type. Constrained to <see langword="notnull"/> -
/// optional or absent values live on members of the input, never as a null
/// whole-input argument.
/// </typeparam>
/// <typeparam name="TOutput">The handler output type.</typeparam>
public abstract class BaseHandler<TSelf, TInput, TOutput> : IHandler<TInput, TOutput>
    where TSelf : BaseHandler<TSelf, TInput, TOutput>
    where TInput : notnull
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BaseHandler{TSelf, TInput, TOutput}"/> class.
    /// </summary>
    /// <param name="context">
    /// The handler context (request + logger) - DI-resolved per-request.
    /// </param>
    protected BaseHandler(HandlerContext<TSelf> context)
    {
        Context = context;
    }

    /// <summary>Gets the per-request handler context (request + logger).</summary>
    protected IHandlerContext Context { get; }

    /// <summary>
    /// Gets the per-handler default options. Override to set handler-specific
    /// defaults (e.g. <c>LogInput = false</c> for handlers whose inputs
    /// contain PII that <c>[RedactData]</c> does not support).
    /// </summary>
    protected virtual HandlerOptions DefaultOptions => new();

    /// <inheritdoc/>
    public virtual async ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        var (result, _) = await RunCorePipelineAsync(input, ct, options);
        return result;
    }

    /// <summary>
    /// The observability + safety pipeline. Pre-checks required scopes,
    /// starts an <see cref="System.Diagnostics.Activity"/>, opens a log scope,
    /// records duration, increments counters, and runs <see cref="ExecuteAsync"/>
    /// inside a universal try/catch.
    /// </summary>
    /// <remarks>
    /// Non-virtual by design - subclasses cannot change the pipeline.
    /// Returns the captured <see cref="Exception"/> alongside the result, so
    /// an overriding <c>HandleAsync</c> can map typed EF and PG exceptions to
    /// <see cref="D2Result"/> failure codes.
    /// </remarks>
    /// <param name="input">The handler input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="options">
    /// Per-call options; null falls through to <see cref="DefaultOptions"/>.
    /// </param>
    /// <returns>
    /// The result and the captured exception (null on success and handled-failure paths).
    /// </returns>
    protected async ValueTask<(D2Result<TOutput?> Result, Exception? CapturedException)>
        RunCorePipelineAsync(
            TInput input,
            CancellationToken ct,
            HandlerOptions? options)
    {
        var resolved = options ?? DefaultOptions;
        var traceId = Context.Request.TraceId;
        var handlerName = typeof(TSelf).Name;
        var handlerTag = new KeyValuePair<string, object?>(
            HandlerTelemetry.Tags.HANDLER_NAME, handlerName);

        // The object cast erases the notnull annotation, so the runtime null
        // check stays reachable for the null! a caller can still pass.
        if ((object?)input is null)
        {
            HandlerTelemetry.SR_Invoked.Add(1, handlerTag);
            HandlerTelemetry.SR_Failed.Add(1, handlerTag);
            BaseHandlerLog.HandlerNullInput(Context.Logger, handlerName);
            return (D2Result<TOutput?>.ValidationFailed(traceId: traceId), null);
        }

        var elevates = resolved.ElevateScopes is { Count: > 0 };

        // Elevation without a scope requirement would grant its scopes to
        // whatever caller reached the handler, so the pairing is refused rather
        // than documented.
        if (elevates && resolved.ScopeRequirement is not { Scopes.Count: > 0 })
        {
            HandlerTelemetry.SR_Invoked.Add(1, handlerTag);
            HandlerTelemetry.SR_Failed.Add(1, handlerTag);
            BaseHandlerLog.HandlerElevateWithoutScopeRequirement(Context.Logger, handlerName);
            return (D2Result<TOutput?>.Forbidden(traceId: traceId), null);
        }

        if (resolved.ScopeRequirement is { Scopes.Count: > 0 } req)
        {
            var ok = req.Match == HandlerScopeMatch.All
                ? HandlerHasAllScopes(Context.Request.Scopes, req.Scopes)
                : HandlerHasAnyScope(Context.Request.Scopes, req.Scopes);

            if (!ok)
            {
                HandlerTelemetry.SR_Invoked.Add(1, handlerTag);
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);
                return (D2Result<TOutput?>.Forbidden(traceId: traceId), null);
            }
        }

        IDisposable? elevateLease = null;

        try
        {
            if (resolved.ElevateScopes is { Count: > 0 } elevateScopes)
            {
                if (Context.Request is not MutableRequestContext mutable)
                {
                    HandlerTelemetry.SR_Invoked.Add(1, handlerTag);
                    HandlerTelemetry.SR_Failed.Add(1, handlerTag);
                    BaseHandlerLog.HandlerElevateContextNotMutable(Context.Logger, handlerName);
                    return (D2Result<TOutput?>.Forbidden(traceId: traceId), null);
                }

                var elevateArray = new string[elevateScopes.Count];
                var elevateIndex = 0;

                foreach (var scope in elevateScopes)
                    elevateArray[elevateIndex++] = scope;

                elevateLease = ScopeElevation.Push(mutable, elevateArray);
            }

            using var activity = HandlerTelemetry.SR_ActivitySource.StartActivity(handlerName);
            activity?.SetTag(HandlerTelemetry.Tags.HANDLER_NAME, handlerName);

            if (Context.Request.UserId is { } userId)
                activity?.SetTag(HandlerTelemetry.Tags.USER_ID, userId.ToString());

            if (Context.Request.OrgId is { } orgId)
                activity?.SetTag(HandlerTelemetry.Tags.ORG_ID, orgId.ToString());

            if (Context.Request.OrgType is { } orgType)
                activity?.SetTag(HandlerTelemetry.Tags.ORG_TYPE, orgType.ToString());

            if (Context.Request.OrgRole is { } orgRole)
                activity?.SetTag(HandlerTelemetry.Tags.ORG_ROLE, orgRole.ToString());

            if (Context.Request.IsImpersonating == true)
            {
                activity?.SetTag(HandlerTelemetry.Tags.IMPERSONATING, true);
                activity?.SetTag(
                    HandlerTelemetry.Tags.IMPERSONATION_KIND,
                    Context.Request.ImpersonationKind?.ToString());

                if (Context.Request.ImpersonatedBy is { } impersonatorId)
                {
                    activity?.SetTag(
                        HandlerTelemetry.Tags.IMPERSONATOR_ID,
                        impersonatorId.ToString());
                }

                if (Context.Request.ImpersonatorOrgId is { } impersonatorOrgId)
                {
                    activity?.SetTag(
                        HandlerTelemetry.Tags.IMPERSONATOR_ORG_ID,
                        impersonatorOrgId.ToString());
                }

                if (Context.Request.ImpersonatorOrgType is { } impersonatorOrgType)
                {
                    activity?.SetTag(
                        HandlerTelemetry.Tags.IMPERSONATOR_ORG_TYPE,
                        impersonatorOrgType.ToString());
                }

                if (Context.Request.ImpersonatorOrgRole is { } impersonatorOrgRole)
                {
                    activity?.SetTag(
                        HandlerTelemetry.Tags.IMPERSONATOR_ORG_ROLE,
                        impersonatorOrgRole.ToString());
                }
            }

            // Open a logger scope so every log emitted inside ExecuteAsync (and
            // every BaseHandlerLog.* call below) carries the universal correlation
            // fields - traceId, userId, orgId, orgType, orgRole, plus
            // impersonation kind when relevant. IRequestContext has no
            // separate correlationId field (a transport-middleware concern);
            // traceId is the cross-service correlator.
            var scopeFields = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [HandlerTelemetry.Tags.HANDLER_NAME] = handlerName,
            };

            if (traceId is not null)
                scopeFields[HandlerTelemetry.Tags.TRACE_ID] = traceId;

            if (Context.Request.UserId is { } scopeUserId)
                scopeFields[HandlerTelemetry.Tags.USER_ID] = scopeUserId.ToString();

            if (Context.Request.OrgId is { } scopeOrgId)
                scopeFields[HandlerTelemetry.Tags.ORG_ID] = scopeOrgId.ToString();

            if (Context.Request.OrgType is { } scopeOrgType)
                scopeFields[HandlerTelemetry.Tags.ORG_TYPE] = scopeOrgType.ToString();

            if (Context.Request.OrgRole is { } scopeOrgRole)
                scopeFields[HandlerTelemetry.Tags.ORG_ROLE] = scopeOrgRole.ToString();

            if (Context.Request.IsImpersonating == true)
            {
                scopeFields[HandlerTelemetry.Tags.IMPERSONATION_KIND] =
                    Context.Request.ImpersonationKind?.ToString();
            }

            using var logScope = Context.Logger.BeginScope(scopeFields);

            var stopwatch = Stopwatch.StartNew();
            HandlerTelemetry.SR_Invoked.Add(1, handlerTag);

            if (resolved.LogInput)
                BaseHandlerLog.HandlerInvoked(Context.Logger, handlerName, input);

            try
            {
                var result = await ExecuteAsync(input, ct);
                stopwatch.Stop();

                HandlerTelemetry.SR_Duration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    handlerTag);

                if (result.Success)
                    HandlerTelemetry.SR_Succeeded.Add(1, handlerTag);
                else
                    HandlerTelemetry.SR_Failed.Add(1, handlerTag);

                LogThresholdBreaches(resolved, stopwatch.Elapsed, handlerName);

                if (resolved.LogOutput)
                {
                    BaseHandlerLog.HandlerReturned(
                        Context.Logger,
                        handlerName,
                        result.Success ? "success" : "failure",
                        stopwatch.Elapsed.TotalMilliseconds);
                }

                // Set TraceId on every result that crosses the handler
                // boundary, so logs, responses and clients carry the
                // correlation id. A result whose TraceId the handler already
                // set keeps its value (no override).
                if (result.TraceId is null && traceId is not null)
                    result = result.WithTraceId(traceId);

                return (result, null);
            }
            catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
            {
                // The caller canceled: an intentional cancellation.
                stopwatch.Stop();
                HandlerTelemetry.SR_Duration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    handlerTag);
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);
                BaseHandlerLog.HandlerCanceled(
                    Context.Logger,
                    handlerName,
                    stopwatch.Elapsed.TotalMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, "canceled");
                return (D2Result<TOutput?>.Canceled(traceId: traceId), oce);
            }
            catch (OperationCanceledException oce)
            {
                // An OperationCanceledException raised while our token is not
                // canceled is a downstream timeout (HttpClient timeout, SQL
                // command timeout, internal handler watchdog and so on). That
                // is a dependency failure, so return ServiceUnavailable and not
                // UnhandledException, which would imply a bug in our own code.
                stopwatch.Stop();
                HandlerTelemetry.SR_Duration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    handlerTag);
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);
                BaseHandlerLog.HandlerDownstreamTimeout(
                    Context.Logger,
                    handlerName,
                    oce.GetType().Name,
                    stopwatch.Elapsed.TotalMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, "downstream timeout");
                return (D2Result<TOutput?>.ServiceUnavailable(traceId: traceId), oce);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                HandlerTelemetry.SR_Duration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    handlerTag);
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);

                // Pass the exception type name only - never `ex.Message` -
                // because Message can contain broker URIs, connection strings,
                // bearer tokens or raw user input. The SetStatus description
                // also gets the type name: it reaches OTel exporters as a
                // string tag, which is the same leak surface.
                var exceptionType = ex.GetType().Name;
                BaseHandlerLog.HandlerThrew(
                    Context.Logger,
                    handlerName,
                    exceptionType,
                    stopwatch.Elapsed.TotalMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, exceptionType);
                return (D2Result<TOutput?>.UnhandledException(traceId: traceId), ex);
            }
        }
        finally
        {
            elevateLease?.Dispose();
        }
    }

    /// <summary>
    /// Implement the handler's actual logic. Called inside the observability
    /// pipeline (see <see cref="RunCorePipelineAsync"/>); exceptions surface as
    /// <see cref="D2Result.UnhandledException"/>.
    /// </summary>
    /// <param name="input">The handler input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's result.</returns>
    protected abstract ValueTask<D2Result<TOutput?>> ExecuteAsync(
        TInput input,
        CancellationToken ct);

    private static bool HandlerHasAnyScope(
        IReadOnlySet<string> granted,
        IReadOnlySet<string> required)
    {
        foreach (var scope in required)
        {
            if (granted.Contains(scope))
                return true;
        }

        return false;
    }

    private static bool HandlerHasAllScopes(
        IReadOnlySet<string> granted,
        IReadOnlySet<string> required)
    {
        foreach (var scope in required)
        {
            if (!granted.Contains(scope))
                return false;
        }

        return true;
    }

    private void LogThresholdBreaches(HandlerOptions options, TimeSpan elapsed, string handlerName)
    {
        if (options.CriticalThreshold is { } critical && elapsed > critical)
        {
            BaseHandlerLog.HandlerCriticalThresholdExceeded(
                Context.Logger,
                handlerName,
                critical.TotalMilliseconds,
                elapsed.TotalMilliseconds);
            return;
        }

        if (options.SlowThreshold is { } slow && elapsed > slow)
        {
            BaseHandlerLog.HandlerSlowThresholdExceeded(
                Context.Logger,
                handlerName,
                slow.TotalMilliseconds,
                elapsed.TotalMilliseconds);
        }
    }
}
