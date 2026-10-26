// -----------------------------------------------------------------------
// <copyright file="ActorEntryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class ActorEntryTests
{
    [Fact]
    public void Construction_MinimalServiceEntry_DefaultsAllOptionalFieldsToNull()
    {
        const string subject = "auth-svc";

        var entry = new ActorEntry(ActorKind.Service, subject);

        entry.Kind.Should().Be(ActorKind.Service);
        entry.Subject.Should().Be(subject);
        entry.ClientId.Should().BeNull();
        entry.ImpersonationKind.Should().BeNull();
        entry.SessionId.Should().BeNull();
        entry.OrgId.Should().BeNull();
        entry.OrgName.Should().BeNull();
        entry.OrgType.Should().BeNull();
        entry.OrgRole.Should().BeNull();
        entry.Act.Should().BeNull();
    }

    [Fact]
    public void Construction_ImpersonationEntry_AllFieldsRoundTrip()
    {
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var orgId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var entry = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent-user-id",
            ImpersonationKind: ImpersonationKind.Consent,
            SessionId: sessionId,
            OrgId: orgId,
            OrgName: "Customer Support",
            OrgType: OrgType.Support,
            OrgRole: Role.Officer);

        entry.Kind.Should().Be(ActorKind.Impersonation);
        entry.Subject.Should().Be("agent-user-id");
        entry.ImpersonationKind.Should().Be(ImpersonationKind.Consent);
        entry.SessionId.Should().Be(sessionId);
        entry.OrgId.Should().Be(orgId);
        entry.OrgName.Should().Be("Customer Support");
        entry.OrgType.Should().Be(OrgType.Support);
        entry.OrgRole.Should().Be(Role.Officer);
    }

    [Fact]
    public void RecordEquality_IdenticalFields_AreEqual()
    {
        var sessionId = Guid.NewGuid();

        var a = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            ImpersonationKind: ImpersonationKind.Force,
            SessionId: sessionId,
            OrgType: OrgType.Admin,
            OrgRole: Role.Owner);

        var b = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            ImpersonationKind: ImpersonationKind.Force,
            SessionId: sessionId,
            OrgType: OrgType.Admin,
            OrgRole: Role.Owner);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void RecordEquality_DifferentKind_NotEqual()
    {
        var a = new ActorEntry(ActorKind.Service, Subject: "x");
        var b = new ActorEntry(ActorKind.Impersonation, Subject: "x");

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_ServiceKindCarriesImpersonationFields_StillRecordsThem()
    {
        // Adversarial: the XML doc of ActorEntry says the ImpersonationKind,
        // SessionId and Org* fields are only meaningful when Kind is
        // Impersonation, but record
        // equality still compares them. Two Service entries that differ only
        // in those fields are not equal: the record carries the fields, and
        // the contract does not normalize them. This documents the actual
        // behavior: callers must not populate impersonation fields on Service
        // entries unless they want those fields to take part in equality.
        var withSession = new ActorEntry(
            ActorKind.Service,
            Subject: "svc",
            SessionId: Guid.NewGuid());

        var withoutSession = new ActorEntry(ActorKind.Service, Subject: "svc");

        withSession.Should().NotBe(withoutSession);
    }

    [Fact]
    public void With_OverrideField_ProducesDistinctRecord()
    {
        var original = new ActorEntry(ActorKind.Service, Subject: "svc-a");

        var modified = original with { Subject = "svc-b" };

        modified.Should().NotBe(original);
        modified.Subject.Should().Be("svc-b");
        original.Subject.Should().Be("svc-a", "with-expression must not mutate original");
    }

    [Fact]
    public void NestedAct_ThreeHopChain_TraversableViaActProperty()
    {
        // Adversarial: build a 3-hop nested actor chain (RFC 8693 section 2.1
        // recursion) and verify that each hop is reachable and holds its own
        // data.
        var earliest = new ActorEntry(ActorKind.Service, Subject: "originating-svc");
        var middle = new ActorEntry(
            ActorKind.Service,
            Subject: "middle-svc",
            Act: earliest);
        var outermost = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent-id",
            ImpersonationKind: ImpersonationKind.Consent,
            Act: middle);

        outermost.Act.Should().Be(middle);
        outermost.Act!.Act.Should().Be(earliest);
        outermost.Act.Act!.Act.Should().BeNull("earliest entry has no further nesting");

        // Walk the chain in a loop and count its entries.
        var current = outermost;
        var depth = 0;
        while (current is not null)
        {
            depth++;
            current = current.Act;
        }

        depth.Should().Be(3);
    }

    [Fact]
    public void RecordEquality_NestedActDifferent_NotEqual()
    {
        // Adversarial: two outermost entries with identical surface fields but
        // different nested Act subtrees must not be equal, because
        // OriginatingClientId is derived from the chain shape, per RFC 8693.
        var nestedA = new ActorEntry(ActorKind.Service, Subject: "deep-a");
        var nestedB = new ActorEntry(ActorKind.Service, Subject: "deep-b");

        var outerA = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            Act: nestedA);

        var outerB = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            Act: nestedB);

        outerA.Should().NotBe(outerB);
    }

    [Fact]
    public void RecordEquality_BothNestedNullVsBothNestedSame_AreEqual()
    {
        // Baseline for the "different Act -> different record" test: entries
        // with the same Act are equal.
        var noActA = new ActorEntry(ActorKind.Service, Subject: "svc");
        var noActB = new ActorEntry(ActorKind.Service, Subject: "svc");

        noActA.Should().Be(noActB);

        var nested = new ActorEntry(ActorKind.Service, Subject: "deep");
        var withActA = new ActorEntry(ActorKind.Service, Subject: "svc", Act: nested);
        var withActB = new ActorEntry(ActorKind.Service, Subject: "svc", Act: nested);

        withActA.Should().Be(withActB);
    }

    [Fact]
    public void With_OverrideAct_ProducesDifferentRecord()
    {
        var nested = new ActorEntry(ActorKind.Service, Subject: "deep");
        var original = new ActorEntry(ActorKind.Impersonation, Subject: "agent");

        var modified = original with { Act = nested };

        modified.Should().NotBe(original);
        modified.Act.Should().Be(nested);
    }

    /// <summary>
    /// A plain <c>{x}</c> log capture, an interpolation and a printed
    /// <c>IAuthContext.ActorChain</c> element each call <c>ToString</c>. The
    /// destructuring policy never sees those calls, so the record's own
    /// <c>ToString</c> must print the impersonation session id masked.
    /// </summary>
    [Fact]
    public void ToString_ImpersonationEntry_MasksTheSessionId()
    {
        var sessionId = Guid.Parse("5c2b8d16-7e40-4a93-b6f2-0d3a9c81e75b");
        var entry = new ActorEntry(
            ActorKind.Impersonation,
            "agent-1",
            ImpersonationKind: ImpersonationKind.Consent,
            SessionId: sessionId);

        var printed = entry.ToString();

        printed.Should().NotContain(sessionId.ToString());
        printed.Should().Contain("SessionId = [REDACTED: SecretInformation]");
    }

    [Fact]
    public void ToString_NestedActor_MasksItsSessionIdToo()
    {
        var nestedSession = Guid.Parse("0d3a9c81-e75b-4a93-b6f2-5c2b8d167e40");
        var entry = new ActorEntry(
            ActorKind.Service,
            "edge",
            Act: new ActorEntry(ActorKind.Impersonation, "agent-1", SessionId: nestedSession));

        entry.ToString().Should().NotContain(nestedSession.ToString());
    }

    /// <summary>
    /// Masking replaces one value and keeps the rest of the chain readable:
    /// every member is printed by name, so this test fails when a member added
    /// later is left out of the printing.
    /// </summary>
    [Fact]
    public void ToString_PrintsEveryMemberByName()
    {
        var entry = new ActorEntry(
            ActorKind.Impersonation,
            "agent-1",
            ClientId: "client-1",
            ImpersonationKind: ImpersonationKind.Force,
            SessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OrgId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OrgName: "Customer Support",
            OrgType: OrgType.Support,
            OrgRole: Role.Agent,
            Act: new ActorEntry(ActorKind.Service, "edge"));

        var printed = entry.ToString();

        foreach (var property in typeof(ActorEntry).GetProperties())
            printed.Should().Contain(property.Name + " = ");

        printed.Should().StartWith("ActorEntry { Kind = Impersonation, Subject = agent-1, ClientId = client-1");
        printed.Should().Contain("OrgName = Customer Support");
        printed.Should().Contain("OrgId = 22222222-2222-2222-2222-222222222222");
        printed.Should().Contain("Act = ActorEntry { Kind = Service, Subject = edge");
    }

    /// <summary>
    /// An entry with no impersonation session prints the member empty, as a
    /// compiler-generated record <c>ToString</c> prints a null member, and not
    /// as a mask that suggests a value.
    /// </summary>
    [Fact]
    public void ToString_NoSessionId_PrintsTheMemberEmpty()
        => new ActorEntry(ActorKind.Service, "edge").ToString()
            .Should().Contain("SessionId = ,");
}
