// -----------------------------------------------------------------------
// <copyright file="EnumerableExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Extension methods for <see cref="IEnumerable{T}"/> that mirror the
/// <c>Truthy</c> and <c>Falsey</c> semantics used elsewhere, and a
/// <c>Clean</c> helper that applies a per-element cleaner with configurable
/// empty and null behavior.
/// </summary>
public static class EnumerableExtensions
{
    /// <param name="enumerable">The enumerable to operate on.</param>
    /// <typeparam name="T">The element type.</typeparam>
    extension<T>(IEnumerable<T>? enumerable)
    {
        /// <summary>
        /// Returns true when the enumerable is non-null and contains at least
        /// one element.
        /// </summary>
        public bool Truthy() => enumerable?.Any() ?? false;

        /// <summary>
        /// Returns true when the enumerable is null or contains zero elements.
        /// </summary>
        public bool Falsey() => !enumerable.Truthy();

        /// <summary>
        /// Applies <paramref name="cleaner"/> to every element and shapes the
        /// result according to the supplied empty and null behaviors.
        /// </summary>
        ///
        /// <param name="cleaner">
        /// Cleaner function. A <c>null</c> return value marks the element as
        /// dropped.
        /// </param>
        /// <param name="enumEmptyBehavior">
        /// Selects the result when the input is null or empty, or when cleaning
        /// produced an empty list. Defaults to <see cref="CleanEnumEmptyBehavior.ReturnNull"/>.
        /// </param>
        /// <param name="valueNullBehavior">
        /// Selects the result when the cleaner returns null for any element. Defaults to
        /// <see cref="CleanValueNullBehavior.RemoveNulls"/>.
        /// </param>
        ///
        /// <returns>
        /// The cleaned enumerable, an empty enumerable, or null, according to
        /// the supplied behavior options.
        /// </returns>
        ///
        /// <exception cref="InvalidOperationException">
        /// Thrown when the cleaner returns null for any element and
        /// <paramref name="valueNullBehavior"/> is
        /// <see cref="CleanValueNullBehavior.ThrowOnNull"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the input or the cleaned enumerable is empty and
        /// <paramref name="enumEmptyBehavior"/> is
        /// <see cref="CleanEnumEmptyBehavior.Throw"/>.
        /// </exception>
        public IEnumerable<T>? Clean(
            Func<T, T?> cleaner,
            CleanEnumEmptyBehavior enumEmptyBehavior = CleanEnumEmptyBehavior.ReturnNull,
            CleanValueNullBehavior valueNullBehavior = CleanValueNullBehavior.RemoveNulls)
        {
            // Materialize once so the upstream LINQ query is enumerated only once.
            var dirty = enumerable?.ToList();

            if (dirty.Falsey())
                return HandleEmpty<T>(nameof(enumerable), enumEmptyBehavior);

            List<T> clean = [];

            foreach (var item in dirty!)
            {
                var cleanedItem = cleaner(item);

                if (cleanedItem is not null)
                {
                    clean.Add(cleanedItem);
                    continue;
                }

                if (valueNullBehavior == CleanValueNullBehavior.ThrowOnNull)
                {
                    throw new InvalidOperationException(
                        "A cleaned value evaluated to null.");
                }
            }

            if (clean.Falsey())
                return HandleEmpty<T>(nameof(enumerable), enumEmptyBehavior);

            return clean;
        }
    }

    private static IEnumerable<T>? HandleEmpty<T>(
        string paramName,
        CleanEnumEmptyBehavior behavior)
    {
        return behavior switch
        {
            CleanEnumEmptyBehavior.ReturnEmpty => [],
            CleanEnumEmptyBehavior.Throw => throw new ArgumentException(
                "The enumerable is empty after cleaning.",
                paramName),
            _ => null,
        };
    }
}
