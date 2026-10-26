// -----------------------------------------------------------------------
// <copyright file="ScopesEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the <c>Scopes</c> static partial class source from
/// a parsed <see cref="ScopesSpec"/>. It is stateless and does not use the Roslyn
/// host. It returns <see cref="EmitDiagnostic"/> records, which are translated
/// into Roslyn <c>Diagnostic</c> instances.
/// </summary>
internal static class ScopesEmitter
{
    private const string _DEFAULT_ROOT_NAMESPACE = "DcsvIo.D2.Auth.Abstractions";
    private const string _DEFAULT_SCOPES_CLASS_NAME = "Scopes";
    private const string _ANON_PREFIX = "anon.";

    // The name prefix that a scope declaring internal must have and that a scope
    // not declaring it must not have. The entry's own `internal` flag decides
    // whether a scope is internal; D2SCP010 requires the prefix to agree with the
    // flag, so testing the name gives the same result as reading the flag. The
    // runtime tests the name: it strips by the prefix while the catalog decides
    // by the flag (scopes-source-gen/README.md, the `internal` entry).
    private const string _INTERNAL_PREFIX = "internal.";

    /// <summary>
    /// Emits the Scopes class source plus diagnostics for the supplied spec.
    /// </summary>
    /// <param name="spec">Parsed scope spec.</param>
    /// <param name="orgTypeNames">
    /// OrgType enum member names extracted from the compilation symbol. Used
    /// for <c>grantedTo</c> validation and
    /// wildcard expansion. Adding a new <c>OrgType</c> member triggers
    /// re-emission with the expanded set automatically.
    /// </param>
    /// <param name="roleNames">Role enum member names, from the same source.</param>
    /// <param name="rootNamespace">Namespace of the emitted class (public or extension target).</param>
    /// <param name="className">
    /// Emit class name: <c>Scopes</c>, or the extension prefix followed by <c>Scopes</c>.
    /// </param>
    /// <returns>The generated source and the diagnostics.</returns>
    public static EmitResult Emit(
        ScopesSpec spec,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames,
        string? rootNamespace = null,
        string? className = null)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var orgTypeSet = new HashSet<string>(orgTypeNames, StringComparer.Ordinal);
        var roleSet = new HashSet<string>(roleNames, StringComparer.Ordinal);
        var effectiveNamespace = rootNamespace ?? _DEFAULT_ROOT_NAMESPACE;
        var effectiveClassName = className ?? _DEFAULT_SCOPES_CLASS_NAME;

        // Per-scope semantic validation (name shape, enum values and the checks below).
        var validScopes = new List<ScopeEntry>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scope in spec.Scopes)
        {
            if (!seenNames.Add(scope.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateScope(scope.Name));
                continue;
            }

            if (!ValidateScopeName(scope.Name, out var nameReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidScopeName(scope.Name, nameReason));
                continue;
            }

            var isAnon = scope.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal);
            var isInternal = scope.Internal;
            var hasInternalPrefix =
                scope.Name.StartsWith(_INTERNAL_PREFIX, StringComparison.Ordinal);

            // D2SCP010: the declared flag marks a scope workload-only, and the
            // `internal.` name prefix marks it the same way; the runtime strips by
            // the prefix (scopes-source-gen/README.md). The two are
            // interchangeable only while they agree, so a disagreement in either
            // direction drops the entry instead of emitting a scope that the flag
            // and the prefix classify differently.
            if (isInternal != hasInternalPrefix)
            {
                diagnostics.Add(EmitDiagnostics.InternalFlagPrefixDisagreement(
                    scope.Name, isInternal, hasInternalPrefix));
                continue;
            }

            // D2SCP005 is a warning only, and the scope is kept: impersonationBlocked
            // on an anonymous scope has no effect.
            if (isAnon && scope.ImpersonationBlocked)
                diagnostics.Add(EmitDiagnostics.AnonImpersonationBlockedNoise(scope.Name));

            // D2SCP008: a scope that no caller can ever be granted is unreachable, so
            // a non-anon scope normally needs grantedTo. A scope declaring internal is
            // the exception: the internal transaction-token mint grants it and the
            // org-role grant matrix does not, so it may omit grantedTo (no user
            // org-role can hold it).
            if (!isAnon && !isInternal && scope.GrantedTo is null)
            {
                diagnostics.Add(EmitDiagnostics.MissingGrantedTo(scope.Name));
                continue;
            }

            // grantedTo entries: validate keys/values, reject empty arrays.
            var grantedToValid = true;
            if (scope.GrantedTo is { } granted)
            {
                foreach (var kvp in granted)
                {
                    if (kvp.Value.Length == 0)
                    {
                        diagnostics.Add(EmitDiagnostics.EmptyRoleArray(scope.Name, kvp.Key));
                        grantedToValid = false;
                        break;
                    }

                    if (kvp.Key != "*" && !orgTypeSet.Contains(kvp.Key))
                    {
                        diagnostics.Add(EmitDiagnostics.UnknownEnumValue(
                            scope.Name,
                            kvp.Key,
                            "OrgType",
                            kvp.Key,
                            "* (wildcard), " + string.Join(", ", orgTypeNames)));
                        grantedToValid = false;
                        break;
                    }

                    foreach (var role in kvp.Value)
                    {
                        if (role != "*" && !roleSet.Contains(role))
                        {
                            diagnostics.Add(EmitDiagnostics.UnknownEnumValue(
                                scope.Name,
                                kvp.Key,
                                "Role",
                                role,
                                "* (wildcard), " + string.Join(", ", roleNames)));
                            grantedToValid = false;
                            break;
                        }
                    }

                    if (!grantedToValid)
                        break;
                }
            }

            if (!grantedToValid)
                continue;

            validScopes.Add(scope);
        }

        // Tree-position collision detection (D2SCP007). For every pair of valid
        // scopes, if one's name is a strict dot-prefix of the other, the prefix
        // scope cannot be both a leaf constant and a parent class. Drop the
        // shorter (parent) one, so the longer path can still be emitted.
        var collidingNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < validScopes.Count; i++)
        {
            for (var j = 0; j < validScopes.Count; j++)
            {
                if (i == j)
                    continue;

                var a = validScopes[i].Name;
                var b = validScopes[j].Name;
                if (b.StartsWith(a + ".", StringComparison.Ordinal))
                {
                    if (collidingNames.Add(a))
                        diagnostics.Add(EmitDiagnostics.TreePositionCollision(a, b));
                }
            }
        }

        if (collidingNames.Count > 0)
            validScopes = validScopes.Where(s => !collidingNames.Contains(s.Name)).ToList();

        // Build the tree of scope-name segments and emit source.
        var tree = new TreeNode(string.Empty);
        foreach (var scope in validScopes.OrderBy(s => s.Name, StringComparer.Ordinal))
            tree.AddScope(scope);

        var source = EmitSource(
            tree, validScopes, orgTypeNames, roleNames, effectiveNamespace, effectiveClassName);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    private static bool ValidateScopeName(string name, out string reason)
    {
        if (name.Falsey())
        {
            reason = "name is empty";
            return false;
        }

        if (name.Contains(".."))
        {
            reason = "name contains consecutive dots";
            return false;
        }

        var segments = name.Split('.');
        if (segments.Length < 2)
        {
            reason = "name must have at least 2 dot-separated segments";
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                reason = "leading or trailing dot in name";
                return false;
            }

            if (!IsAsciiLowerLetter(segment[0]))
            {
                reason = $"segment '{segment}' must start with a lowercase letter";
                return false;
            }

            for (var i = 1; i < segment.Length; i++)
            {
                var c = segment[i];
                if (!IsAsciiLowerLetter(c) && !IsAsciiDigit(c))
                {
                    reason =
                        $"segment '{segment}' contains invalid character '{c}' "
                        + "(only lowercase letters and digits allowed)";
                    return false;
                }
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string EmitSource(
        TreeNode tree,
        IReadOnlyList<ScopeEntry> validScopes,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames,
        string rootNamespace,
        string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.Auth.Scopes.SourceGen.ScopesGenerator");
        sb.AppendLine("//   from contracts/auth-scopes/scopes.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("//");
        sb.AppendLine("//   Wildcards in grantedTo were expanded against the OrgType + Role enum");
        sb.AppendLine("//   members read from the compilation at codegen time:");
        sb.AppendLine("//     OrgType: " + string.Join(", ", orgTypeNames));
        sb.AppendLine("//     Role:    " + string.Join(", ", roleNames));
        sb.AppendLine("//   Adding a new enum member triggers re-emission automatically.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// OAuth-canonical scope string constants generated from "
            + "<c>contracts/auth-scopes/scopes.spec.json</c>.");
        sb.AppendLine(
            "/// Each scope is represented as a nested <c>const string</c> matching its "
            + "dot-separated path");
        sb.AppendLine($"/// (e.g. <c>{className}.Auth.User.Impersonate.Force</c>).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        EmitTreeNode(sb, tree, indent: 1);

        sb.AppendLine();
        EmitHelpers(sb, validScopes, orgTypeNames, roleNames);

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitTreeNode(StringBuilder sb, TreeNode node, int indent)
    {
        var pad = new string(' ', indent * 4);

        var constantOrder = node.Constants.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < constantOrder.Count; i++)
        {
            var constName = constantOrder[i];
            var scope = node.Constants[constName];
            var doc = scope.Description ?? scope.Name;
            sb.AppendLine($"{pad}/// <summary>");
            sb.AppendLine($"{pad}/// {EscapeXmlDoc(doc)}");
            sb.AppendLine($"{pad}/// </summary>");

            if (scope.Deprecated)
            {
                sb.AppendLine(
                    $"{pad}[Obsolete(\"{EscapeStringLiteral(ObsoleteMessage(scope))}\")]");
            }

            sb.AppendLine(
                $"{pad}public const string {constName} = "
                + $"\"{EscapeStringLiteral(scope.Name)}\";");
            if (i < constantOrder.Count - 1 || node.Children.Count > 0)
                sb.AppendLine();
        }

        var childOrder = node.Children.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < childOrder.Count; i++)
        {
            var childName = childOrder[i];
            var childNode = node.Children[childName];
            sb.AppendLine($"{pad}/// <summary>");
            sb.AppendLine(
                $"{pad}/// Scopes nested under "
                + $"<c>{EscapeXmlDoc(childName.ToLowerInvariant())}</c>.");
            sb.AppendLine($"{pad}/// </summary>");
            sb.AppendLine($"{pad}public static class {childName}");
            sb.AppendLine($"{pad}{{");
            EmitTreeNode(sb, childNode, indent + 1);
            sb.AppendLine($"{pad}}}");
            if (i < childOrder.Count - 1)
                sb.AppendLine();
        }
    }

    private static void EmitHelpers(
        StringBuilder sb,
        IReadOnlyList<ScopeEntry> validScopes,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames)
    {
        var allScopes = validScopes
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var anonScopes = validScopes
            .Where(s => s.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal))
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var blockedScopes = validScopes
            .Where(s =>
                s.ImpersonationBlocked
                && !s.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal))
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Granted scopes: expand wildcards into a (OrgType, Role) -> SortedSet<string> map.
        var granted = new SortedDictionary<(string Org, string Role), SortedSet<string>>(
            Comparer<(string Org, string Role)>.Create((a, b) =>
            {
                var orgCompare = string.CompareOrdinal(a.Org, b.Org);
                return orgCompare != 0 ? orgCompare : string.CompareOrdinal(a.Role, b.Role);
            }));

        foreach (var scope in validScopes)
        {
            if (scope.GrantedTo is null)
                continue;

            foreach (var kvp in scope.GrantedTo)
            {
                IEnumerable<string> orgsFor = kvp.Key == "*" ? orgTypeNames : [kvp.Key];
                foreach (var org in orgsFor)
                {
                    foreach (var role in kvp.Value)
                    {
                        IEnumerable<string> rolesFor = role == "*" ? roleNames : [role];
                        foreach (var r in rolesFor)
                        {
                            var key = (org, r);
                            if (!granted.TryGetValue(key, out var set))
                            {
                                set = new SortedSet<string>(StringComparer.Ordinal);
                                granted[key] = set;
                            }

                            set.Add(scope.Name);
                        }
                    }
                }
            }
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// True when the entry service strips the scope from impersonated tokens");
        sb.AppendLine("    /// at mint time (defense-in-depth).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is impersonation-blocked.</returns>");
        sb.AppendLine("    public static bool IsImpersonationBlocked(string scope) =>");
        sb.AppendLine("        sr_impersonationBlockedScopes.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when the scope is in the <c>anon.*</c> namespace "
            + "(pre-auth, no");
        sb.AppendLine("    /// caller identity required).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is anonymous.</returns>");
        sb.AppendLine("    public static bool IsAnonymous(string scope) =>");
        sb.AppendLine("        scope.StartsWith(\"anon.\", StringComparison.Ordinal);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when the scope appears in the spec (i.e. is a known scope name).");
        sb.AppendLine("    /// Useful for rejecting unknown scope strings early.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is a known spec entry.</returns>");
        sb.AppendLine(
            "    public static bool IsKnown(string scope) => sr_allScopes.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when <paramref name=\"orgType\"/> + <paramref name=\"role\"/> "
            + "is granted");
        sb.AppendLine(
            "    /// <paramref name=\"scope\"/> per the spec's grantedTo matrix "
            + "(after wildcard expansion).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <param name=\"orgType\">The org type.</param>");
        sb.AppendLine("    /// <param name=\"role\">The role within the org.</param>");
        sb.AppendLine(
            "    /// <returns>True if the (orgType, role) pair is granted the scope."
            + "</returns>");
        sb.AppendLine(
            "    public static bool IsGrantedTo(string scope, OrgType orgType, Role role) =>");
        sb.AppendLine(
            "        sr_grantedScopes.TryGetValue((orgType, role), out var set) "
            + "&& set.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets all scope names declared in the spec (defensive read-only set).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlySet<string> AllScopes => sr_allScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the anonymous (<c>anon.*</c>) scope names - implicit universal "
            + "pre-auth grant.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlySet<string> AllAnonymousScopes => sr_anonymousScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the scopes that the entry service strips from impersonated tokens "
            + "at mint time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlySet<string> AllImpersonationBlockedScopes =>");
        sb.AppendLine("        sr_impersonationBlockedScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the (<see cref=\"OrgType\"/>, <see cref=\"Role\"/>) -> granted scope "
            + "set, with wildcards expanded.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "GrantedScopes =>");
        sb.AppendLine("        sr_grantedScopesView;");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_allScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in allScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_anonymousScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in anonScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_impersonationBlockedScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in blockedScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly Dictionary<(OrgType, Role), HashSet<string>> "
            + "sr_grantedScopesBacking =");
        sb.AppendLine("        new()");
        sb.AppendLine("    {");
        foreach (var kvp in granted)
        {
            sb.AppendLine(
                $"        [(OrgType.{kvp.Key.Org}, Role.{kvp.Key.Role})] = "
                + "new(StringComparer.Ordinal)");
            sb.AppendLine($"        {{");
            foreach (var scope in kvp.Value)
                sb.AppendLine($"            \"{EscapeStringLiteral(scope)}\",");

            sb.AppendLine($"        }},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly Dictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "sr_grantedScopes =");
        sb.AppendLine("        BuildGrantedScopesView();");
        sb.AppendLine();
        sb.AppendLine(
            "    private static readonly "
            + "IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "sr_grantedScopesView =");
        sb.AppendLine("        sr_grantedScopes;");
        sb.AppendLine();
        sb.AppendLine(
            "    private static Dictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "BuildGrantedScopesView()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        var result = "
            + "new Dictionary<(OrgType, Role), IReadOnlySet<string>>("
            + "sr_grantedScopesBacking.Count);");
        sb.AppendLine("        foreach (var kvp in sr_grantedScopesBacking)");
        sb.AppendLine("            result[kvp.Key] = kvp.Value;");
        sb.AppendLine();
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// The <c>[Obsolete]</c> message of a retired scope's constant. It contains the
    /// reason where the entry gives one and the successor where the entry gives
    /// one, so the message states the replacement and not only that the constant
    /// is obsolete.
    /// </summary>
    private static string ObsoleteMessage(ScopeEntry scope)
    {
        var reason = scope.DeprecatedReason.Falsey()
            ? $"Scope '{scope.Name}' is deprecated."
            : scope.DeprecatedReason!;

        // A reason that already contains the successor scope name is not repeated.
        if (scope.ReplacedBy.Falsey()
            || reason.IndexOf(scope.ReplacedBy!, StringComparison.Ordinal) >= 0)
        {
            return reason;
        }

        return $"{reason} Replaced by '{scope.ReplacedBy}'.";
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    private static bool IsAsciiLowerLetter(char c) => c is >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
