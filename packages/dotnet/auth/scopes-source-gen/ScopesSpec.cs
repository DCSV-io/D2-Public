// -----------------------------------------------------------------------
// <copyright file="ScopesSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/auth-scopes/scopes.spec.json</c>. The
/// <c>$schema</c> field is absent: editors and IDEs validate against the
/// JSON Schema at edit time. The loader reads the data fields, and
/// <see cref="ScopesEmitter"/> validates them.
/// </summary>
/// <param name="Scopes">Every scope declared in the spec (in spec order).</param>
internal sealed record ScopesSpec(ImmutableArray<ScopeEntry> Scopes);
