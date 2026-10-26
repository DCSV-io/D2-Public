// -----------------------------------------------------------------------
// <copyright file="DlqLifetimeContractFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using System.Text.Json;

/// <summary>Loads the shared lifetime expectations from
/// <c>contracts/messaging/fixtures/dlq-lifetime.json</c>, looking first under the
/// <c>public</c> subfolder of the folder that contains <c>pnpm-workspace.yaml</c>
/// and then under that folder itself.</summary>
internal static class DlqLifetimeContractFixture
{
    internal static JsonDocument Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "pnpm-workspace.yaml")))
            {
                foreach (var prefix in new[] { "public", string.Empty })
                {
                    var path = Path.Combine(
                        directory.FullName,
                        prefix,
                        "contracts",
                        "messaging",
                        "fixtures",
                        "dlq-lifetime.json");
                    if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path));
                }

                throw new FileNotFoundException("Shared DLQ lifetime fixture was not found.");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Fixture repository boundary was not found.");
    }
}
