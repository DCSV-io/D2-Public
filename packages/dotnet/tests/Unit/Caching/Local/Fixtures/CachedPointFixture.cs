// -----------------------------------------------------------------------
// <copyright file="CachedPointFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local.Fixtures;

/// <summary>
/// A record of primitives, which is the shape the compare-and-set member
/// documents as the one whose equality means the same thing in every cache
/// flavor.
/// </summary>
/// <param name="X">The first member.</param>
/// <param name="Y">The second member.</param>
internal sealed record CachedPointFixture(int X, int Y);
