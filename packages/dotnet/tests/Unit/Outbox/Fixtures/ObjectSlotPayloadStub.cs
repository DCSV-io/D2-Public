// -----------------------------------------------------------------------
// <copyright file="ObjectSlotPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Collections;
using System.Text.Json.Nodes;

/// <summary>
/// A payload with members the serializer writes by the runtime type of the
/// values they hold. Whether it contains a marked value depends on the instance
/// rather than on the type.
/// </summary>
internal sealed class ObjectSlotPayloadStub
{
    /// <summary>Gets or sets a value written by its runtime type.</summary>
    public object? Detail { get; set; }

    /// <summary>Gets or sets values each written by its runtime type.</summary>
    public List<object> Items { get; set; } = [];

    /// <summary>Gets or sets named values each written by its runtime type.</summary>
    public Dictionary<string, object> Named { get; set; } = [];

    /// <summary>Gets or sets a collection that holds its elements as object.</summary>
    public ArrayList Loose { get; set; } = [];

    /// <summary>Gets or sets a JSON document, which holds no typed value.</summary>
    public JsonObject? Document { get; set; }
}
