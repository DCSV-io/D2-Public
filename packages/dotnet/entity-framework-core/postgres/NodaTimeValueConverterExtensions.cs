// -----------------------------------------------------------------------
// <copyright file="NodaTimeValueConverterExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore.Postgres;

using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

/// <summary>
/// Npgsql EF Core extension methods that configure the Npgsql NodaTime
/// plugin, enabling value conversions between NodaTime types and PostgreSQL
/// timestamp / text columns.
/// </summary>
public static class NodaTimeValueConverterExtensions
{
    extension(NpgsqlDbContextOptionsBuilder optionsBuilder)
    {
        /// <summary>
        /// Configures the <see cref="NpgsqlDbContextOptionsBuilder" /> to use the
        /// Npgsql NodaTime plugin, enabling automatic value conversions for
        /// <c>Instant</c> &lt;-> <c>timestamptz</c>, <c>LocalDateTime</c> &lt;->
        /// <c>timestamp</c>, <c>LocalDate</c> &lt;-> <c>date</c>, and other
        /// NodaTime &lt;-> PostgreSQL type mappings. Call from the Npgsql configuration
        /// lambda passed to <c>UseNpgsql(connStr, opts =&gt; opts.AddD2NodaTime())</c>.
        /// Idempotent: calling this method more than once on the same builder
        /// is safe (the underlying <c>UseNodaTime()</c> tolerates repeat
        /// invocations).
        /// </summary>
        /// <returns>
        /// The same <see cref="NpgsqlDbContextOptionsBuilder" /> for fluent
        /// chaining.
        /// </returns>
        public NpgsqlDbContextOptionsBuilder AddD2NodaTime()
        {
            optionsBuilder.UseNodaTime();
            return optionsBuilder;
        }
    }
}
