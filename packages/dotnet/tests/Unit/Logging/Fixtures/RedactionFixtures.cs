// -----------------------------------------------------------------------
// <copyright file="RedactionFixtures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// Fixture types for the <c>RedactDataDestructuringPolicy</c> unit tests.
/// They are in one file because they exist only to exercise that one policy
/// class, and a separate file for each type would add noise without value. The
/// file deviates on purpose from the one-class-per-file convention.
/// </summary>
internal static class RedactionFixtures
{
    /// <summary>Struct with type-level redaction.</summary>
    [RedactData(Reason = RedactReason.SecretInformation)]
    internal readonly struct TypeLevelRedactedStruct
    {
        public TypeLevelRedactedStruct(string secret) => Secret = secret;

        public string Secret { get; }
    }

    /// <summary>Type-level <c>[RedactData]</c> with default reason.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    internal sealed record TypeLevelRedactedRecord(string Email, string PhoneNumber);

    /// <summary>Type-level <c>[RedactData]</c> with a custom reason override.</summary>
    [RedactData(CustomReason = "MyCustomMaskReason")]
    internal sealed record TypeLevelRedactedWithCustomReasonRecord(string Value);

    /// <summary>Property-level <c>[RedactData]</c> on one of two properties.</summary>
    internal sealed record PropertyLevelRedactedRecord
    {
        public string Name { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>Multiple property-level <c>[RedactData]</c> properties.</summary>
    internal sealed record MultiPropertyRedactedRecord
    {
        public string Name { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public string Token { get; init; } = string.Empty;

        public int Age { get; init; }
    }

    /// <summary>Plain record with no redaction - passthrough fixture.</summary>
    internal sealed record PassthroughInner(string Value);

    /// <summary>Outer record holding a redacted-typed inner record property.</summary>
    internal sealed record OuterWithRedactedTypedInnerRecord
    {
        public string Description { get; init; } = string.Empty;

        public TypeLevelRedactedRecord? Inner { get; init; }
    }

    /// <summary>
    /// Outer record holding a property-level redaction directly on the inner
    /// reference (the inner type itself does NOT carry a type-level
    /// attribute).
    /// </summary>
    internal sealed record OuterWithPropertyLevelRedactedInnerRecord
    {
        public string Description { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public PassthroughInner? Secret { get; init; }
    }

    /// <summary>Outer with a list of redacted-typed entries.</summary>
    internal sealed record OuterWithListOfRedactedRecord
    {
        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<TypeLevelRedactedRecord> Items { get; init; } = [];
    }

    /// <summary>No redaction anywhere - passthrough.</summary>
    internal sealed record PassthroughRecord(string Name, int Number);

    /// <summary>Two redacted properties with different reasons.</summary>
    internal sealed record MixedReasonsRecord
    {
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public string Token { get; init; } = string.Empty;
    }

    /// <summary>
    /// Base class that declares a redacted property that the subclass inherits.
    /// It tests that the policy follows inheritance when it reads the property
    /// attribute.
    /// </summary>
    internal abstract record InheritanceBase
    {
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>
    /// Subclass adds its own property; inherits the redacted Email from base.
    /// </summary>
    internal sealed record InheritanceChild : InheritanceBase
    {
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>Plain class (not record) with type-level redaction.</summary>
    [RedactData(Reason = RedactReason.FinancialInformation)]
    internal sealed class TypeLevelRedactedClass
    {
        public string AccountNumber { get; init; } = string.Empty;
    }

    /// <summary>Type with no public properties - no-redaction-needed empty fixture.</summary>
    internal sealed class EmptyClass
    {
    }

    /// <summary>
    /// Mixed access fixture with a public instance property, a private field
    /// and a public static property. The policy reads only
    /// <c>BindingFlags.Public | Instance</c> members, so it skips the private
    /// field and the static property.
    /// </summary>
    internal sealed class MixedAccessClass
    {
#pragma warning disable CS0414, IDE0051
        // The private readonly field exists only to verify that the policy skips private fields.
        private readonly string _privateValue = "private";
#pragma warning restore CS0414, IDE0051

        // The static member exists only to verify that the policy skips static members.
        public static string StaticValue { get; set; } = "static";

        public string PublicValue { get; set; } = "public";
    }

    /// <summary>
    /// Field-level <c>[RedactData]</c>. It pins the documented limitation that
    /// the policy ignores attributes on fields.
    /// </summary>
    internal sealed class FieldLevelRedactedClass
    {
#pragma warning disable SA1401 // Public field used to drive the documented limitation test only.
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email = string.Empty;
#pragma warning restore SA1401
    }

    /// <summary>
    /// Cyclic graph: the outer node references the inner node, which references
    /// the outer node. It verifies that the policy does not interfere with
    /// Serilog's recursion-depth limit.
    /// </summary>
    internal sealed class CyclicNode
    {
        public string Name { get; set; } = string.Empty;

        public CyclicNode? Next { get; set; }
    }
}
