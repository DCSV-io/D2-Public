// -----------------------------------------------------------------------
// <copyright file="EnumExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Parse helpers that turn a string into a <see cref="System.Enum"/> value.
/// </summary>
public static class EnumExtensions
{
    /// <param name="input">The string to parse.</param>
    extension(string? input)
    {
        /// <summary>
        /// Tries to parse <paramref name="input"/> as <typeparamref name="TEnum"/>,
        /// ignoring case. Returns <c>true</c> and the parsed value when the input
        /// matches a defined enum member. Returns <c>false</c> and <c>null</c>
        /// when the input is null, empty, whitespace, or matches no member.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type.</typeparam>
        /// <param name="value">
        /// The parsed enum value on success; <c>null</c> on failure.
        /// </param>
        /// <returns>True on parse success; false otherwise.</returns>
        public bool TryParseTruthyNull<TEnum>(out TEnum? value)
            where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(input, ignoreCase: true, out var parsed))
            {
                value = parsed;
                return true;
            }

            value = null;
            return false;
        }
    }
}
