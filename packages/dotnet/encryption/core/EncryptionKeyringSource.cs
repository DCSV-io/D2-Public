// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyringSource.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Provenance of the keyring backing a registered <see cref="IPayloadCrypto"/>.
/// The zero value is the fail-closed deny default: a registration whose
/// source cannot be determined (no marker, or the enum default) is treated as
/// <see cref="StaticFactory"/>, so a hand-wired static key never passes
/// <see cref="EncryptionSourceStartupCheck"/> in a non-Development host.
/// </summary>
public enum EncryptionKeyringSource
{
    /// <summary>
    /// A hand-supplied key factory (the raw <c>AddD2EncryptionFor</c> seam).
    /// Rejected outside a Development host. This is the source the startup
    /// check guards against in production, and it is the zero-value fail-closed
    /// default.
    /// </summary>
    StaticFactory = 0,

    /// <summary>
    /// A managed, rotation-aware keyring. The only source
    /// <see cref="EncryptionSourceStartupCheck"/> accepts in a non-Development host.
    /// </summary>
    Managed = 1,
}
