// -----------------------------------------------------------------------
// <copyright file="TelemetryServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using DcsvIo.D2.Telemetry.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

/// <summary>
/// OTel SDK builds singleton <c>MeterProvider</c> / <c>TracerProvider</c>
/// per process, so these cases run in a dedicated collection that serializes
/// against integration tests touching the same surface. The OTEL_SDK_DISABLED
/// gate that decides what AddD2Telemetry registers is pinned per test rather
/// than read from the process environment.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class TelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Telemetry_NullServices_Throws()
    {
        IServiceCollection? services = null;
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var act = () => services!.AddD2Telemetry(cfg);

            act.Should().Throw<ArgumentNullException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        WithCleanEnv(() =>
        {
            var act = () => services.AddD2Telemetry(null!);

            act.Should().Throw<ArgumentNullException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var ret = services.AddD2Telemetry(cfg);

            ret.Should().BeSameAs(services);
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersOptions_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<IOptions<D2TelemetryOptions>>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersMeterProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<MeterProvider>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_RegistersTracerProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<TracerProvider>().Should().NotBeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            var act = () =>
            {
                services.AddD2Telemetry(cfg);
                services.AddD2Telemetry(cfg);
            };

            act.Should().NotThrow();
        });
    }

    [Fact]
    public void AddD2Telemetry_ServiceNameFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("default-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY] = "from-config",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.ServiceName.Should().Be("from-config");
        });
    }

    [Fact]
    public void AddD2Telemetry_ServiceNameFallsBackToHostEnvironment_WhenConfigAbsent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.ServiceName.Should().Be("env-app");
        });
    }

    [Fact]
    public void AddD2Telemetry_ConfigureCallback_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("env-app"));
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY] = "from-config",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "explicit-override");
            using var sp = services.BuildServiceProvider();
            var resolved = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            resolved.ServiceName.Should().Be("explicit-override");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpTracesEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/traces",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpTracesEndpoint.Should().Be("https://otlp.example.com/v1/traces");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpMetricsEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/metrics",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpMetricsEndpoint.Should().Be("https://otlp.example.com/v1/metrics");
        });
    }

    [Fact]
    public void AddD2Telemetry_OtlpLogsEndpointFromConfig_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY] =
                    "https://otlp.example.com/v1/logs",
            })
            .Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            opts.OtlpLogsEndpoint.Should().Be("https://otlp.example.com/v1/logs");
        });
    }

    [Fact]
    public void AddD2Telemetry_NoServiceNameAvailable_ValidationFailsOnResolve()
    {
        var services = new ServiceCollection();

        // No IHostEnvironment registered, no config, no configure callback.
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg);

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_EmptyExcludedPaths_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.InstrumentationExcludedPaths))!;
                prop.SetValue(opts, Array.Empty<string>());
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_ExcludedPathsContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.InstrumentationExcludedPaths))!;
                prop.SetValue(opts, new[] { "/health", "   " });
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_AdditionalActivitySourcesContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                var prop = t.GetProperty(
                    nameof(D2TelemetryOptions.AdditionalActivitySources))!;
                prop.SetValue(opts, new[] { "Example.Orders", "  " });
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_AdditionalMetersContainsWhitespace_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "x");
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                opts.AdditionalMeters.Add("Example.Orders");
                opts.AdditionalMeters.Add(string.Empty);
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    [Fact]
    public void AddD2Telemetry_InvalidOtlpTracesEndpoint_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
                opts.OtlpTracesEndpoint = "not a uri";
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>();
        });
    }

    // On Linux and macOS, Uri.TryCreate reads a rooted path such as
    // "/v1/traces" as the absolute URI file:///v1/traces; on Windows it does
    // not parse as absolute. The file: and ftp: rows parse as absolute on every OS.
    [Theory]
    [InlineData("traces", "/v1/traces")]
    [InlineData("traces", "file:///v1/traces")]
    [InlineData("traces", "ftp://otlp.example.com/v1/traces")]
    [InlineData("metrics", "/v1/metrics")]
    [InlineData("metrics", "file:///v1/metrics")]
    [InlineData("logs", "/v1/logs")]
    [InlineData("logs", "file:///v1/logs")]
    public void AddD2Telemetry_OtlpEndpointThatIsNotAnHttpUrl_ValidationFails(
        string signal,
        string endpoint)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
                SetOtlpEndpoint(opts, signal, endpoint);
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                return sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;
            };

            act.Should().Throw<OptionsValidationException>()
                .WithMessage($"*{OtlpEndpointPropertyName(signal)}*");
        });
    }

    [Theory]
    [InlineData("traces")]
    [InlineData("metrics")]
    [InlineData("logs")]
    public void AddD2Telemetry_OtlpEndpointThatIsAnHttpUrl_PassesValidation(string signal)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();
        var endpoint = $"http://localhost:4318/v1/{signal}";

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
                SetOtlpEndpoint(opts, signal, endpoint);
            });
            using var sp = services.BuildServiceProvider();

            var opts = sp.GetRequiredService<IOptions<D2TelemetryOptions>>().Value;

            ReadOtlpEndpoint(opts, signal).Should().Be(endpoint);
        });
    }

    [Fact]
    public void AddD2Telemetry_OtelSdkDisabled_ShortCircuits_NoProviders()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithGateValue("true", () =>
        {
            services.AddD2Telemetry(cfg);
            using var sp = services.BuildServiceProvider();

            sp.GetService<MeterProvider>().Should().BeNull();
            sp.GetService<TracerProvider>().Should().BeNull();
            sp.GetService<IOptions<D2TelemetryOptions>>().Should().BeNull();
        });
    }

    [Fact]
    public void AddD2Telemetry_OtelSdkDisabled_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithGateValue("true", () =>
        {
            var ret = services.AddD2Telemetry(cfg);

            ret.Should().BeSameAs(services);
        });
    }

    [Fact]
    public void AddD2Telemetry_DisableEverySwitch_StillBuilds()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        IConfiguration cfg = new ConfigurationBuilder().Build();

        WithCleanEnv(() =>
        {
            services.AddD2Telemetry(cfg, opts =>
            {
                opts.ServiceName = "x";
            });
            services.PostConfigure<D2TelemetryOptions>(opts =>
            {
                var t = opts.GetType();
                t.GetProperty(nameof(D2TelemetryOptions.EnableAspNetCoreInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableHttpClientInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(
                    nameof(D2TelemetryOptions.EnableGrpcNetClientInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableProcessInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnableRuntimeInstrumentation))!
                    .SetValue(opts, false);
                t.GetProperty(nameof(D2TelemetryOptions.EnablePrometheusExporter))!
                    .SetValue(opts, false);
            });

            var act = () =>
            {
                using var sp = services.BuildServiceProvider();
                _ = sp.GetService<MeterProvider>();
                _ = sp.GetService<TracerProvider>();
            };

            act.Should().NotThrow();
        });
    }

    private static void WithCleanEnv(Action body) => WithGateValue(null, body);

    private static void SetOtlpEndpoint(D2TelemetryOptions opts, string signal, string endpoint)
    {
        switch (signal)
        {
            case "traces":
                opts.OtlpTracesEndpoint = endpoint;
                break;
            case "metrics":
                opts.OtlpMetricsEndpoint = endpoint;
                break;
            case "logs":
                opts.OtlpLogsEndpoint = endpoint;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(signal), signal, null);
        }
    }

    private static string? ReadOtlpEndpoint(D2TelemetryOptions opts, string signal) => signal switch
    {
        "traces" => opts.OtlpTracesEndpoint,
        "metrics" => opts.OtlpMetricsEndpoint,
        "logs" => opts.OtlpLogsEndpoint,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    private static string OtlpEndpointPropertyName(string signal) => signal switch
    {
        "traces" => nameof(D2TelemetryOptions.OtlpTracesEndpoint),
        "metrics" => nameof(D2TelemetryOptions.OtlpMetricsEndpoint),
        "logs" => nameof(D2TelemetryOptions.OtlpLogsEndpoint),
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    /// <summary>
    /// Runs <paramref name="body"/> with the <c>OTEL_SDK_DISABLED</c> gate
    /// reading <paramref name="raw"/> for this test's flow only, so the test
    /// neither reads nor writes the process environment.
    /// </summary>
    private static void WithGateValue(string? raw, Action body)
    {
        using var pin = OtelSdkDisabledGate.Pin(raw);

        body();
    }

    /// <summary>
    /// Minimal <see cref="IHostEnvironment"/> stub.
    /// </summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(
            string applicationName = "test-app",
            string environmentName = "Test")
        {
            ApplicationName = applicationName;
            EnvironmentName = environmentName;
            ContentRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider =
                new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        public string ApplicationName { get; set; }

        public string EnvironmentName { get; set; }

        public string ContentRootPath { get; set; }

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider
        {
            get;
            set;
        }
    }
}
