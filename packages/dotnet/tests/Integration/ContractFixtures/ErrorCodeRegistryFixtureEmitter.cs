// -----------------------------------------------------------------------
// <copyright file="ErrorCodeRegistryFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.ErrorCodes.Registry;
using Xunit;

/// <summary>
/// Emits the cross-runtime parity fixture for the merged
/// <see cref="ErrorCodeRegistry"/>. Reflects the built registry -
/// every <see cref="ErrorCodeInfo"/> entry - and writes a deterministic,
/// sorted <c>registry.json</c> fixture to
/// <c>fixtures/error-codes-registry/</c> in the fixture directory that the TypeScript
/// contract tests read.
/// The fixture holds the map that the TypeScript <c>errorCodeRegistry</c> also exposes.
/// </summary>
/// <remarks>
/// JSON shape per entry:
/// <code>
/// {
///   "code": "LIB_AUTH_BEARER_MISSING",
///   "httpStatus": 401,
///   "category": "validation_failure",
///   "userMessageKeySnake": "auth_errors_UNAUTHORIZED",
///   "userMessageKeyPath": "TK.Auth.Errors.UNAUTHORIZED",
///   "factoryName": "BearerMissing",
///   "factoryShape": "standard",
///   "doc": "...",
///   "domain": "auth",
///   "owner": "lib",
///   "isDeprecated": false
/// }
/// </code>
/// The <c>category</c> field is the snake_case wire string (e.g.
/// <c>"validation_failure"</c>) produced by
/// <see cref="ErrorCategoryWire.ToWire"/>, the same string the TypeScript
/// registry holds in <c>category</c>.
/// The <c>userMessageKeySnake</c> field contains the runtime <c>.Key</c>
/// value of the .NET <see cref="DcsvIo.D2.I18n.TKMessage"/>; the
/// <c>userMessageKeyPath</c> field contains the spec's TK symbol-path
/// reference (derived by inverse-transforming the snake key). The fixture
/// holds both fields so that a difference between the symbol path and the
/// snake key shows in a comparison with the TypeScript registry entry.
/// </remarks>
public sealed class ErrorCodeRegistryFixtureEmitter
{
    private const string _CATALOG = "error-codes-registry";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Registry()
    {
        var data = new SortedDictionary<string, object?>(System.StringComparer.Ordinal);

        foreach (var info in ErrorCodeRegistry.All.OrderBy(i => i.Code, System.StringComparer.Ordinal))
        {
            var entry = new SortedDictionary<string, object?>(System.StringComparer.Ordinal)
            {
                ["code"] = info.Code,
                ["httpStatus"] = info.HttpStatus,
                ["category"] = info.Category.ToWire(),
                ["userMessageKeySnake"] = info.UserMessageKey.Key,
                ["userMessageKeyPath"] = SnakeKeyToPath(info.UserMessageKey.Key),
                ["factoryName"] = info.FactoryName,
                ["factoryShape"] = info.FactoryShape,
                ["doc"] = info.Doc,
                ["domain"] = info.Domain,
                ["owner"] = info.Owner,
                ["isDeprecated"] = info.IsDeprecated,
            };
            data[info.Code] = entry;
        }

        FixturePathHelpers.WriteFixture(_CATALOG, "registry", data);
    }

    /// <summary>
    /// Transforms a snake_case TK key into its TK symbol-path reference.
    /// This is the inverse of <c>TkKeyTransform.ToSnakeKey</c> of the
    /// error-codes emitter.
    /// </summary>
    /// <remarks>
    /// Input: <c>auth_errors_UNAUTHORIZED</c>.
    /// Output: <c>TK.Auth.Errors.UNAUTHORIZED</c>.
    /// </remarks>
    /// <param name="snakeKey">The snake_case TK key (e.g. <c>auth_errors_UNAUTHORIZED</c>).</param>
    /// <returns>The TK symbol-path reference (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c>).</returns>
    private static string SnakeKeyToPath(string snakeKey)
    {
        // snake key is <domain>_<category>_<CONSTANT...>
        // Split on '_' at the first and second underscore only; everything after the
        // second underscore is the constant, which keeps its own underscores.
        var underscoreIdx1 = snakeKey.IndexOf('_');
        if (underscoreIdx1 < 0)
            return snakeKey;

        var underscoreIdx2 = snakeKey.IndexOf('_', underscoreIdx1 + 1);
        if (underscoreIdx2 < 0)
            return snakeKey;

        var domain = snakeKey.Substring(0, underscoreIdx1);
        var category = snakeKey.Substring(underscoreIdx1 + 1, underscoreIdx2 - underscoreIdx1 - 1);
        var constant = snakeKey.Substring(underscoreIdx2 + 1);

        return "TK." + UpperFirst(domain) + "." + UpperFirst(category) + "." + constant;
    }

    private static string UpperFirst(string s) =>
        s.Length == 0
            ? s
            : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
