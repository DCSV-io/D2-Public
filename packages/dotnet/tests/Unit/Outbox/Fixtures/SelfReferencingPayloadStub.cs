// -----------------------------------------------------------------------
// <copyright file="SelfReferencingPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload the serializer refuses, because it points at itself. It stands in
/// for the class of caller mistake the staging seam has to catch before a row
/// reaches the table - a payload nothing can read back is a row the relay would
/// retry for the whole window and then abandon.
/// </summary>
internal sealed class SelfReferencingPayloadStub
{
    /// <summary>Initializes a new <see cref="SelfReferencingPayloadStub"/>.</summary>
    public SelfReferencingPayloadStub()
    {
        Loop = this;
    }

    /// <summary>Gets the cycle that makes this payload unserializable.</summary>
    public SelfReferencingPayloadStub Loop { get; }
}
