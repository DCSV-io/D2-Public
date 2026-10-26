// -----------------------------------------------------------------------
// <copyright file="NestedMarkedPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload whose own members have no marker but whose list elements do, so
/// the refusal has to find the marker through a collection's element type.
/// </summary>
/// <param name="SubjectId">The subject the message names.</param>
/// <param name="Contacts">Elements that each declare a personal value.</param>
internal sealed record NestedMarkedPayloadStub(
    Guid SubjectId,
    IReadOnlyList<MarkedMemberPayloadStub> Contacts);
