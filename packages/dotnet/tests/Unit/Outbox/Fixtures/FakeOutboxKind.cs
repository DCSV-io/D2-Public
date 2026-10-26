// -----------------------------------------------------------------------
// <copyright file="FakeOutboxKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A consumer's own closed kind set, standing in for the enums real consumers
/// declare. The package never sees one of these except as a type argument,
/// because the kind is generic at the call site.
/// </summary>
internal enum FakeOutboxKind
{
    /// <summary>The ordinary kind these tests stage.</summary>
    ThingHappened = 0,

    /// <summary>A second kind, for the tests that need two.</summary>
    OtherThingHappened = 1,
}
