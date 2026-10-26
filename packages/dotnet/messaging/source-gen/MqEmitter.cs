// -----------------------------------------------------------------------
// <copyright file="MqEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the messaging spec output: the <c>MqMessages</c>
/// and <c>MqSubscriptions</c> static classes (string constants) and a runtime
/// registry pair (<c>MqMessagesRegistry</c> and <c>MqSubscriptionsRegistry</c>)
/// that maps each constant to its full descriptor record. The emitter is
/// stateless and has no dependency on the Roslyn host.
/// </summary>
internal static class MqEmitter
{
    private const string _PATTERNS_FQN = "DcsvIo.D2.Messaging.QueuePattern";
    private const string _PLAINTEXT_LITERAL = "plaintext";

    // Closed vocabulary, declared inline instead of read from the enums of a
    // downstream assembly, so its behavior can be pinned without a reference
    // to that assembly.
    private static readonly HashSet<string> sr_exchangeTypes =
        new(StringComparer.Ordinal) { "fanout", "topic", "direct" };

    private static readonly HashSet<string> sr_patterns = new(StringComparer.Ordinal)
    {
        "CompetingConsumer",
        "FanoutExclusiveAutoDelete",
        "DurableShared",
    };

    /// <summary>
    /// Emits the full messaging spec output. Returns one result for the
    /// messages file (<c>MqMessages.g.cs</c> for the public target) and one for
    /// the subscriptions file (<c>MqSubscriptions.g.cs</c> for the public
    /// target). Both files are always emitted, even when a spec has no
    /// entries.
    /// </summary>
    /// <param name="messages">Parsed messages spec.</param>
    /// <param name="subscriptions">Parsed subscriptions spec.</param>
    /// <param name="encryptionDomains">EncryptionDomains constant values
    /// (e.g. ["payload-fixture-sealed"]) extracted from the
    /// compilation symbol. The emitter validates each message's encryption
    /// value against this set + the literal "plaintext".</param>
    /// <param name="target">Which set of type names and which namespace the
    /// output carries.</param>
    /// <returns>The messages and subscriptions emit results, each with its
    /// own diagnostics.</returns>
    public static (EmitResult Messages, EmitResult Subscriptions) Emit(
        MqMessagesSpec messages,
        MqSubscriptionsSpec subscriptions,
        IReadOnlyList<string> encryptionDomains,
        MqEmitTarget target)
    {
        var msgDiags = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var subDiags = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        var validMessages = ValidateMessages(messages, encryptionDomains, msgDiags);
        var validSubs = ValidateSubscriptions(subscriptions, validMessages, subDiags);

        var messagesSrc = EmitMessagesClass(validMessages, target);
        var subsSrc = EmitSubscriptionsClass(validSubs, target);

        return (
            new EmitResult(target.MessagesHint, messagesSrc, msgDiags.ToImmutable()),
            new EmitResult(target.SubscriptionsHint, subsSrc, subDiags.ToImmutable()));
    }

    private static List<MqMessageEntry> ValidateMessages(
        MqMessagesSpec spec,
        IReadOnlyList<string> encryptionDomains,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validEncryption = new HashSet<string>(encryptionDomains, StringComparer.Ordinal)
        {
            _PLAINTEXT_LITERAL,
        };
        var valid = new List<MqMessageEntry>();

        foreach (var entry in spec.Messages)
        {
            if (!ValidateConstant(entry.Constant, out var constReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstantName(
                    "mq-messages.spec.json", entry.Constant, constReason));
                continue;
            }

            if (!seen.Add(entry.Constant))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstant(
                    "mq-messages.spec.json", entry.Constant));
                continue;
            }

            if (!sr_exchangeTypes.Contains(entry.ExchangeType))
            {
                diagnostics.Add(EmitDiagnostics.UnknownExchangeType(
                    entry.Constant, entry.ExchangeType));
                continue;
            }

            if (!validEncryption.Contains(entry.Encryption))
            {
                var validList = string.Join(", ",
                    encryptionDomains.OrderBy(s => s, StringComparer.Ordinal));
                diagnostics.Add(EmitDiagnostics.UnknownEncryption(
                    entry.Constant, entry.Encryption, validList));
                continue;
            }

            if (string.Equals(entry.Encryption, _PLAINTEXT_LITERAL, StringComparison.Ordinal))
            {
                if (entry.EncryptionReason.Falsey()
                    || (entry.EncryptionReason?.Trim().Length ?? 0) < 10)
                {
                    diagnostics.Add(EmitDiagnostics.MissingPlaintextReason(entry.Constant));
                    continue;
                }
            }

            valid.Add(entry);
        }

        return valid;
    }

    private static List<MqSubscriptionEntry> ValidateSubscriptions(
        MqSubscriptionsSpec spec,
        List<MqMessageEntry> validMessages,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var knownMessageTypes = new HashSet<string>(
            validMessages.Select(m => m.MessageType), StringComparer.Ordinal);
        var valid = new List<MqSubscriptionEntry>();

        foreach (var entry in spec.Subscriptions)
        {
            if (!ValidateConstant(entry.Constant, out var constReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstantName(
                    "mq-subscriptions.spec.json", entry.Constant, constReason));
                continue;
            }

            if (!seen.Add(entry.Constant))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstant(
                    "mq-subscriptions.spec.json", entry.Constant));
                continue;
            }

            if (!sr_patterns.Contains(entry.Pattern))
            {
                var validList = string.Join(", ",
                    sr_patterns.OrderBy(s => s, StringComparer.Ordinal));
                diagnostics.Add(EmitDiagnostics.UnknownPattern(
                    entry.Constant, entry.Pattern, validList));
                continue;
            }

            if (!knownMessageTypes.Contains(entry.MessageType))
            {
                diagnostics.Add(EmitDiagnostics.UnknownMessageType(
                    entry.Constant, entry.MessageType));
                continue;
            }

            if (entry.TieredRetry is { } retry)
            {
                var anyBadTier = false;
                foreach (var tier in retry.Tiers)
                {
                    if (!TimeSpan.TryParseExact(
                        tier, @"hh\:mm\:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _))
                    {
                        diagnostics.Add(EmitDiagnostics.InvalidTieredRetryDuration(
                            entry.Constant, tier));
                        anyBadTier = true;
                    }
                }

                if (anyBadTier) continue;
            }

            valid.Add(entry);
        }

        return valid;
    }

    private static bool ValidateConstant(string value, out string reason)
    {
        if (value.Falsey())
        {
            reason = "empty";
            return false;
        }

        if (!char.IsUpper(value[0]) || !char.IsLetter(value[0]))
        {
            reason = "must start with an uppercase ASCII letter";
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                reason = $"invalid character '{ch}'";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string EmitMessagesClass(List<MqMessageEntry> messages, MqEmitTarget target)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");

        if (target.NeedsDescriptorUsing)
            sb.AppendLine($"using {MqEmitTarget.DESCRIPTOR_NAMESPACE};");

        sb.AppendLine();
        sb.AppendLine($"namespace {target.TargetNamespace};");
        sb.AppendLine();

        // Constants class, referenced as [MqPub(<constants class>.X)].
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Codegen'd string constants for every message type registered in");
        sb.AppendLine("/// <c>contracts/mq-messages/mq-messages.spec.json</c>.");
        sb.AppendLine(
            $"/// Apply via <c>[MqPub({target.MessagesClass}.X)]</c> on the message type.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {target.MessagesClass}");
        sb.AppendLine("{");
        foreach (var m in messages)
        {
            sb.AppendLine($"    /// <summary>Publisher contract for {m.MessageType}.</summary>");
            sb.AppendLine($"    public const string {m.Constant} = \"{m.Constant}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Registry that the runtime catalog looks up by constant.
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            $"/// Runtime registry mapping every <see cref=\"{target.MessagesClass}\"/> "
            + "constant to its");
        sb.AppendLine(
            "/// fully-resolved <see cref=\"MqMessageDescriptor\"/> (exchange, encryption,");
        sb.AppendLine("/// default routing key, message type FQN). Looked up by");
        sb.AppendLine("/// <c>MessagingCatalog.RequireDescriptor</c> via the constant on a");
        sb.AppendLine("/// type's <c>[MqPub]</c> attribute.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {target.MessagesRegistryClass}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Lookup table: constant -> descriptor.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyDictionary<string, MqMessageDescriptor> "
            + "ByConstant = new Dictionary<string, MqMessageDescriptor>("
            + "System.StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var m in messages)
        {
            var encReason = m.EncryptionReason is null
                ? "null"
                : "\"" + m.EncryptionReason.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var defaultKey = m.DefaultRoutingKey is null
                ? "null"
                : "\"" + m.DefaultRoutingKey.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            sb.AppendLine(
                $"        [\"{m.Constant}\"] = new MqMessageDescriptor(");
            sb.AppendLine($"            Constant: \"{m.Constant}\",");
            sb.AppendLine($"            MessageTypeName: \"{m.MessageType}\",");
            sb.AppendLine($"            Exchange: \"{m.Exchange}\",");
            sb.AppendLine($"            ExchangeType: \"{m.ExchangeType}\",");
            sb.AppendLine($"            Encryption: \"{m.Encryption}\",");
            sb.AppendLine($"            EncryptionReason: {encReason},");
            sb.AppendLine($"            DefaultRoutingKey: {defaultKey}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string EmitSubscriptionsClass(
        List<MqSubscriptionEntry> subscriptions, MqEmitTarget target)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");

        if (target.NeedsDescriptorUsing)
            sb.AppendLine($"using {MqEmitTarget.DESCRIPTOR_NAMESPACE};");

        sb.AppendLine();
        sb.AppendLine($"namespace {target.TargetNamespace};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Codegen'd string constants for every subscription registered in");
        sb.AppendLine(
            "/// <c>contracts/mq-subscriptions/mq-subscriptions.spec.json</c>.");
        sb.AppendLine(
            $"/// Apply via <c>[MqSub({target.SubscriptionsClass}.X)]</c> on a handler class.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {target.SubscriptionsClass}");
        sb.AppendLine("{");
        foreach (var s in subscriptions)
        {
            sb.AppendLine(
                $"    /// <summary>Subscription consuming {s.MessageType} via "
                + $"queue '{s.QueueName}' (pattern: {s.Pattern}).</summary>");
            sb.AppendLine($"    public const string {s.Constant} = \"{s.Constant}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            $"/// Runtime registry mapping every <see cref=\"{target.SubscriptionsClass}\"/> "
            + "constant");
        sb.AppendLine(
            "/// to its fully-resolved <see cref=\"MqSubscriptionDescriptor\"/>.");
        sb.AppendLine(
            "/// Used by the assembly-scan registration to build per-subscriber DI entries.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {target.SubscriptionsRegistryClass}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Lookup table: constant -> descriptor.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyDictionary<string, MqSubscriptionDescriptor> "
            + "ByConstant = new Dictionary<string, MqSubscriptionDescriptor>("
            + "StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var s in subscriptions)
        {
            var binding = s.RoutingKeyBinding is null
                ? "\"\""
                : "\"" + s.RoutingKeyBinding.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var prefetch = s.Prefetch is { } p ? p.ToString() : "10";
            var idem = s.Idempotency is true ? "true" : "false";
            string retry;
            if (s.TieredRetry is { } r)
            {
                var tiers = string.Join(
                    ", ",
                    r.Tiers.Select(t => $"TimeSpan.Parse(\"{t}\")"));
                retry =
                    $"new TieredRetryDescriptor(Tiers: new[] {{ {tiers} }}, "
                    + $"MaxAttempts: {r.MaxAttempts})";
            }
            else
            {
                retry = "null";
            }

            sb.AppendLine($"        [\"{s.Constant}\"] = new MqSubscriptionDescriptor(");
            sb.AppendLine($"            Constant: \"{s.Constant}\",");
            sb.AppendLine($"            MessageTypeName: \"{s.MessageType}\",");
            sb.AppendLine($"            QueueName: \"{s.QueueName}\",");
            sb.AppendLine($"            Pattern: {_PATTERNS_FQN}.{s.Pattern},");
            sb.AppendLine($"            RoutingKeyBinding: {binding},");
            sb.AppendLine($"            Prefetch: {prefetch},");
            sb.AppendLine($"            Idempotency: {idem},");
            sb.AppendLine($"            TieredRetry: {retry}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine(
            "//   Generated by DcsvIo.D2.Messaging.SourceGen from contracts/mq-messages/");
        sb.AppendLine(
            "//   mq-messages.spec.json + contracts/mq-subscriptions/mq-subscriptions.spec.json.");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
    }
}
