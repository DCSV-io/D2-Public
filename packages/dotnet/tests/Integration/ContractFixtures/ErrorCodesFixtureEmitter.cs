// -----------------------------------------------------------------------
// <copyright file="ErrorCodesFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Emits the parity fixture for the generic ErrorCodes catalog reflected off
/// the .NET codegen-emitted static class <see cref="ErrorCodes"/>. The
/// fixture <c>codes.json</c> maps every constant name to its wire value, as
/// the codegen-emitted TypeScript catalog does (<c>@dcsv-io/d2-result</c>'s
/// <c>ErrorCodes</c> object). The fixture <c>http-statuses.json</c> maps every
/// code in <c>ErrorCodes.AllCodes</c> to its HTTP status.
/// </summary>
public sealed class ErrorCodesFixtureEmitter
{
    private const string _CATALOG = "error-codes";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Codes()
    {
        var data = EnumerateConstants(typeof(ErrorCodes));
        FixturePathHelpers.WriteFixture(_CATALOG, "codes", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_HttpStatuses()
    {
        // Per-code -> HTTP-status mapping, as ErrorCodes.GetHttpStatus returns it;
        // the TypeScript getErrorHttpStatus returns the same statuses.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var code in ErrorCodes.AllCodes)
            data[code] = ErrorCodes.GetHttpStatus(code);

        FixturePathHelpers.WriteFixture(_CATALOG, "http-statuses", data);
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the catalog type; produce a
    /// sorted map keyed by the field name (e.g. <c>NOT_FOUND</c> ->
    /// <c>"GEN_NOT_FOUND"</c>) so the fixture has the shape of the TypeScript
    /// const map.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateConstants(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            data[f.Name] = (string)f.GetValue(null)!;

        return data;
    }
}
