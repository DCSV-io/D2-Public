// -----------------------------------------------------------------------
// <copyright file="PolymorphicBasePayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json.Serialization;

/// <summary>
/// An unmarked base type the serializer writes by its runtime type, because it
/// declares <see cref="MarkedDerivedPayloadStub"/> as a derived type.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(MarkedDerivedPayloadStub), "marked")]
internal class PolymorphicBasePayloadStub
{
    /// <summary>Gets or sets the subject the message names.</summary>
    public Guid SubjectId { get; set; }
}
