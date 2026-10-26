// -----------------------------------------------------------------------
// <copyright file="MarkedArrayPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload whose array elements declare a personal value, so the marker is
/// reached only through the array's element type.
/// </summary>
/// <param name="Contacts">Elements that each declare a personal value.</param>
internal sealed record MarkedArrayPayloadStub(MarkedMemberPayloadStub[] Contacts);
