// -----------------------------------------------------------------------
// <copyright file="MarkedTypePayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>A payload whose type as a whole is marked personal.</summary>
/// <param name="Value">A member that has no marker of its own.</param>
[RedactData(Reason = RedactReason.PersonalInformation)]
internal sealed record MarkedTypePayloadStub(string Value);
