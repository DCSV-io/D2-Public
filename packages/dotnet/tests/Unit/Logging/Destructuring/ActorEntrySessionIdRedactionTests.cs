// -----------------------------------------------------------------------
// <copyright file="ActorEntrySessionIdRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Destructuring;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Logging.Destructuring;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Pins that the real policy masks <c>ActorEntry.SessionId</c>, a raw session id
/// nested inside the destructured actor chain, with the SecretInformation reason.
/// </summary>
public sealed class ActorEntrySessionIdRedactionTests
{
    [Fact]
    public void TryDestructure_ActorEntry_MasksSessionIdUnderSecretRegime()
    {
        var policy = new RedactDataDestructuringPolicy();
        var entry = new ActorEntry(
            ActorKind.Impersonation,
            "agent-1",
            ImpersonationKind: ImpersonationKind.Consent,
            SessionId: Guid.Parse("5c2b8d16-7e40-4a93-b6f2-0d3a9c81e75b"));

        var handled = policy.TryDestructure(entry, new PassthroughFactoryStub(), out var result);

        handled.Should().BeTrue("ActorEntry carries a property-level [RedactData] marker");
        var structure = result.Should().BeOfType<StructureValue>().Subject;
        var sessionProperty = structure.Properties
            .Should().ContainSingle(p => p.Name == nameof(ActorEntry.SessionId)).Subject;

        // The reason is SecretInformation, not PersonalInformation: the two
        // are separate redaction reasons.
        sessionProperty.Value.ToString().Should().Contain("REDACTED: SecretInformation");
        sessionProperty.Value.ToString().Should().NotContain("5c2b8d16");
    }

    [Fact]
    public void TryDestructure_ActorEntry_LeavesNonCredentialFieldsIntact()
    {
        var policy = new RedactDataDestructuringPolicy();
        var entry = new ActorEntry(ActorKind.Service, "edge", ClientId: "edge");

        policy.TryDestructure(entry, new PassthroughFactoryStub(), out var result);

        // Redaction leaves the other fields readable, so operators can still
        // read the actor chain.
        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p => p.Name == nameof(ActorEntry.Subject));
        structure.Properties.Should().Contain(p => p.Name == nameof(ActorEntry.Kind));
    }

    private sealed class PassthroughFactoryStub : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(
            object? value,
            bool destructureObjects = false)
            => new ScalarValue(value);
    }
}
