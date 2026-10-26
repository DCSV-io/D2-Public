// -----------------------------------------------------------------------
// <copyright file="MarkedDerivedPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// The derived type that <see cref="PolymorphicBasePayloadStub"/> declares, with
/// a marked value the base type does not declare.
/// </summary>
internal sealed class MarkedDerivedPayloadStub : PolymorphicBasePayloadStub
{
    /// <summary>Gets or sets a personal value the payload must not carry.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string Email { get; set; } = "someone@example.test";
}
