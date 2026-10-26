// -----------------------------------------------------------------------
// <copyright file="MarkedElementListPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A collection type that is not generic itself, whose elements declare a
/// personal value. Only the serializer's element type identifies them.
/// </summary>
internal sealed class MarkedElementListPayloadStub : List<MarkedMemberPayloadStub>;
