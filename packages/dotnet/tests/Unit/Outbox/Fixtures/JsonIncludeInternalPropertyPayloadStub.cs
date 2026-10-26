// -----------------------------------------------------------------------
// <copyright file="JsonIncludeInternalPropertyPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json.Serialization;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A payload whose marked value is an internal property the serializer writes
/// because it has <see cref="JsonIncludeAttribute"/>.
/// </summary>
internal sealed class JsonIncludeInternalPropertyPayloadStub
{
    /// <summary>Gets or sets the subject the message names.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Gets or sets a personal value the payload must not carry.</summary>
    [JsonInclude]
    [RedactData(Reason = RedactReason.PersonalInformation)]
    internal string Email { get; set; } = "someone@example.test";
}
