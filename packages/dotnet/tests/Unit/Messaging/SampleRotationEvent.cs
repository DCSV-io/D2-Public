// -----------------------------------------------------------------------
// <copyright file="SampleRotationEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Unit.Messaging.Fixtures;

/// <summary>Test fixture: a message type carrying [MqPub] with a fixture
/// constant whose descriptor is plaintext, so the composer and the dispatcher
/// handle it on the plaintext code path.</summary>
[MqPub(MessagingCatalogFixture.SAMPLE_ROTATION)]
public sealed partial class SampleRotationEvent;
