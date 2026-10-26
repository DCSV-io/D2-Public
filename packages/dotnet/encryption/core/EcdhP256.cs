// -----------------------------------------------------------------------
// <copyright file="EcdhP256.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;
using JetBrains.Annotations;

/// <summary>
/// Import and validation helpers for the P-256 ECDH keys the sealed encryption
/// mode uses. All imports are strict: trailing bytes after the DER structure,
/// a non-256-bit curve, or key material that cannot complete a P-256
/// agreement are rejected with <see cref="CryptographicException"/>. The keyring
/// constructors convert it to <see cref="ArgumentException"/>, and the opener
/// converts it to <see cref="FrameMalformedException"/> for frame-borne keys.
/// </summary>
internal static class EcdhP256
{
    /// <summary>P-256 key size in bits.</summary>
    internal const int KEY_SIZE_BITS = 256;

    /// <summary>
    /// Imports a SubjectPublicKeyInfo public key, requiring a 256-bit curve.
    /// </summary>
    /// <param name="spki">SubjectPublicKeyInfo DER bytes.</param>
    /// <returns>The imported key. Caller disposes.</returns>
    [MustDisposeResource]
    internal static ECDiffieHellman ImportPublicP256(ReadOnlySpan<byte> spki)
    {
        var ecdh = ECDiffieHellman.Create();

        try
        {
            try
            {
                ecdh.ImportSubjectPublicKeyInfo(spki, out var bytesRead);

                if (bytesRead != spki.Length)
                {
                    throw new CryptographicException(
                        "Trailing bytes after the SubjectPublicKeyInfo structure.");
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                // Windows CNG raises PlatformNotSupportedException for a
                // DER-valid SPKI whose point is invalid for the declared
                // curve. Normalize: this is invalid key material and not a
                // missing platform capability.
                throw new CryptographicException(
                    "Key material is not a valid P-256 public key.", ex);
            }

            if (ecdh.KeySize != KEY_SIZE_BITS)
            {
                throw new CryptographicException(
                    $"Key size {ecdh.KeySize} is not the required {KEY_SIZE_BITS} (P-256).");
            }

            return ecdh;
        }
        catch
        {
            ecdh.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Imports a PKCS#8 private key, requiring a 256-bit curve.
    /// </summary>
    /// <param name="pkcs8">PKCS#8 PrivateKeyInfo DER bytes.</param>
    /// <returns>The imported key. Caller disposes.</returns>
    [MustDisposeResource]
    internal static ECDiffieHellman ImportPrivatePkcs8P256(ReadOnlySpan<byte> pkcs8)
    {
        var ecdh = ECDiffieHellman.Create();

        try
        {
            try
            {
                ecdh.ImportPkcs8PrivateKey(pkcs8, out var bytesRead);

                if (bytesRead != pkcs8.Length)
                {
                    throw new CryptographicException(
                        "Trailing bytes after the PKCS#8 PrivateKeyInfo structure.");
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                // See ImportPublicP256. Convert the CNG invalid-material
                // failure to a CryptographicException.
                throw new CryptographicException(
                    "Key material is not a valid P-256 private key.", ex);
            }

            if (ecdh.KeySize != KEY_SIZE_BITS)
            {
                throw new CryptographicException(
                    $"Key size {ecdh.KeySize} is not the required {KEY_SIZE_BITS} (P-256).");
            }

            return ecdh;
        }
        catch
        {
            ecdh.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Checks that the key can complete a real P-256 ECDH agreement. This
    /// functional check catches non-P-256 curves of the same size and invalid
    /// points that survive structural import. It runs at construction time only,
    /// because each probe costs a throwaway keygen and one agreement; the
    /// per-message paths use the real derivation as their probe.
    /// </summary>
    /// <param name="key">The imported key to probe.</param>
    internal static void ProbeP256Agreement(ECDiffieHellman key)
    {
        using var probe = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[]? sharedSecret = null;

        try
        {
            using var publicPart = key.PublicKey;
            sharedSecret = probe.DeriveRawSecretAgreement(publicPart);
        }
        catch (ArgumentException ex)
        {
            throw new CryptographicException(
                "Key cannot complete a P-256 ECDH agreement (wrong curve).", ex);
        }
        finally
        {
            if (sharedSecret is not null)
                CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }
}
