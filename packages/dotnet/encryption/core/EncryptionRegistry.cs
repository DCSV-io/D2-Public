// -----------------------------------------------------------------------
// <copyright file="EncryptionRegistry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Linq;

/// <summary>
/// Aggregates every <see cref="EncryptionRegistration"/> into a single
/// queryable registry. Public only so DI can resolve it; it is not meant to
/// be constructed directly.
/// </summary>
public sealed class EncryptionRegistry
{
    /// <summary>Initializes a new <see cref="EncryptionRegistry"/>.</summary>
    /// <param name="registrations">
    /// All registrations made via
    /// <see cref="EncryptionServiceCollectionExtensions.AddD2EncryptionFor"/>.
    /// </param>
    public EncryptionRegistry(IEnumerable<EncryptionRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ServiceKeys = registrations
            .Select(r => r.ServiceKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets every service key that has been registered.</summary>
    public IReadOnlyList<string> ServiceKeys { get; }
}
