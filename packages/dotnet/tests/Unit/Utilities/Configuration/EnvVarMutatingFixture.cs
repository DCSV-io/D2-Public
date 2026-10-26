// -----------------------------------------------------------------------
// <copyright file="EnvVarMutatingFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration;

using Xunit;

/// <summary>
/// Serializes any test class decorated with <c>[Collection("EnvVarMutating")]</c>.
/// xUnit runs the tests of one collection one at a time, and
/// <c>DisableParallelization</c> stops other collections from running at the same
/// time, so one test's environment-variable changes do not overlap another test's.
/// </summary>
[CollectionDefinition("EnvVarMutating", DisableParallelization = true)]
public sealed class EnvVarMutatingFixture
{
}
