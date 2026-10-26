// -----------------------------------------------------------------------
// <copyright file="OutboxOptionsFor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

/// <summary>
/// One database's outbox settings. It adds no member to
/// <see cref="OutboxOptions"/>: the type parameter is the key.
/// </summary>
/// <typeparam name="TDb">
/// The database these settings belong to, identified by its application-layer
/// context interface.
/// </typeparam>
/// <remarks>
/// Two modules over two databases need two sets of these -
/// two lock keys, two connection strings - and the framework's options pattern
/// separates two sets either by a name or by a type. A name is a string two
/// registrations have to spell the same way, and a relay reading the wrong set
/// takes the wrong lock against the wrong database. The type provides the same
/// separation and the compiler checks the spelling, which is why the relay
/// reads <c>IOptionsMonitor&lt;OutboxOptionsFor&lt;TDb&gt;&gt;</c> and no
/// options name exists.
/// </remarks>
public sealed class OutboxOptionsFor<TDb> : OutboxOptions;
