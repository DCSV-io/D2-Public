// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameConstraints.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

/// <summary>
/// Frame-level numeric constraints parsed from the <c>constraints</c> block
/// of the spec.
/// </summary>
/// <param name="MinKidLength">Minimum allowed kid length in UTF-8 bytes.</param>
/// <param name="MaxKidLength">Maximum allowed kid length in UTF-8 bytes.</param>
/// <param name="NonceLength">AES-GCM nonce length in bytes (always 12).</param>
/// <param name="TagLength">AES-GCM authentication tag length in bytes (always 16).</param>
/// <param name="MinFrameSize">
/// Fixed-field floor in bytes: VERSION, KID_LENGTH, NONCE and the tag. The KID is excluded;
/// its length is checked separately.
/// </param>
internal sealed record EncryptionFrameConstraints(
    int MinKidLength,
    int MaxKidLength,
    int NonceLength,
    int TagLength,
    int MinFrameSize);
