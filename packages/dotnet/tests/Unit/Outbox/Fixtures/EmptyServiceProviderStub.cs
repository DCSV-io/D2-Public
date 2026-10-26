// -----------------------------------------------------------------------
// <copyright file="EmptyServiceProviderStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A provider that resolves nothing, for the tests that drive one tick's work
/// directly and so never open a work scope. A test that does resolve through it
/// fails loudly rather than silently taking a fabricated seam.
/// </summary>
internal sealed class EmptyServiceProviderStub : IServiceProvider
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType) => null;
}
