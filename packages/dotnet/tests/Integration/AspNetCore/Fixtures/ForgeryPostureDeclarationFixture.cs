// -----------------------------------------------------------------------
// <copyright file="ForgeryPostureDeclarationFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;

using DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// Endpoint metadata declaring a forgery posture, standing in for the record
/// a route-policy generator stamps onto a real registration.
/// </summary>
/// <param name="Posture">The declared posture.</param>
internal sealed record ForgeryPostureDeclarationFixture(string Posture)
    : ID2ForgeryPostureDeclaration;
