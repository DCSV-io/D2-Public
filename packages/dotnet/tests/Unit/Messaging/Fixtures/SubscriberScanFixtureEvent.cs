// -----------------------------------------------------------------------
// <copyright file="SubscriberScanFixtureEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Fixtures;

/// <summary>Test fixture: the message type that
/// <see cref="SubscriberScanFixtureHandler"/> consumes. It has no
/// <c>[MqPub]</c>, because the assembly scan reads the generic argument of the
/// handler and not a publisher contract.</summary>
public sealed class SubscriberScanFixtureEvent;
