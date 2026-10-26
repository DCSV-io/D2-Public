// -----------------------------------------------------------------------
// <copyright file="MutualTlsHostExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Host wiring for mutual-TLS client-certificate require-and-validate. When
/// <see cref="D2MutualTlsOptions.Enabled"/>, Kestrel's HTTPS endpoint is configured
/// to require a client certificate and to validate it with the default-deny
/// <see cref="SpiffeSanPeerValidator"/>. The Kestrel configuration logic lives
/// here, in <c>DcsvIo.D2.AspNetCore</c>.
/// </summary>
public static class MutualTlsHostExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires mutual-TLS client-certificate require-and-validate into the host's
        /// Kestrel HTTPS endpoint. When <see cref="D2MutualTlsOptions.Enabled"/>,
        /// every HTTPS connection must present a client certificate that the
        /// default-deny <see cref="SpiffeSanPeerValidator"/> accepts (it chains to the
        /// internal CA, the SPIFFE trust domain matches, and the workload is in the
        /// allowed set). When disabled, no Kestrel client-certificate configuration
        /// is added.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>An empty allowed set admits nobody.</b> The options are validated at
        /// host build via <c>ValidateOnStart()</c>. When
        /// <see cref="D2MutualTlsOptions.Enabled"/> is true,
        /// <see cref="D2MutualTlsOptions.TrustAnchorsProvider"/> must be set, and
        /// every entry of <see cref="D2MutualTlsOptions.AllowedWorkloads"/> must be
        /// non-empty and canonical - lowercase, no whitespace - or the host throws,
        /// and the exception message identifies each offending entry. An empty list
        /// is valid: the host boots, the port keeps requiring a client certificate,
        /// and the validator admits no peer.
        /// </para>
        /// <para>
        /// <b>Connection-level concern.</b> Client-certificate validation runs at
        /// the TLS handshake before any middleware, so the wiring happens entirely
        /// in the service-collection phase via <see cref="KestrelServerOptions"/> -
        /// there is no <c>IApplicationBuilder</c> pipeline step.
        /// </para>
        /// <para>
        /// <b>Composes with other HTTPS configuration.</b> The validation callback is
        /// set through an <see cref="IConfigureOptions{TOptions}"/> for
        /// <see cref="KestrelServerOptions"/>, so it composes with the host's other
        /// HTTPS configuration; it never calls <c>AllowAnyClientCertificate()</c>
        /// (which would discard the callback).
        /// </para>
        /// </remarks>
        /// <param name="configure">Configuration delegate for <see cref="D2MutualTlsOptions"/>.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
        /// </exception>
        public IServiceCollection AddD2MutualTls(Action<D2MutualTlsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<D2MutualTlsOptions>()
                .Configure(configure)
                .ValidateOnStart();

            // One validator rather than a chain of predicates. The validator
            // collects every offending entry, so the failure lists all of them
            // and each message includes the entry it is about.
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<D2MutualTlsOptions>, MutualTlsOptionsValidator>());

            // The validator is built from the options snapshot. It is a
            // singleton because it is stateless given the resolved options.
            services.TryAddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value;
                var logger = sp.GetRequiredService<
                    Microsoft.Extensions.Logging.ILogger<SpiffeSanPeerValidator>>();

                return new SpiffeSanPeerValidator(options, logger);
            });

            // Configure Kestrel's HTTPS default at options-build time via an
            // IConfigureOptions so the validator + options resolve from DI. The
            // callback requires and validates a client cert only when Enabled, so
            // a disabled host's HTTPS endpoint is untouched (safe by default).
            services.AddSingleton<IConfigureOptions<KestrelServerOptions>, MutualTlsKestrelConfigure>();

            return services;
        }
    }

    /// <summary>
    /// Validates <see cref="D2MutualTlsOptions"/> at host build. A disabled host
    /// passes unconditionally; an enabled one must supply trust anchors and a
    /// canonical allowed-workload list, which may be empty.
    /// </summary>
    private sealed class MutualTlsOptionsValidator : IValidateOptions<D2MutualTlsOptions>
    {
        public ValidateOptionsResult Validate(string? name, D2MutualTlsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!options.Enabled)
                return ValidateOptionsResult.Success;

            var failures = new List<string>();

            for (var i = 0; i < options.AllowedWorkloads.Count; i++)
            {
                var workload = options.AllowedWorkloads[i];

                if (workload.Falsey())
                {
                    failures.Add(
                        $"D2MutualTlsOptions.AllowedWorkloads[{i}] is empty or whitespace.");

                    continue;
                }

                if (!IsCanonical(workload))
                {
                    failures.Add(
                        $"D2MutualTlsOptions.AllowedWorkloads[{i}] is '{workload}', which is not "
                        + "canonical. An entry is lowercase and carries no whitespace, because "
                        + "the peer validator compares it ordinally against a SPIFFE service id "
                        + "that is already lowercase - this entry matches no peer.");
                }
            }

            if (options.TrustAnchorsProvider is null)
            {
                failures.Add(
                    "D2MutualTlsOptions.TrustAnchorsProvider must be set when Enabled is true "
                    + "(the host supplies the public internal CA trust anchor the peer "
                    + "certificate chains to).");
            }

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        private static bool IsCanonical(string workload)
        {
            foreach (var character in workload)
            {
                if (char.IsWhiteSpace(character) || char.IsUpper(character))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Configures Kestrel's HTTPS defaults to require and validate a client
    /// certificate when mutual-TLS is enabled. Registered against
    /// <see cref="IConfigureOptions{TOptions}"/> for <see cref="KestrelServerOptions"/>
    /// so the resolved <see cref="D2MutualTlsOptions"/> + <see cref="SpiffeSanPeerValidator"/>
    /// are available at configure time.
    /// </summary>
    private sealed class MutualTlsKestrelConfigure : IConfigureOptions<KestrelServerOptions>
    {
        private readonly IOptions<D2MutualTlsOptions> r_options;
        private readonly SpiffeSanPeerValidator r_validator;

        public MutualTlsKestrelConfigure(
            IOptions<D2MutualTlsOptions> options, SpiffeSanPeerValidator validator)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(validator);

            r_options = options;
            r_validator = validator;
        }

        public void Configure(KestrelServerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!r_options.Value.Enabled)
                return;

            options.ConfigureHttpsDefaults(https =>
            {
                // RequireCertificate so a connection without a certificate is
                // rejected at the handshake - the peer must present a client
                // certificate.
                https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

                // Default-deny: the callback returns true only when the validator's
                // result is Ok. The cert + chain are borrowed from Kestrel and are
                // never disposed here. The chain contains the peer-presented
                // intermediate that the validator seeds into its root-anchored
                // rebuild. Never use AllowAnyClientCertificate (it discards this
                // callback). SslPolicyErrors is intentionally not consulted: the
                // validator rebuilds the chain against the configured trust anchors
                // with CustomRootTrust, which supersedes the machine-store result.
                // Do not restore the default-policy check.
                https.ClientCertificateValidation =
                    (cert, chain, _) => r_validator.Validate(cert, chain).Success;
            });
        }
    }
}
