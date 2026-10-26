// -----------------------------------------------------------------------
// <copyright file="PropertySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

/// <summary>
/// One property declaration within a <see cref="Section"/>. Renders as an
/// interface property and a corresponding mutable property on the concrete class.
/// A derived property is a computed getter on the concrete class.
/// </summary>
/// <param name="Name">PascalCase property name (for example <c>"OrgId"</c>).</param>
/// <param name="Type">
/// Type string from the closed vocabulary (for example <c>"string?"</c>, <c>"Guid?"</c>
/// or <c>"IReadOnlyList&lt;ActorEntry&gt;"</c>). The emitter validates
/// against an allow-list and emits <c>D2CTX002</c> on unknown types.
/// </param>
/// <param name="Claim">
/// JWT claim name this property maps to (used by FromClaims / FromJwtPayload
/// factories). Null for properties not sourced from JWT (for example transport-level
/// fields on IRequestContext, or derived properties).
/// </param>
/// <param name="ClaimForm">
/// How the claim's value reaches this property. Null is the default form for
/// the property's type. <c>"verbatimSet"</c> applies to an
/// <c>IReadOnlySet&lt;string&gt;</c> whose elements are copied unchanged,
/// including one that is not a string and one that is whitespace, so the
/// conversion of the set can reject it whole instead of producing a silently
/// smaller one. The default set form is the
/// RFC 6749 section 3.3 scope reading, which drops what it cannot use. A claim
/// present but empty leaves the property unassigned on both reading paths: the
/// ClaimsPrincipal path receives an empty array as no claim at all, because
/// ASP.NET expands an array claim into one claim entry per element.
/// <c>"spaceJoined"</c> applies to a <c>string?</c> whose claim is an array of strings,
/// such as RFC 8176 <c>amr</c>: both reading paths join every non-empty string element
/// with one space, and a single string value is read as it is.
/// </param>
/// <param name="TrinaryAuth">
/// True for <c>bool?</c> properties whose null-versus-false distinction is
/// meaningful (pre-auth versus confirmed-not). The emitted property defaults to
/// null, the same as default(bool?). The generator parses the flag but
/// does not read it when emitting, so it makes no difference at codegen time and
/// documents the property.
/// </param>
/// <param name="Derived">
/// Marks this property as computed from other properties at read time. The
/// named rule is implemented by the generator's <c>MutableEmitter</c>.
/// Currently recognized: <c>"actorChain"</c> (reads the actor chain to
/// compute the impersonation kind, the impersonator org and similar properties).
/// </param>
/// <param name="Default">
/// Default-value expression (raw C# literal). Examples: <c>"[]"</c> for
/// collection-expression empty defaults, <c>"null"</c> for explicit null.
/// </param>
/// <param name="Doc">
/// XML doc <c>&lt;summary&gt;</c> text rendered on the generated property.
/// </param>
/// <param name="Propagate">
/// When true, this property is included in the codegen-emitted
/// <c>PropagatedContext</c> record (the cross-hop subset that ships in the
/// <c>x-d2-context</c> AMQP / gRPC / HTTP header). Identity fields
/// (UserId / OrgId / Scopes / ActorChain) must not be propagated; each
/// synchronous hop rebuilds them from the JWT.
/// </param>
/// <param name="MaxLength">
/// Wire-level per-field length cap, enforced by the codegen-emitted
/// <c>PropagatedContextSerializer.TryDecode</c>. A forged
/// <c>x-d2-context</c> header with any propagatable field exceeding its
/// cap is dropped as a whole, because propagation is never required. Only meaningful
/// when <see cref="Propagate"/> is true. On a string field it is the maximum
/// character length; on a list field it is the maximum entry count.
/// </param>
/// <param name="EntryIdMaxLength">
/// Per-entry id length cap for a propagated list-of-records field (for example
/// <c>CallPath</c>). Bounds a single forged entry id so it cannot bloat log
/// scope keys or audit columns even when the entry count is within
/// <see cref="MaxLength"/>. Single source of the cap: the codegen-emitted
/// <c>PropagatedContextSerializer</c> (both .NET and TypeScript) derives it
/// from this value, and neither hard-codes the number. Only meaningful on a
/// propagated list-of-records field.
/// </param>
/// <param name="Redact">
/// When true, the property is PII-bearing and must be redacted from logs
/// and telemetry. The emitter places <c>[RedactData]</c> on the generated
/// interface property and on the matching property of
/// <c>MutableRequestContext</c>. The TypeScript codegen
/// emits the same property name into a <c>RedactPaths</c> array, so the
/// .NET attribute and the TypeScript paths come from the same spec field.
/// </param>
internal sealed record PropertySpec(
    string Name,
    string Type,
    string? Claim,
    string? ClaimForm,
    bool TrinaryAuth,
    string? Derived,
    string? Default,
    string? Doc,
    bool Propagate,
    int? MaxLength,
    int? EntryIdMaxLength,
    bool Redact);
