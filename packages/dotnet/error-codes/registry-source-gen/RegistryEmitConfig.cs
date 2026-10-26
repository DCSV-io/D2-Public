// -----------------------------------------------------------------------
// <copyright file="RegistryEmitConfig.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

/// <summary>
/// Emit configuration for a registry host package. This package defines only
/// <see cref="Public"/>. Each value is supplied when a
/// <see cref="RegistryEmitConfig"/> instance is constructed, and the values of
/// any other instance (its message-key class and optional remap prefix) are not
/// defined in this package.
/// </summary>
/// <param name="TargetAssemblyName">
/// AssemblyName hard gate for the generator (must match the compilation's
/// assembly name).
/// </param>
/// <param name="RootNamespace">Namespace of the emitted registry types.</param>
/// <param name="GeneratorTypeName">
/// Fully-qualified generator type name embedded in the auto-generated banner.
/// </param>
/// <param name="MessageKeyClassName">
/// C# class for message keys: <c>TK</c> or another class supplied when the
/// instance is constructed.
/// Spec paths stay <c>TK.*</c>; emission remaps roots when
/// <paramref name="PrivateMessageKeyRemapFromPrefix"/> is set.
/// </param>
/// <param name="MessageKeyUsingNamespace">
/// Namespace imported for the message-key class.
/// </param>
/// <param name="PrivateMessageKeyRemapFromPrefix">
/// Optional TK path prefix whose matching DOM keys are re-rooted onto
/// <paramref name="MessageKeyClassName"/> when it is set.
/// <see langword="null"/> means no remap, as in <see cref="Public"/>.
/// </param>
internal sealed record RegistryEmitConfig(
    string TargetAssemblyName,
    string RootNamespace,
    string GeneratorTypeName,
    string MessageKeyClassName,
    string MessageKeyUsingNamespace,
    string? PrivateMessageKeyRemapFromPrefix = null)
{
    /// <summary>Gets the public registry host config (GEN and LIB catalogs).</summary>
    public static RegistryEmitConfig Public { get; } = new(
        TargetAssemblyName: "DcsvIo.D2.ErrorCodes.Registry",
        RootNamespace: "DcsvIo.D2.ErrorCodes.Registry",
        GeneratorTypeName: "DcsvIo.D2.ErrorCodes.Registry.SourceGen.RegistryGenerator",
        MessageKeyClassName: "TK",
        MessageKeyUsingNamespace: "DcsvIo.D2.I18n",
        PrivateMessageKeyRemapFromPrefix: null);
}
