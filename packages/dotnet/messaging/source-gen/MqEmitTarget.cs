// -----------------------------------------------------------------------
// <copyright file="MqEmitTarget.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

/// <summary>
/// The naming one emit target uses: which namespace the generated registry
/// types are declared in, what they are called, and which files they are
/// written to. The targets are the framework catalog in the messaging
/// abstractions assembly and an extension catalog in a host assembly that sets
/// the extension properties, whose spec set is the union of every spec file
/// that assembly lists.
/// </summary>
internal sealed class MqEmitTarget
{
    /// <summary>The namespace the descriptor records themselves live in.</summary>
    public const string DESCRIPTOR_NAMESPACE = "DcsvIo.D2.Messaging";

    private MqEmitTarget(
        string targetNamespace,
        string messagesClass,
        string messagesRegistryClass,
        string subscriptionsClass,
        string subscriptionsRegistryClass,
        string messagesHint,
        string subscriptionsHint)
    {
        TargetNamespace = targetNamespace;
        MessagesClass = messagesClass;
        MessagesRegistryClass = messagesRegistryClass;
        SubscriptionsClass = subscriptionsClass;
        SubscriptionsRegistryClass = subscriptionsRegistryClass;
        MessagesHint = messagesHint;
        SubscriptionsHint = subscriptionsHint;
    }

    /// <summary>The framework target: one spec file per catalog, emitted into
    /// the messaging abstractions assembly.</summary>
    public static MqEmitTarget Public { get; } = new(
        targetNamespace: DESCRIPTOR_NAMESPACE,
        messagesClass: "MqMessages",
        messagesRegistryClass: "MqMessagesRegistry",
        subscriptionsClass: "MqSubscriptions",
        subscriptionsRegistryClass: "MqSubscriptionsRegistry",
        messagesHint: "MqMessages.g.cs",
        subscriptionsHint: "MqSubscriptions.g.cs");

    /// <summary>Gets the namespace the emitted classes are declared in.</summary>
    public string TargetNamespace { get; }

    /// <summary>Gets the name of the message-constants class.</summary>
    public string MessagesClass { get; }

    /// <summary>Gets the name of the message-descriptor registry class.</summary>
    public string MessagesRegistryClass { get; }

    /// <summary>Gets the name of the subscription-constants class.</summary>
    public string SubscriptionsClass { get; }

    /// <summary>Gets the name of the subscription-descriptor registry class.</summary>
    public string SubscriptionsRegistryClass { get; }

    /// <summary>Gets the generated-file hint name for the messages half.</summary>
    public string MessagesHint { get; }

    /// <summary>Gets the generated-file hint name for the subscriptions half.</summary>
    public string SubscriptionsHint { get; }

    /// <summary>Gets a value indicating whether the emitted file needs a
    /// <c>using</c> for the namespace the descriptor records live in.</summary>
    public bool NeedsDescriptorUsing =>
        !string.Equals(TargetNamespace, DESCRIPTOR_NAMESPACE, System.StringComparison.Ordinal);

    /// <summary>Builds an extension target: the framework class names with
    /// <paramref name="classPrefix"/> before each, declared in
    /// <paramref name="targetNamespace"/>, each written to its class name
    /// followed by <c>.g.cs</c>.</summary>
    /// <param name="targetNamespace">The namespace the emitted classes are declared in.</param>
    /// <param name="classPrefix">The identifier placed before each class name.</param>
    /// <returns>The extension target.</returns>
    public static MqEmitTarget Extension(string targetNamespace, string classPrefix) => new(
        targetNamespace: targetNamespace,
        messagesClass: classPrefix + Public.MessagesClass,
        messagesRegistryClass: classPrefix + Public.MessagesRegistryClass,
        subscriptionsClass: classPrefix + Public.SubscriptionsClass,
        subscriptionsRegistryClass: classPrefix + Public.SubscriptionsRegistryClass,
        messagesHint: classPrefix + Public.MessagesHint,
        subscriptionsHint: classPrefix + Public.SubscriptionsHint);
}
