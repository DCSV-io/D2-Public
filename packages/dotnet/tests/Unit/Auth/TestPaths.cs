// -----------------------------------------------------------------------
// <copyright file="TestPaths.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.IO;

/// <summary>
/// Locates spec JSON files under contract roots by walking up from the test bin
/// directory until a workspace root marker is found.
/// </summary>
/// <remarks>
/// Supports both layouts and tells them apart. The lines below that carry an
/// exemption marker name a directory that the public clone does not have,
/// and each names it deliberately.
/// <list type="bullet">
/// <item><description>
/// Monorepo root D2.slnx, contracts at public/contracts. public-clone-path:exempt-line layout-map.
/// </description></item>
/// <item><description>
/// Public OSS root <c>D2.Public.slnx</c>, contracts at <c>contracts/</c>.
/// </description></item>
/// </list>
/// A nested public/ carries D2.Public.slnx too. public-clone-path:exempt-line layout-map.
/// The walker prefers the root above it.
/// </remarks>
internal static class TestPaths
{
    /// <summary>
    /// Returns the absolute path to the workspace root (monorepo or public OSS).
    /// </summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var monorepoMarker = Path.Combine(dir.FullName, "D2.slnx");

            // Prefer monorepo: D2.slnx is authoritative when present.
            if (File.Exists(monorepoMarker))
            {
                return dir.FullName;
            }

            var publicOssMarker = Path.Combine(dir.FullName, "D2.Public.slnx");

            if (File.Exists(publicOssMarker))
            {
                // Nested monorepo public/ also has D2.Public.slnx - keep walking
                // if the parent owns D2.slnx (true monorepo root is above).
                var parent = dir.Parent;

                if (parent is not null
                    && File.Exists(Path.Combine(parent.FullName, "D2.slnx")))
                {
                    dir = parent;
                    continue;
                }

                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root (D2.slnx or D2.Public.slnx) walking up from "
            + AppContext.BaseDirectory);
    }

    /// <summary>
    /// True when the workspace is the flattened public OSS clone (no monorepo
    /// <c>D2.slnx</c>).
    /// </summary>
    public static bool IsPublicOssLayout()
    {
        var root = RepoRoot();

        return File.Exists(Path.Combine(root, "D2.Public.slnx"))
            && !File.Exists(Path.Combine(root, "D2.slnx"));
    }

    public static string PublicContractsRoot() =>
        IsPublicOssLayout()
            ? Path.Combine(RepoRoot(), "contracts")
            : Path.Combine(RepoRoot(), "public", "contracts");

    /// <summary>
    /// Root of the public .NET package tree (layout-aware).
    /// </summary>
    public static string PublicPackagesDotnetRoot() =>
        IsPublicOssLayout()
            ? Path.Combine(RepoRoot(), "packages", "dotnet")
            : Path.Combine(RepoRoot(), "public", "packages", "dotnet");

    /// <summary>
    /// Root of the public TypeScript package tree (layout-aware).
    /// </summary>
    public static string PublicPackagesTypescriptRoot() =>
        IsPublicOssLayout()
            ? Path.Combine(RepoRoot(), "packages", "typescript")
            : Path.Combine(RepoRoot(), "public", "packages", "typescript");

    /// <summary>
    /// Absolute path to the public solution file (layout-aware).
    /// </summary>
    public static string PublicSolutionPath() =>
        IsPublicOssLayout()
            ? Path.Combine(RepoRoot(), "D2.Public.slnx")
            : Path.Combine(RepoRoot(), "public", "D2.Public.slnx");

    public static string AuthScopesSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-scopes", "scopes.spec.json");

    public static string AuthContextSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-context", "IAuthContext.spec.json");

    public static string RequestContextSpec() =>
        Path.Combine(PublicContractsRoot(), "request-context", "IRequestContext.spec.json");

    public static string HeadersSpec() =>
        Path.Combine(PublicContractsRoot(), "headers", "headers.spec.json");

    public static string InProcessKeysSpec() =>
        Path.Combine(PublicContractsRoot(), "in-process-keys", "keys.spec.json");

    public static string JwtClaimsSpec() =>
        Path.Combine(PublicContractsRoot(), "jwt-claims", "jwt-claims.spec.json");

    public static string AuthErrorCodesSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-error-codes", "auth-error-codes.spec.json");

    public static string MessagesDirectory() =>
        Path.Combine(PublicContractsRoot(), "messages");

    public static string ErrorCategorySpec() =>
        Path.Combine(PublicContractsRoot(), "error-category", "error-category.spec.json");
}
