// -----------------------------------------------------------------------
// <copyright file="RegistrySpecEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

/// <summary>
/// A single error-code entry enriched with registry-level fields. It is the
/// source-generator counterpart of the runtime <c>ErrorCodeInfo</c> record that
/// the registry emitter produces. It holds the spec fields plus the registry
/// domain, owner, full DomainPrefix and deprecation.
/// </summary>
/// <param name="Code">Wire-format error code (SCREAMING_SNAKE).</param>
/// <param name="HttpStatus">HTTP status mapping for transport.</param>
/// <param name="Category">
/// Closed semantic/telemetry classification from the spec
/// (for example <c>validation_failure</c>, <c>not_found</c>).
/// </param>
/// <param name="UserMessageKey">
/// TK symbol-path reference from the spec
/// (for example <c>TK.Auth.Errors.UNAUTHORIZED</c>).
/// </param>
/// <param name="FactoryName">
/// PascalCase factory name from the spec (for example <c>BearerMissing</c>).
/// </param>
/// <param name="FactoryShape">
/// Closed factory-shape value from the spec
/// (<c>standard</c> - the universal error-factory shape - or <c>none</c>).
/// </param>
/// <param name="Doc">XML summary / JSDoc text for this code.</param>
/// <param name="Domain">
/// Business domain token (for example <c>common</c>, <c>auth</c>, <c>billing</c>),
/// read from the catalog's declared identity rather than the filename stem, so
/// a catalog file whose name contains more than the domain still resolves to the
/// one token the catalog declares.
/// </param>
/// <param name="Owner">
/// Closed owner token: <c>gen</c> | <c>lib</c> | <c>dom</c>.
/// </param>
/// <param name="DomainPrefix">
/// Full enforced wire prefix including owner (for example <c>GEN_</c>,
/// <c>LIB_AUTH_</c>, <c>DOM_AUTH_</c>).
/// </param>
/// <param name="SpecFileName">
/// The spec filename this entry was loaded from. Used for collision
/// diagnostics.
/// </param>
/// <param name="IsDeprecated">
/// <see langword="true"/> when the spec entry carries the deprecation marker
/// (<c>"deprecated": true</c>). Exposed on the runtime <c>ErrorCodeInfo</c> as its
/// deprecation state. Defaults to
/// <see langword="false"/> (active).
/// </param>
internal sealed record RegistrySpecEntry(
    string Code,
    int HttpStatus,
    string Category,
    string UserMessageKey,
    string FactoryName,
    string FactoryShape,
    string Doc,
    string Domain,
    string Owner,
    string DomainPrefix,
    string SpecFileName,
    bool IsDeprecated = false);
