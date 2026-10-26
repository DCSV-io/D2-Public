// -----------------------------------------------------------------------
// <copyright file="LoggerMessageParameterContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Tiered;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Result.Grpc;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Enumerates every <c>[LoggerMessage]</c> partial method in the log-emitting
/// assemblies under test and asserts two things of every parameter: that none
/// takes a raw <see cref="Exception"/>, and that none takes a type holding a
/// live credential. An exception message carries broker URIs, gRPC status
/// detail, connection strings and user input, so a delegate that accepts one
/// writes all of that to every sink. Callers pass
/// <c>SanitizedExceptionRender.TypeName(ex)</c> and <c>FirstFrame(ex)</c> as
/// separate strings instead. A parameter of a credential-holding type, such as
/// <c>ForwardedJwt</c>, puts the bearer bytes in every sink.
/// </summary>
/// <remarks>
/// The per-type contract tests beside this one cover one surface each and do
/// not cover a log class added later. This one reflects over the assemblies,
/// so a delegate added to a class that has no test of its own is still held to
/// the contract. It reaches internal log classes through
/// <c>Assembly.GetTypes</c>, which does not need compile-time access to them.
/// </remarks>
public sealed class LoggerMessageParameterContractTests
{
    /// <summary>
    /// The delegates that take an exception on purpose. Each is a fault sink at
    /// the top of a background task: the task has already failed, nothing below
    /// it will run, and the whole exception is the only record of why. No
    /// caller-supplied value reaches either one. Both are invoked from a
    /// continuation on a hosted service's own startup task, with no request,
    /// no message and no user input in scope.
    /// </summary>
    private static readonly HashSet<string> sr_faultSinks = new(StringComparer.Ordinal)
    {
        "DcsvIo.D2.Messaging.RabbitMq.Subscribing.SubscriberLog.HostStartupFaulted",
        "DcsvIo.D2.Messaging.RabbitMq.Topology.TopologyLog.DeclarationFailedFaultSink",
    };

    public static TheoryData<string> LogEmittingAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (var assembly in Assemblies())
            data.Add(assembly.GetName().Name!);

        return data;
    }

    [Theory]
    [MemberData(nameof(LogEmittingAssemblies))]
    public void EveryLoggerMessageDelegate_TakesNoRawException(string assemblyName)
    {
        var assembly = Assemblies().Single(a => a.GetName().Name == assemblyName);

        var offenders = LoggerMessageMethodsIn(assembly)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(FullNameOf)
            .Where(name => !sr_faultSinks.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "an exception message carries broker URIs, gRPC status detail and user input; "
            + "pass SanitizedExceptionRender.TypeName(ex) and FirstFrame(ex) as strings. "
            + "Offending delegates: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheEnumeration_ReachesEveryLogSurfaceIncludingTheInternalOnes()
    {
        // A guard that enumerated nothing would pass every case above.
        // ForgeryProtectionLog and OutboxLog are internal and have no test of
        // their own, which is the gap this file closes, so this test checks
        // that the enumeration reaches them.
        var reached = Assemblies()
            .SelectMany(LoggerMessageMethodsIn)
            .Select(m => m.DeclaringType!.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        reached.Should().Contain("DcsvIo.D2.AspNetCore.Forgery.ForgeryProtectionLog");
        reached.Should().Contain(
            "DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres.OutboxLog");
        reached.Should().Contain("DcsvIo.D2.Result.Grpc.ProtoExtensions");
    }

    [Fact]
    public void EveryNamedFaultSink_StillExistsAndStillTakesTheException()
    {
        // An allowed delegate that no longer exists, or that no longer takes
        // an exception, would leave a gap in the guard that no other test
        // reports.
        var takingAnException = Assemblies()
            .SelectMany(LoggerMessageMethodsIn)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(FullNameOf)
            .ToHashSet(StringComparer.Ordinal);

        takingAnException.Should().BeEquivalentTo(sr_faultSinks);
    }

    [Theory]
    [MemberData(nameof(LogEmittingAssemblies))]
    public void EveryLoggerMessageDelegate_TakesNoCredentialHoldingType(string assemblyName)
    {
        var assembly = Assemblies().Single(a => a.GetName().Name == assemblyName);
        var credentialHolding = CredentialHoldingTypes();

        var offenders = LoggerMessageMethodsIn(assembly)
            .Where(m => m.GetParameters().Any(p => IsCredentialHolding(
                p.ParameterType,
                credentialHolding)))
            .Select(FullNameOf)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a credential-holding type written as a log parameter puts the credential "
            + "itself in every sink. Offending delegates: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheCredentialTypeSet_IsDerivedFromTheDeclaredRedactionAndReachesForwardedJwt()
    {
        // A set that discovered nothing would pass the case above whatever the
        // delegates take. The set is read from the declaration that a
        // credential type already has, so a new credential type in a covered
        // assembly joins the set without an edit to this file.
        var credentialHolding = CredentialHoldingTypes();

        credentialHolding.Should().Contain(typeof(ForwardedJwt));
    }

    /// <summary>
    /// Every type in the enumerated assemblies that declares itself, or one of
    /// its own members, as holding secret information.
    /// </summary>
    /// <returns>The credential-holding types.</returns>
    private static HashSet<Type> CredentialHoldingTypes() =>
        Assemblies()
            .SelectMany(a => a.GetTypes())
            .Where(DeclaresSecretInformation)
            .ToHashSet();

    private static bool DeclaresSecretInformation(Type type)
    {
        if (IsSecretInformation(type.GetCustomAttributes(typeof(RedactDataAttribute), false)))
            return true;

        const BindingFlags members = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        return type.GetMembers(members).Any(
            m => IsSecretInformation(m.GetCustomAttributes(typeof(RedactDataAttribute), false)));
    }

    private static bool IsSecretInformation(object[] attributes) =>
        attributes.OfType<RedactDataAttribute>()
            .Any(a => a.Reason == RedactReason.SecretInformation);

    private static bool IsCredentialHolding(Type parameter, HashSet<Type> credentialHolding)
    {
        var candidate = Nullable.GetUnderlyingType(parameter) ?? parameter;

        return credentialHolding.Contains(candidate)
            || credentialHolding.Any(t => t.IsAssignableFrom(candidate));
    }

    private static IEnumerable<MethodInfo> LoggerMessageMethodsIn(Assembly assembly) =>
        assembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes(typeof(LoggerMessageAttribute), false).Length > 0);

    private static string FullNameOf(MethodInfo method) =>
        method.DeclaringType!.FullName + "." + method.Name;

    private static Assembly[] Assemblies() =>
    [
        typeof(D2AspNetCoreConstants).Assembly,
        typeof(ActorEntry).Assembly,
        typeof(RedisDistributedCache).Assembly,
        typeof(DefaultTieredCache).Assembly,
        typeof(MutableRequestContext).Assembly,
        typeof(AnonymizationEngine).Assembly,
        typeof(EncryptionStartupCheck).Assembly,
        typeof(PgAdvisoryLock).Assembly,
        typeof(HandlerTelemetry).Assembly,
        typeof(MessagingRabbitMqServiceCollectionExtensions).Assembly,
        typeof(OutboxRelayPostgresServiceCollectionExtensions).Assembly,
        typeof(ProtoExtensions).Assembly,
        typeof(DcsvIo.D2.Logging.Internal.LogSafeActorEntry).Assembly,
    ];
}
