// -----------------------------------------------------------------------
// <copyright file="ConvertedValueStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json.Serialization;

/// <summary>
/// A value written by a converter of its own, holding a member declared as
/// <see cref="object"/>. The serializer has no member metadata for it, so
/// nothing describes what the converter writes from that member.
/// </summary>
[JsonConverter(typeof(ConvertedValueStubConverter))]
internal sealed class ConvertedValueStub
{
    /// <summary>Gets or sets a value the converter may write.</summary>
    public object? Inner { get; set; }
}
