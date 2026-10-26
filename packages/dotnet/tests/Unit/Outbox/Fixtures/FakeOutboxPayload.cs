// -----------------------------------------------------------------------
// <copyright file="FakeOutboxPayload.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload whose shape the golden-file test pins, so a change to the
/// serializer options fails a test that says why rather than corrupting a
/// backlog nobody is looking at.
/// </summary>
/// <param name="SubjectId">An identifier the consumer's own message names.</param>
/// <param name="Detail">A member present on the wire.</param>
/// <param name="Absent">A member the options omit when it is null.</param>
internal sealed record FakeOutboxPayload(Guid SubjectId, string Detail, string? Absent);
