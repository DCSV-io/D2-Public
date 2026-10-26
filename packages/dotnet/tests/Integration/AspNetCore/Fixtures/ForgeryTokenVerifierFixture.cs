// -----------------------------------------------------------------------
// <copyright file="ForgeryTokenVerifierFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;

using System.Collections.Concurrent;
using DcsvIo.D2.AspNetCore.Forgery;
using global::Microsoft.AspNetCore.Http;

/// <summary>
/// A scripted <see cref="ID2ForgeryTokenVerifier"/> standing in for the token
/// verifier a cookie-serving host registers. Records every token it was handed
/// so a test can assert which side of the request the middleware read it from.
/// </summary>
internal sealed class ForgeryTokenVerifierFixture : ID2ForgeryTokenVerifier
{
    private readonly Func<string?, D2ForgeryTokenVerdict> r_verdict;
    private readonly Func<D2ForgeryTokenIssue?> r_issue;
    private readonly ConcurrentQueue<string?> r_presentedTokens = new();
    private int _issueCallCount;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ForgeryTokenVerifierFixture"/> class.
    /// </summary>
    /// <param name="verdict">The verdict to return for a presented token.</param>
    /// <param name="issue">The issue to return on the response path.</param>
    public ForgeryTokenVerifierFixture(
        Func<string?, D2ForgeryTokenVerdict> verdict,
        Func<D2ForgeryTokenIssue?>? issue = null)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        r_verdict = verdict;
        r_issue = issue ?? (static () => null);
    }

    /// <summary>Gets every token value handed to <see cref="Verify"/>, in order.</summary>
    public IReadOnlyCollection<string?> PresentedTokens => [.. r_presentedTokens];

    /// <summary>Gets how many times the response path asked for an issue.</summary>
    public int IssueCallCount => Volatile.Read(ref _issueCallCount);

    /// <inheritdoc />
    public D2ForgeryTokenVerdict Verify(HttpContext context, string? presentedToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        r_presentedTokens.Enqueue(presentedToken);

        return r_verdict(presentedToken);
    }

    /// <inheritdoc />
    public D2ForgeryTokenIssue? IssueForResolvedSession(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Interlocked.Increment(ref _issueCallCount);

        return r_issue();
    }
}
