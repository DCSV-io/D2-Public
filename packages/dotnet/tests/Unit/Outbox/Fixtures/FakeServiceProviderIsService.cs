// -----------------------------------------------------------------------
// <copyright file="FakeServiceProviderIsService.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Responds to the relay's store-presence probe with the result the test chose,
/// and records what it was asked, so a test can assert the relay checks its own
/// database's store rather than something else.
/// </summary>
/// <param name="answer">The value <c>IsService</c> returns for every service type.</param>
internal sealed class FakeServiceProviderIsService(bool answer) : IServiceProviderIsService
{
    /// <summary>Gets the service types this was asked about, in order.</summary>
    public List<Type> Asked { get; } = [];

    /// <inheritdoc/>
    public bool IsService(Type serviceType)
    {
        Asked.Add(serviceType);

        return answer;
    }
}
