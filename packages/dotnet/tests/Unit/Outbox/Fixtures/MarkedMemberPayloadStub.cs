// -----------------------------------------------------------------------
// <copyright file="MarkedMemberPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A payload that declares a personal value on one of its own properties, the
/// case the staging seam refuses.
/// </summary>
/// <param name="SubjectId">The subject the message names.</param>
/// <param name="Email">A personal value the payload must not carry.</param>
internal sealed record MarkedMemberPayloadStub(
    Guid SubjectId,
    [property: RedactData(Reason = RedactReason.PersonalInformation)] string Email);
