// -----------------------------------------------------------------------
// <copyright file="EncryptionSourceStartupCheckLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for <see cref="EncryptionSourceStartupCheck"/>.
/// The delegates emit only the domain name and the source classification. They
/// never emit key material and never take an <see cref="System.Exception"/>
/// parameter, because its message can contain key material or caller input.
/// </summary>
internal static partial class EncryptionSourceStartupCheckLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "EncryptionSourceStartupCheck registered but no encryption domains " +
            "were registered via AddD2EncryptionFor.")]
    public static partial void NoDomainsRegistered(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Encryption domain {ServiceKey} is backed by a static key source " +
            "({Source}). Allowed only because the host is in the Development " +
            "environment - this configuration would crash a non-Development host.")]
    public static partial void StaticSourceInDevelopment(
        ILogger logger, string serviceKey, EncryptionKeyringSource source);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Encryption domain {ServiceKey} passed the source-provenance check " +
            "(managed source).")]
    public static partial void SourceCheckPassed(ILogger logger, string serviceKey);
}
