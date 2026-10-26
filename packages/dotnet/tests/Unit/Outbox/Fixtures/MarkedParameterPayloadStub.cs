// -----------------------------------------------------------------------
// <copyright file="MarkedParameterPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// A payload whose marker has no <c>property:</c> target, so the compiler puts
/// it on the constructor parameter and the generated property has none.
/// </summary>
/// <param name="SubjectId">The subject the message names.</param>
/// <param name="Email">A personal value the payload must not carry.</param>
internal sealed record MarkedParameterPayloadStub(
    Guid SubjectId,
    [RedactData(Reason = RedactReason.PersonalInformation)] string Email);
