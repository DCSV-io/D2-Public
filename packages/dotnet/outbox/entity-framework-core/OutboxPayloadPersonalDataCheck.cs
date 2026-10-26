// -----------------------------------------------------------------------
// <copyright file="OutboxPayloadPersonalDataCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using DcsvIo.D2.Utilities.Attributes;

/// <summary>
/// Reports whether a payload contains a personal value anywhere the serializer
/// reaches, read from the <see cref="RedactDataAttribute"/> markers.
/// </summary>
/// <remarks>
/// <para>
/// The type walk reads the members the payload serializer writes, from the
/// metadata <see cref="OutboxPayloadSerializer.Options"/> resolves: public
/// properties, fields and non-public properties with <c>[JsonInclude]</c>,
/// the element and key types of a collection, and every derived type a
/// <c>[JsonDerivedType]</c> attribute lists. It also reads the type itself and
/// the parameters of its public constructors, where a positional record's marker
/// without a <c>property:</c> target ends up, and recurses into each generic type
/// argument. A type with a converter of its own has no member metadata, so its
/// public properties are read instead. A type in a <c>System</c> namespace is
/// not walked into beyond its type arguments and, for a collection that is not
/// generic itself, its element type, because no framework type has the
/// marker.
/// </para>
/// <para>
/// A member declared as <see cref="object"/>, or a collection of them, is written
/// by the runtime type of the value it holds, so the type does not determine it.
/// A payload whose type reaches one is walked again over the instance, each value
/// read by its runtime type. A value there whose type has a converter of its own
/// and itself holds such a member is reported as containing personal data,
/// because nothing describes what the converter writes from it.
/// </para>
/// <para>
/// Each type and each instance is visited once per walk, so a cyclic graph ends.
/// The type verdict is cached per type, and the cache has one entry for each
/// distinct type that is staged or held in an object-typed member.
/// </para>
/// <para>
/// The check reads markers, not values: a personal value on an unmarked member
/// passes it, so this check does not establish that a payload contains no
/// personal value, and that precondition is met before staging.
/// </para>
/// </remarks>
internal static class OutboxPayloadPersonalDataCheck
{
    private const BindingFlags _MEMBERS = BindingFlags.Public | BindingFlags.Instance;

    private static readonly ConcurrentDictionary<Type, Reach> sr_verdicts = new();

    /// <summary>
    /// The furthest a type walk reached, in increasing order of consequence.
    /// </summary>
    private enum Reach
    {
        /// <summary>No marker and no member written by its runtime type.</summary>
        None,

        /// <summary>No marker, but a member written by its runtime type.</summary>
        RuntimeTyped,

        /// <summary>A marker the serializer reaches.</summary>
        Marker,
    }

    /// <summary>
    /// Reports whether <paramref name="payloadType"/> declares a personal value.
    /// </summary>
    /// <param name="payloadType">The type to walk.</param>
    /// <returns>
    /// <c>true</c> when some marker is reachable from the type alone. A member
    /// written by its runtime type is not covered here.
    /// </returns>
    internal static bool DeclaresPersonalData(Type payloadType)
        => VerdictOf(payloadType) == Reach.Marker;

    /// <summary>
    /// Reports whether <paramref name="payload"/> contains a personal value, from
    /// its type and, where the type holds members written by their runtime type,
    /// from the values those members hold.
    /// </summary>
    /// <param name="payload">The payload being staged.</param>
    /// <returns><c>true</c> when some marker is reachable from the payload.</returns>
    internal static bool CarriesPersonalData(object payload)
        => Carries(payload, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static Reach VerdictOf(Type type)
        => sr_verdicts.GetOrAdd(type, static t => Walk(t, []));

    private static Reach Walk(Type type, HashSet<Type> visited)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(object))
            return Reach.RuntimeTyped;

        if (!visited.Add(target))
            return Reach.None;

        if (target.IsDefined(typeof(RedactDataAttribute), inherit: true))
            return Reach.Marker;

        if (target.IsArray)
            return Walk(target.GetElementType()!, visited);

        var reach = Reach.None;

        if (target.IsGenericType)
        {
            foreach (var argument in target.GetGenericArguments())
            {
                if (Widen(ref reach, Walk(argument, visited)))
                    return Reach.Marker;
            }
        }

        if (target.IsPrimitive || target.IsEnum || target.IsGenericParameter)
            return reach;

        if (IsFrameworkNamespace(target.Namespace))
            return WalkFrameworkElements(target, reach, visited);

        if (target.GetConstructors(_MEMBERS)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.IsDefined(typeof(RedactDataAttribute), inherit: true)))
        {
            return Reach.Marker;
        }

        var info = MetadataOf(target);

        if (info.Kind == JsonTypeInfoKind.None)
        {
            foreach (var property in target.GetProperties(_MEMBERS))
            {
                if (property.IsDefined(typeof(RedactDataAttribute), inherit: true)
                    || Widen(ref reach, Walk(property.PropertyType, visited)))
                {
                    return Reach.Marker;
                }
            }

            return reach;
        }

        foreach (var member in info.Properties)
        {
            if (member.AttributeProvider?.IsDefined(typeof(RedactDataAttribute), inherit: true) == true
                || Widen(ref reach, Walk(member.PropertyType, visited)))
            {
                return Reach.Marker;
            }
        }

        if (info.ElementType is { } element && Widen(ref reach, Walk(element, visited)))
            return Reach.Marker;

        if (info.KeyType is { } key && Widen(ref reach, Walk(key, visited)))
            return Reach.Marker;

        foreach (var derived in info.PolymorphismOptions?.DerivedTypes ?? [])
        {
            if (Widen(ref reach, Walk(derived.DerivedType, visited)))
                return Reach.Marker;
        }

        return reach;
    }

    /// <summary>
    /// Walks the elements of a framework type that is not generic itself, which
    /// its type arguments do not describe.
    /// </summary>
    /// <param name="target">The framework type.</param>
    /// <param name="reach">The furthest reach its type arguments found.</param>
    /// <param name="visited">The types this walk has visited.</param>
    /// <returns>The furthest reach, including the elements.</returns>
    private static Reach WalkFrameworkElements(Type target, Reach reach, HashSet<Type> visited)
    {
        if (target == typeof(string)
            || target.IsGenericType
            || !typeof(IEnumerable).IsAssignableFrom(target))
        {
            return reach;
        }

        var elements = target.GetInterfaces()
            .Where(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToList();

        // A collection with no typed enumeration holds its elements as object,
        // and the serializer writes each by its runtime type.
        if (elements.Count == 0)
            return Reach.RuntimeTyped;

        foreach (var element in elements)
        {
            if (Widen(ref reach, Walk(element, visited)))
                return Reach.Marker;
        }

        return reach;
    }

    private static bool Carries(object? value, HashSet<object> seen)
    {
        if (value is null)
            return false;

        var type = value.GetType();

        // A bare object has no members, so the serializer writes it as {}.
        if (type == typeof(object))
            return false;

        switch (VerdictOf(type))
        {
            case Reach.Marker:
                return true;
            case Reach.None:
                return false;
        }

        if (!seen.Add(value))
            return false;

        var info = MetadataOf(type);

        switch (info.Kind)
        {
            case JsonTypeInfoKind.Dictionary when value is IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (Carries(entry.Key, seen) || Carries(entry.Value, seen))
                        return true;
                }

                return false;

            case JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary
                when value is IEnumerable items:
                foreach (var item in items)
                {
                    if (Carries(item, seen))
                        return true;
                }

                return false;

            case JsonTypeInfoKind.Object:
                foreach (var member in info.Properties)
                {
                    if (member.Get is { } read && Carries(read(value), seen))
                        return true;
                }

                return false;

            default:
                // A converter of its own writes this value, and nothing
                // describes what it writes from the object-typed member.
                return true;
        }
    }

    /// <summary>
    /// Reads the metadata the payload serializer writes <paramref name="type"/>
    /// from.
    /// </summary>
    /// <remarks>
    /// The options set no resolver, and the serializer supplies the default one
    /// only the first time it runs. The check can run before that, so it asks for
    /// the same population the serializer performs.
    /// </remarks>
    /// <param name="type">The type to read.</param>
    /// <returns>The serializer's metadata for the type.</returns>
    private static JsonTypeInfo MetadataOf(Type type)
    {
        var options = OutboxPayloadSerializer.Options;
        options.MakeReadOnly(populateMissingResolver: true);

        return options.GetTypeInfo(type);
    }

    /// <summary>
    /// Raises <paramref name="reach"/> to <paramref name="found"/> where that is
    /// further, and reports whether a marker was found.
    /// </summary>
    /// <param name="reach">The furthest reach so far.</param>
    /// <param name="found">What the latest walk found.</param>
    /// <returns><c>true</c> when <paramref name="found"/> is a marker.</returns>
    private static bool Widen(ref Reach reach, Reach found)
    {
        if (found > reach)
            reach = found;

        return found == Reach.Marker;
    }

    private static bool IsFrameworkNamespace(string? ns)
        => ns is not null
            && (ns == nameof(System) || ns.StartsWith(nameof(System) + ".", StringComparison.Ordinal));
}
