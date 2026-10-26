// -----------------------------------------------------------------------
// <copyright file="SampleAuditEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Unit.Messaging.Fixtures;

/// <summary>Test fixture: a message type carrying [MqPub] with a fixture
/// constant that no shipped spec declares. It is a "happy-path" type with a
/// working descriptor.</summary>
/// <remarks>A catalog built by <see cref="MessagingCatalogFixture"/> supplies
/// the descriptor, and the unknown-constant case resolves against a catalog
/// that omits the constant. A test can also pass a hand-built
/// <see cref="MqMessageDescriptor"/> directly, and then nothing consults the
/// catalog.</remarks>
[MqPub(MessagingCatalogFixture.SAMPLE_AUDIT)]
public sealed partial class SampleAuditEvent;
