// -----------------------------------------------------------------------
// <copyright file="ConvertedMarkedValueStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json.Serialization;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A value written by a converter of its own, with a marked public property.
/// The serializer has no member metadata for it, so the property is found by
/// reading the type's public properties.
/// </summary>
[JsonConverter(typeof(ConvertedMarkedValueStubConverter))]
internal sealed class ConvertedMarkedValueStub
{
    /// <summary>Gets or sets a personal value the payload must not carry.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string Email { get; set; } = "someone@example.test";
}
