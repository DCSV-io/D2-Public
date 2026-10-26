// -----------------------------------------------------------------------
// <copyright file="ActionSensitivity.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// How dangerous an action is if it succeeds, used to drive audit verbosity,
/// step-up triggers and alerting thresholds. It is declared on the operation and
/// reaches the runtime as endpoint metadata. A scope has no sensitivity of
/// its own, because one scope can be granted to several operations and a scope
/// entry declaring one would be a second statement of what the operation
/// already says.
/// </summary>
/// <remarks>
/// <para>
/// This is <b>distinct from</b> the rate-limit tier, an endpoint-level concern
/// whose enum is declared outside this package, not here in
/// <c>auth-abstractions</c>. Action sensitivity captures the security /
/// blast-radius dimension; rate limit captures the resource-cost /
/// abuse-surface dimension. The two are orthogonal: a sign-in endpoint is
/// <see cref="Routine"/> in sensitivity (just an authentication attempt -
/// nothing sensitive happens unless it succeeds and creates a session) but
/// tightly capped on rate limit (brute-force surface). An admin destructive
/// endpoint is <see cref="Critical"/> in sensitivity but standard on rate limit
/// (low call volume).
/// </para>
/// <para>
/// Sensitivity is informational metadata - it drives audit verbosity and
/// risk-score thresholds, not runtime impersonation gating. Impersonation
/// gating is per scope (the <c>impersonationBlocked</c> field in
/// <c>contracts/auth-scopes/scopes.spec.json</c>) and is enforced at token mint
/// time by stripping blocked scopes from impersonation tokens. There
/// is no <c>[ImpersonationAllowed]</c> attribute; to make a Sensitive scope
/// available during impersonation, set
/// <c>impersonationBlocked: false</c> on that scope in the spec.
/// </para>
/// </remarks>
public enum ActionSensitivity
{
    /// <summary>
    /// Normal action with no special handling. Standard audit; risk-score
    /// thresholds at platform defaults (step-up at 50, block at 80). Most read
    /// operations and routine writes (e.g., updating display name).
    /// </summary>
    Routine,

    /// <summary>
    /// Action affects account integrity, modifies auth-relevant state, or
    /// exposes PII beyond the user's own basic profile. Higher audit verbosity;
    /// tighter risk thresholds (step-up at 30, block at 60). Examples: change
    /// email, change password, view billing history, set up MFA.
    /// </summary>
    Sensitive,

    /// <summary>
    /// Irreversible, financial, or admin-org-only action. Maximum audit
    /// verbosity; strictest risk thresholds (step-up at 20, block at 40).
    /// Examples: charge a payment, delete an org, ban a user, initiate
    /// force-impersonation.
    /// </summary>
    Critical,
}
