// -----------------------------------------------------------------------
// <copyright file="PolymorphicSlotPayloadStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

/// <summary>
/// A payload whose only member is declared as the polymorphic base type, so the
/// marked value is reachable only through a derived type the base declares.
/// </summary>
/// <param name="Item">A value written by its runtime type.</param>
internal sealed record PolymorphicSlotPayloadStub(PolymorphicBasePayloadStub Item);
