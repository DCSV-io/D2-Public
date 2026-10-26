// -----------------------------------------------------------------------
// <copyright file="SearchIndexDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

/// <summary>
/// Emitted search descriptor. Contains property names, never database column
/// names.
/// </summary>
/// <remarks>
/// The emitter reads contract models rather than database entities, and this
/// repository maps entity properties to snake-case columns explicitly. A
/// descriptor containing column names would work only while every searchable
/// property is a single word, so column vocabulary stays entirely on the
/// entity-configuration side.
/// </remarks>
/// <param name="BlobProperty">The property that holds the concatenated search column.</param>
/// <param name="SourceProperties">
/// The properties the blob concatenates, in the order they are joined.
/// </param>
/// <param name="Weights">
/// The relevance weight of each source property, positionally aligned with
/// <paramref name="SourceProperties"/>.
/// </param>
public sealed record SearchIndexDescriptor(
    string BlobProperty,
    IReadOnlyList<string> SourceProperties,
    IReadOnlyList<double> Weights);
