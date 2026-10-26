// -----------------------------------------------------------------------
// <copyright file="RunD2ServiceWebApplicationExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Xunit;

public sealed class RunD2ServiceWebApplicationExtensionsTests
{
    [Fact]
    public async Task RunD2ServiceAsync_NullApp_ThrowsArgumentNullException()
    {
        WebApplication? app = null;

        var act = async () => await app!.RunD2ServiceAsync();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // The serviceName fallback and the happy-path and fault-path scenarios
    // are covered in the integration test class RunD2ServiceAsyncTests,
    // because they require constructing a WebApplication through
    // WebApplication.CreateBuilder, which is an integration-level concern.
}
