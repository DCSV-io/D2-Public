// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <c>MqGenerator</c>.
/// This class is separate from <c>DiagnosticDescriptors</c> so that code
/// running outside the Roslyn host, such as the loaders and the emitter, can
/// reference the IDs without referencing <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates its schema shape.</summary>
    public const string MalformedSpec = "D2MQ001";

    /// <summary>Required string field on a spec entry is missing or non-string.</summary>
    public const string MissingRequiredField = "D2MQ002";

    /// <summary>Two entries share the same constant name within one spec file.</summary>
    public const string DuplicateConstant = "D2MQ003";

    /// <summary>
    /// Encryption value is neither <c>"plaintext"</c> nor a recognized
    /// <c>EncryptionDomains</c> constant value.
    /// </summary>
    public const string UnknownEncryption = "D2MQ004";

    /// <summary>
    /// Message entry has <c>encryption == "plaintext"</c> but no
    /// <c>encryptionReason</c> set (or set to whitespace / too short).
    /// </summary>
    public const string MissingPlaintextReason = "D2MQ005";

    /// <summary>
    /// <c>pattern</c> on a subscription is not one of the recognized
    /// <c>QueuePattern</c> values.
    /// </summary>
    public const string UnknownPattern = "D2MQ006";

    /// <summary>
    /// Subscription's <c>messageType</c> does not match any registered
    /// message entry's <c>messageType</c>.
    /// </summary>
    public const string UnknownMessageType = "D2MQ007";

    /// <summary>
    /// <c>exchangeType</c> on a message entry is not one of the recognized
    /// values (<c>fanout</c> / <c>topic</c> / <c>direct</c>).
    /// </summary>
    public const string UnknownExchangeType = "D2MQ008";

    /// <summary>No mq-messages.spec.json file found in <c>AdditionalFiles</c>
    /// for the target assembly.</summary>
    public const string MissingMessagesSpecFile = "D2MQ009";

    /// <summary>No mq-subscriptions.spec.json file found in
    /// <c>AdditionalFiles</c> for the target assembly.</summary>
    public const string MissingSubscriptionsSpecFile = "D2MQ010";

    /// <summary>
    /// <c>constant</c> on a spec entry is not a valid C# PascalCase
    /// identifier (must start with an uppercase letter, contain only ASCII
    /// letters and digits, no separators).
    /// </summary>
    public const string InvalidConstantName = "D2MQ011";

    /// <summary>
    /// Tiered-retry tier string fails TimeSpan parse (must be HH:MM:SS).
    /// </summary>
    public const string InvalidTieredRetryDuration = "D2MQ012";

    /// <summary>
    /// The same <c>constant</c> is declared in two spec files of one catalog.
    /// Which file's entry would win depends on where the repository sits on
    /// disk, so the union is not merged and neither entry is emitted.
    /// </summary>
    public const string CrossFileDuplicateConstant = "D2MQ013";

    /// <summary>
    /// The <c>D2MqExtensionNamespace</c> property is not a dot-separated
    /// sequence of C# identifiers.
    /// </summary>
    public const string InvalidExtensionNamespace = "D2MQ014";

    /// <summary>
    /// The <c>D2MqExtensionNamespace</c> property is set and the
    /// <c>D2MqExtensionClassPrefix</c> property is absent or not a C# identifier.
    /// </summary>
    public const string InvalidExtensionClassPrefix = "D2MQ015";

    /// <summary>
    /// The <c>D2MqExtensionClassPrefix</c> property is set and the
    /// <c>D2MqExtensionNamespace</c> property is not.
    /// </summary>
    public const string ExtensionClassPrefixWithoutNamespace = "D2MQ016";
}
