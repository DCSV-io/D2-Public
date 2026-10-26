// -----------------------------------------------------------------------
// <copyright file="JsonIncludeFieldPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json.Serialization;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A payload whose marked value is a public field. The serializer writes a
/// field only when it has <see cref="JsonIncludeAttribute"/>, and this one
/// does.
/// </summary>
internal sealed class JsonIncludeFieldPayloadStub
{
#pragma warning disable SA1401 // A public field is the payload shape this fixture exists to present.
    /// <summary>A personal value the payload must not carry.</summary>
    [JsonInclude]
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string Email = "someone@example.test";
#pragma warning restore SA1401

    /// <summary>Gets or sets the subject the message names.</summary>
    public Guid SubjectId { get; set; }
}
