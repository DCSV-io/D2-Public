// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Emits the parity fixture for the codegen-emitted field-constraints catalog
/// reflected off the <c>DcsvIo.D2.Validation.Abstractions</c> assembly. The
/// fixture has these top-level groups:
/// <list type="bullet">
///   <item>
///     <c>constraints</c> - one <c>{ NAME: intValue }</c> map over every
///     <see cref="FieldConstraints"/> <c>public const int</c> (the integer is
///     the value on both runtimes).
///   </item>
///   <item>
///     <c>enums</c> - one sub-map per taxonomy enum keyed by enum-member name;
///     the value is the wire form (the member name itself - all enums
///     serialize via <c>JsonStringEnumConverter</c> so the wire form equals
///     the member name, matching the TypeScript string-valued const objects).
///   </item>
/// </list>
/// The emitter writes the fixture to <c>fixtures/validation/field-constraints.json</c>.
/// </summary>
public sealed class FieldConstraintsFixtureEmitter
{
    private const string _CATALOG = "validation";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_FieldConstraints()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["constraints"] = EnumerateConstInts(typeof(FieldConstraints)),
            ["enums"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["NamePrefix"] = EnumerateStringWireEnum(typeof(NamePrefix)),
                ["NameSuffix"] = EnumerateStringWireEnum(typeof(NameSuffix)),
                ["BiologicalSex"] = EnumerateStringWireEnum(typeof(BiologicalSex)),
            },
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "field-constraints", data);
    }

    /// <summary>
    /// Reflect every <c>public const int</c> field declared on the supplied
    /// static class into a sorted <c>{ NAME: intValue }</c> map. The map has the
    /// shape of the TypeScript numeric const object (its keys are the constant
    /// names; its values are the integers).
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateConstInts(Type type)
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(int));

        foreach (var field in fields)
        {
            var value = Convert.ToInt32(
                field.GetRawConstantValue(), CultureInfo.InvariantCulture);

            data[field.Name] = value;
        }

        return data;
    }

    /// <summary>
    /// Reflect every member of a string-wire enum (the wire form is the member
    /// name via <c>JsonStringEnumConverter</c>) into a sorted
    /// <c>{ memberName: memberName }</c> map. Keying by the wire form matches
    /// the shape of the TypeScript const object, whose keys are the wire-form
    /// strings.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateStringWireEnum(Type enumType)
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var names = Enum.GetNames(enumType).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in names)
            data[name] = name;

        return data;
    }
}
