// -----------------------------------------------------------------------
// <copyright file="MarkedKeyMapPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A dictionary type that is not generic itself, whose keys are marked
/// personal. Only the serializer's key type identifies them.
/// </summary>
internal sealed class MarkedKeyMapPayloadStub : Dictionary<MarkedTypePayloadStub, string>;
