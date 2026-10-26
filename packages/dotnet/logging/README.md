<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Logging

Serilog configuration, the `[RedactData]` enforcement layer and an ASP.NET Core request-logging middleware. Other
shared libraries and per-service composition roots call it to wire the Serilog pipeline, the `[RedactData]`
destructuring policy and the request-logging middleware. A service calls `AddD2Logging` instead of repeating the same
`LoggerConfiguration` lines. Any host that wants the same Serilog setup can also use the package standalone, without an
aggregator.

## Install

```bash
dotnet add package DcsvIo.D2.Logging
```

The library does not own:

- OpenTelemetry SDK setup, which is out of scope. Logs reach OTLP collectors through the Microsoft.Extensions.Logging
  (MEL) pipeline (`writeToProviders: true` routes Serilog output to other registered `ILoggerProvider`s); separate
  observability infrastructure wires the OTLP log exporter.
- `[LoggerMessage]` source-generated delegates: this library sets up the log pipeline, so logging inside its own setup
  path would be circular.
- Activity / span enrichment (e.g. `Serilog.Enrichers.Span`), which is out of scope. Separate observability
  infrastructure emits activities and spans.

## Public API surface

### Composition

```csharp
services.AddD2Logging(configuration, opts =>
{
    opts.ServiceName = "orders";     // optional - defaults from OTEL_SERVICE_NAME -> IHostEnvironment.ApplicationName
    opts.Environment = "Production"; // optional - defaults from IHostEnvironment.EnvironmentName
    opts.MinimumLevel = LogEventLevel.Information; // optional - default Information
});

// later, inside the middleware pipeline:
app.UseRouting();
app.UseD2RequestLogging(); // optional configure callback
app.UseEndpoints(...);
```

`AddD2Logging(IConfiguration, Action<D2LoggingOptions>?)` builds the Serilog `LoggerConfiguration`, sets `Log.Logger`,
and connects the MEL bridge (`AddSerilog(preserveStaticLogger: true, writeToProviders: true)`). It validates the options
at the first `IOptions<D2LoggingOptions>.Value` resolution (typically host-startup composition) through
`ValidateOnStart()`, so invalid configuration fails at startup.

`UseD2RequestLogging(Action<RequestLoggingOptions>?)` wraps Serilog's `UseSerilogRequestLogging` with the D2 defaults:
per-source minimum-level overrides, infrastructure-path suppression (logged at `Verbose` so the default minimum-level
gate filters them out), diagnostic-context enrichment that omits the NOT-LOGGED fields of
[field-policy.md](docs/field-policy.md), and projection of the spec-driven `IRequestContext` LOG-OK fields onto the
request-completion event.

### Configuration - `D2LoggingOptions`

| Property              | Type                    | Default                                                          | Notes                                                                                                                                                                                                                       |
| --------------------- | ----------------------- | ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ServiceName`         | `string?`               | `OTEL_SERVICE_NAME` config -> `IHostEnvironment.ApplicationName` | Emitted on every log line via the `service_name` structured property. Validated non-empty / non-whitespace at startup.                                                                                                      |
| `Environment`         | `string?`               | `IHostEnvironment.EnvironmentName`                               | Emitted on every log line via the `environment` structured property. Validated non-empty / non-whitespace at startup.                                                                                                       |
| `MinimumLevel`        | `LogEventLevel`         | `Information`                                                    | Default minimum log level. Per-source overrides applied independently - see "Per-source overrides" below.                                                                                                                   |
| `InfrastructurePaths` | `IReadOnlyList<string>` | `["/health", "/alive", "/metrics", "/.well-known"]`              | Path prefixes whose request-completion events emit at `Verbose` instead of `Information`, so the default level gate filters them out. Validated non-empty (collection) and per-entry non-empty / non-whitespace at startup. |

### Constants

`D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY` - the `"OTEL_SERVICE_NAME"` configuration key consulted for the
`ServiceName` default. It follows the OpenTelemetry convention, so service names in logs, traces and metrics match,
and this library takes no OpenTelemetry SDK dependency.

### Per-source overrides

`AddD2Logging` applies these per-source minimum-level overrides on top of the configured `MinimumLevel`:

| Source                      | Override  |
| --------------------------- | --------- |
| `Microsoft.AspNetCore`      | `Warning` |
| `Microsoft.Extensions.Http` | `Warning` |
| `System.Net.Http`           | `Warning` |
| `D2`                        | `Debug`   |

These overrides reduce the framework and HTTP-client output that the Information level produces by default, and they
keep the diagnostic logs of D2 code visible.

### Enrichers

| Enricher                            | What it adds                                                                             |
| ----------------------------------- | ---------------------------------------------------------------------------------------- |
| `FromLogContext`                    | Per-scope properties pushed via `LogContext.PushProperty` propagate to nested log calls. |
| `WithMachineName`                   | `MachineName` structured property.                                                       |
| `WithProperty("service_name", ...)` | The configured `ServiceName`.                                                            |
| `WithProperty("environment", ...)`  | The configured `Environment`.                                                            |

### Output sink

`Console` with `CompactJsonFormatter` (terse JSON to stdout). When other `ILoggerProvider`s are registered against
`builder.Logging` (e.g. an OTLP log-exporter from observability infrastructure), Serilog routes events through both
sinks via `writeToProviders: true`.

## Field policy

`field-policy.md`, in this package's documentation folder in the repository, covers the network and IP enrichment
design, the fields emitted when populated, the fields suppressed by default, and how a host opts one back in.

## Destructuring discipline

`RedactDataDestructuringPolicy` enforces the `[RedactData]` attribute defined in `DcsvIo.D2.Utilities.Attributes`. It
runs on every `@`-captured object in every `ILogger.Log*` call across every service that wires `AddD2Logging`. Rules to
know:

1. Capture mode matters. Serilog's `@` prefix invokes destructuring, so the policy fires. The default `{prop}` capture
   (without `@`) calls `.ToString()` on the value and bypasses destructuring entirely. If you have `[RedactData]` on a
   record and write `logger.Information("user {User}", user)`, the record's default `ToString()` includes property
   values verbatim, which leaks the PII the attribute was supposed to redact.
2. Field-level `[RedactData]` is silently ignored. The policy reflects over `BindingFlags.Public | Instance`
   PROPERTIES only, not fields. Use property syntax for redaction.
3. Computed properties leak through their inputs. A `public string DisplayName => $"{First} {Last}"` is destructured
   even if `First` has `[RedactData]`. Redact the computed property too if its inputs are PII.

### Redaction modes

| Decoration                           | Effect                                                              |
| ------------------------------------ | ------------------------------------------------------------------- |
| `[RedactData]` on the type           | Entire value rendered as `[REDACTED: {Reason}]`.                    |
| `[RedactData]` on a property         | Only that property is masked; siblings destructure normally.        |
| `[RedactData(CustomReason = "...")]` | The custom string replaces the enum-name reason in the placeholder. |

Reason rendering uses the enum name (`PersonalInformation`, `FinancialInformation`, `SecretInformation`,
`VerboseContent`, `Other`, `Unspecified`) unless a `CustomReason` overrides it.

## Types (source map)

| File                                             | Role                                                                                                                   |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Logging.csproj`                       | csproj - `Microsoft.NET.Sdk.Web` + `OutputType=Library`. package references to `utilities/` + `context/abstractions/`. |
| `D2LoggingOptions.cs`                            | Sealed record - Options-pattern config.                                                                                |
| `D2LoggingConstants.cs`                          | Public constants (`OTEL_SERVICE_NAME_CONFIG_KEY`).                                                                     |
| `LoggingServiceCollectionExtensions.cs`          | Public DI extension: `AddD2Logging`.                                                                                   |
| `WebApplicationLoggingExtensions.cs`             | Public AspNetCore extension: `UseD2RequestLogging`.                                                                    |
| `Destructuring/RedactDataDestructuringPolicy.cs` | Internal sealed `IDestructuringPolicy` impl.                                                                           |
| `Destructuring/TypeRedactionInfo.cs`             | Internal sealed record - per-type cache value.                                                                         |
| `Internal/D2RequestContextEnricher.cs`           | Internal static helper that projects `IRequestContext` LOG-OK fields onto the request-completion log line.             |

## Dependencies

| Package                         | Why                                                                                                                                                                                    |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Serilog.AspNetCore`            | The umbrella package. Pulls Serilog, Serilog.Extensions.Logging, Serilog.Sinks.Console, Microsoft.Extensions.Logging.Abstractions transitively. `UseSerilogRequestLogging` lives here. |
| `Serilog.Formatting.Compact`    | `CompactJsonFormatter`. Listed explicitly so a transitive removal cannot silently drop it.                                                                                             |
| `Serilog.Enrichers.Environment` | `Enrich.WithMachineName()`.                                                                                                                                                            |
| `JetBrains.Annotations`         | `[MustDisposeResource]` annotations on disposable factory paths.                                                                                                                       |

| Package dependency               | Why                                                                                                                                                                                                                                                          |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `DcsvIo.D2.Utilities`            | `[RedactData]` attribute + `RedactReason` enum + `Falsey()` / `Truthy()` extensions.                                                                                                                                                                         |
| `DcsvIo.D2.Context.Abstractions` | `IRequestContext` interface (the strongly-typed enricher dependency).                                                                                                                                                                                        |
| `DcsvIo.D2.AspNetCore`           | Canonical `InfrastructurePathMatcher` consumed by `UseD2RequestLogging`'s level callback to demote infrastructure-path request-completion logs to `Verbose`. Single source of truth shared with `DcsvIo.D2.Telemetry`'s AspNetCore-instrumentation `Filter`. |

## Edge cases / gotchas

- `Log.Logger` is a process-global static. `AddD2Logging` SETS it, so in tests that build several hosts in one process
  the static holds the logger of the latest host. The library's integration tests use
  `[Collection("LogLoggerStaticState")]` to run serially against any other test that touches the static.
- `Microsoft.NET.Sdk.Web` sets `OutputType` to `Exe` by default. This csproj overrides it to `Library`, as the other
  ASP.NET library projects do.
- The csproj has no `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` PackageReferences. The
  framework reference (`Microsoft.AspNetCore.App`) ships them, and explicit references would trigger NU1510 ("will not
  be pruned"). `Directory.Packages.props` pins the versions.
- `InfrastructurePathMatcher` is in `DcsvIo.D2.AspNetCore`, the single source for the path set. The level callback of
  `UseD2RequestLogging` and the AspNetCore-instrumentation `Filter` of `DcsvIo.D2.Telemetry` both use that public
  matcher, so both apply the same path set (`/health`, `/alive`, `/metrics`, `/.well-known`) without duplicating it in
  each library.
- The integration tests pin the absent PII fields. `RequestContextEnricherIntegrationTests` enumerates every
  `IRequestContext` NOT-LOGGED field (`ClientIp`, `City`, `Region`, `SubdivisionCode`, `PostalCode`, `Latitude`,
  `Longitude`, `Geohash`) and asserts that none of them appear in the rendered JSON output. Adding a PII field to the
  spec without a coverage update fails this test.
