// -----------------------------------------------------------------------
// <copyright file="TKEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Xunit;

public sealed class TKEmitterTests
{
    /// <summary>
    /// Empty map of the other locales, used by call sites that test only the
    /// en-US path. ReSharper cannot tell that the dictionary is intentionally
    /// never written. The suppression is valid because TKEmitter never mutates
    /// the dictionary it receives.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "CollectionNeverUpdated.Local",
        Justification = "Intentionally empty fixture; TKEmitter consumes it read-only.")]
    private static readonly Dictionary<string, string> sr_noOtherLocales = new();

    // ----------------------------------------------------------------------
    // Snapshot - one small fixture
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_SingleKey_GeneratesNestedClassChain()
    {
        const string single_key_json = @"{""common_errors_NOT_FOUND"": ""Not found.""}";

        var result = TKEmitter.Emit(single_key_json, sr_noOtherLocales);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("namespace DcsvIo.D2.I18n;");
        result.GeneratedSource.Should().Contain("public static partial class TK");
        result.GeneratedSource.Should().Contain("public static partial class Common");
        result.GeneratedSource.Should().Contain("public static partial class Errors");
        result.GeneratedSource.Should()
            .Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage NOT_FOUND")
            .And.Contain(@"= new(""common_errors_NOT_FOUND"")");
    }

    [Fact]
    public void Emit_ExtensionNamespaceAndClass_PinsExtensionTypeFqns()
    {
        // The emitter writes the extension class under the namespace the
        // caller passes and never writes a type with the same fully qualified
        // name as DcsvIo.D2.I18n.TK, which would cause CS0433.
        const string single_key_json = @"{""orders_errors_ORDER_INVALID"": ""Order invalid.""}";

        var result = TKEmitter.Emit(
            single_key_json,
            sr_noOtherLocales,
            rootNamespace: "Example.I18n",
            className: "SampleTK");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("namespace Example.I18n;");
        result.GeneratedSource.Should().Contain("public static partial class SampleTK");
        result.GeneratedSource.Should().NotContain("namespace DcsvIo.D2.I18n;");
        result.GeneratedSource.Should().NotContain("public static partial class TK");
        result.GeneratedSource.Should()
            .Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage ORDER_INVALID");
    }

    [Fact]
    public void Emit_SchemaKeyOnly_ProducesEmptyTk()
    {
        // $schema is the JSON Schema reference and not a translation key, so
        // the emitter skips it.
        const string schema_only = @"{""$schema"": ""./schema.json""}";

        var result = TKEmitter.Emit(schema_only, sr_noOtherLocales);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class TK");
        result.GeneratedSource.Should().NotContain("$schema");
    }

    [Fact]
    public void Emit_EmptyJsonObject_ProducesEmptyTk()
    {
        var result = TKEmitter.Emit("{}", sr_noOtherLocales);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class TK");
    }

    [Fact]
    public void Emit_MultipleDomainsAndCategories_GroupsCorrectly()
    {
        const string multi = @"{
            ""common_errors_NOT_FOUND"": ""Not found"",
            ""common_errors_FORBIDDEN"": ""Forbidden"",
            ""common_validation_EMAIL_INVALID"": ""Invalid email"",
            ""auth_errors_INVALID_ROLE"": ""Invalid role""
        }";

        var result = TKEmitter.Emit(multi, sr_noOtherLocales);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage NOT_FOUND")
            .And.Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage FORBIDDEN")
            .And.Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage EMAIL_INVALID")
            .And.Contain("public static readonly global::DcsvIo.D2.I18n.TKMessage INVALID_ROLE");

        // The output contains both the Common and the Auth domain blocks.
        result.GeneratedSource.Should().Contain("public static partial class Common");
        result.GeneratedSource.Should().Contain("public static partial class Auth");

        // Common contains both the Errors and the Validation categories.
        result.GeneratedSource.Should().Contain("public static partial class Errors");
        result.GeneratedSource.Should().Contain("public static partial class Validation");
    }

    [Fact]
    public void Emit_OutputIsDeterministic_RepeatedRunsProduceIdenticalSource()
    {
        // Incremental-generator caching depends on identical input producing
        // identical output. The emitter uses SortedDictionary, which gives the
        // same output for any ordering of the input JSON.
        const string fixture = @"{
            ""z_z_LAST"": ""z"",
            ""a_a_FIRST"": ""a""
        }";

        var first = TKEmitter.Emit(fixture, sr_noOtherLocales);
        var second = TKEmitter.Emit(fixture, sr_noOtherLocales);

        second.GeneratedSource.Should().Be(first.GeneratedSource);
    }

    [Fact]
    public void Emit_KeysInRandomOrder_ProducesAlphabeticalOutput()
    {
        // The emission order does not depend on the input order, which keeps
        // the incremental-generator cache stable.
        const string reverse_order = @"{
            ""z_z_X"": ""z"",
            ""m_m_X"": ""m"",
            ""a_a_X"": ""a""
        }";

        var result = TKEmitter.Emit(reverse_order, sr_noOtherLocales);

        var src = result.GeneratedSource;
        var aPos = src.IndexOf("partial class A", System.StringComparison.Ordinal);
        var mPos = src.IndexOf("partial class M", System.StringComparison.Ordinal);
        var zPos = src.IndexOf("partial class Z", System.StringComparison.Ordinal);

        aPos.Should().BePositive();
        aPos.Should().BeLessThan(mPos);
        mPos.Should().BeLessThan(zPos);
    }

    // ----------------------------------------------------------------------
    // Diagnostics - invalid keys, collisions, malformed JSON
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_InvalidKey_EmitsD2I18N001AndSkipsKey()
    {
        const string mixed = @"{
            ""common_errors_NOT_FOUND"": ""Not found"",
            ""bad_key"": ""too few segments""
        }";

        var result = TKEmitter.Emit(mixed, sr_noOtherLocales);

        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].DescriptorId.Should().Be("D2I18N001");
        result.Diagnostics[0].Args[0].Should().Be("bad_key");

        // The emitter still emits the valid key.
        result.GeneratedSource.Should().Contain("NOT_FOUND");

        // The emitter does not emit the invalid key.
        result.GeneratedSource.Should().NotContain("bad_key");
    }

    [Fact]
    public void Emit_MultipleInvalidKeys_EmitsOneDiagnosticPerKey()
    {
        const string many_bad = @"{
            ""bad1"": ""x"",
            ""bad2"": ""x"",
            ""bad3_xx"": ""x""
        }";

        var result = TKEmitter.Emit(many_bad, sr_noOtherLocales);

        result.Diagnostics.Should().HaveCount(3);
        result.Diagnostics.Should().AllSatisfy(d => d.DescriptorId.Should().Be("D2I18N001"));
    }

    [Fact]
    public void Emit_TwoKeysCollideOnTKPath_EmitsD2I18N003AndKeepsFirst()
    {
        // common_errors_unknown and common_errors_UNKNOWN both decompose to
        // TK.Common.Errors.UNKNOWN. The emitter sorts the keys with
        // StringComparer.Ordinal and keeps the first one, so one key is kept
        // and the other is reported.
        const string colliding = @"{
            ""common_errors_unknown"": ""x"",
            ""common_errors_UNKNOWN"": ""y""
        }";

        var result = TKEmitter.Emit(colliding, sr_noOtherLocales);

        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].DescriptorId.Should().Be("D2I18N003");
        var args = result.Diagnostics[0].Args;
        args[2].Should().Be("TK.Common.Errors.UNKNOWN");

        // The first key in ordinal sort order is kept. The diagnostic gives
        // the kept key and the dropped key.
        args[0].Should().BeOneOf("common_errors_unknown", "common_errors_UNKNOWN");
        args[1].Should().BeOneOf("common_errors_unknown", "common_errors_UNKNOWN");
        args[0].Should().NotBe(args[1]);

        // The emitter writes only one constant. A substring count is enough
        // to check this and needs no regex.
        var occurrences = CountSubstring(
            result.GeneratedSource,
            "static readonly global::DcsvIo.D2.I18n.TKMessage UNKNOWN");
        occurrences.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Per-locale coverage - D2I18N002 (missing) and D2I18N004 (orphan)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_KeyMissingInOtherLocale_EmitsD2I18N002()
    {
        const string en_us = @"{""common_errors_NOT_FOUND"": ""Not found""}";
        const string fr_fr = @"{}";

        var result = TKEmitter.Emit(
            en_us,
            new Dictionary<string, string> { ["fr-FR"] = fr_fr });

        result.Diagnostics.Should().ContainSingle(d => d.DescriptorId == "D2I18N002");
        var diag = result.Diagnostics.Single(d => d.DescriptorId == "D2I18N002");
        diag.Args[0].Should().Be("common_errors_NOT_FOUND");
        diag.Args[1].Should().Be("fr-FR");
    }

    [Fact]
    public void Emit_OrphanKeyInOtherLocale_EmitsD2I18N004AndOmitsFromTk()
    {
        const string en_us = @"{""common_errors_NOT_FOUND"": ""Not found""}";
        const string fr_fr = @"{
            ""common_errors_NOT_FOUND"": ""Pas trouvé"",
            ""common_errors_ORPHAN"": ""Orphelin""
        }";

        var result = TKEmitter.Emit(
            en_us,
            new Dictionary<string, string> { ["fr-FR"] = fr_fr });

        var orphan = result.Diagnostics.Single(d => d.DescriptorId == "D2I18N004");
        orphan.Args[0].Should().Be("common_errors_ORPHAN");
        orphan.Args[1].Should().Be("fr-FR");

        // The orphan key does not appear in the generated TK.
        result.GeneratedSource.Should().NotContain("ORPHAN");
    }

    [Fact]
    public void Emit_OtherLocaleSchemaKey_IsNotTreatedAsOrphan()
    {
        const string en_us = @"{""common_errors_NOT_FOUND"": ""Not found""}";
        const string fr_fr = @"{
            ""$schema"": ""./schema.json"",
            ""common_errors_NOT_FOUND"": ""Pas trouvé""
        }";

        var result = TKEmitter.Emit(
            en_us,
            new Dictionary<string, string> { ["fr-FR"] = fr_fr });

        result.Diagnostics.Should().BeEmpty();
    }

    // ----------------------------------------------------------------------
    // Malformed JSON - D2I18N006
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_MalformedEnUsJson_EmitsD2I18N006AndEmptyTk()
    {
        var result = TKEmitter.Emit("{not valid json", sr_noOtherLocales);

        result.Diagnostics.Should().ContainSingle();
        result.Diagnostics[0].DescriptorId.Should().Be("D2I18N006");
        result.Diagnostics[0].Args[0].Should().Be("en-US.json");

        // The emitter still writes the TK class, with no constants.
        result.GeneratedSource.Should().Contain("public static partial class TK");
    }

    [Fact]
    public void Emit_EnUsRootIsArray_EmitsD2I18N006()
    {
        var result = TKEmitter.Emit("[]", sr_noOtherLocales);

        result.Diagnostics.Should().ContainSingle(d => d.DescriptorId == "D2I18N006");
    }

    [Fact]
    public void Emit_EnUsValueIsNotString_EmitsD2I18N006()
    {
        // The catalog must be a flat string-to-string map. The emitter rejects
        // numeric, object and array values as malformed.
        const string non_string_value = @"{""common_errors_NOT_FOUND"": 42}";

        var result = TKEmitter.Emit(non_string_value, sr_noOtherLocales);

        result.Diagnostics.Should().ContainSingle(d => d.DescriptorId == "D2I18N006");
    }

    [Fact]
    public void Emit_MalformedOtherLocale_EmitsD2I18N006WithLocaleFilenameAndSkipsThatLocaleOnly()
    {
        const string en_us = @"{""common_errors_NOT_FOUND"": ""Not found""}";

        var result = TKEmitter.Emit(
            en_us,
            new Dictionary<string, string>
            {
                ["fr-FR"] = "{not valid",
                ["de-DE"] = @"{""common_errors_NOT_FOUND"": ""Nicht gefunden""}",
            });

        var malformed = result.Diagnostics.Single(d => d.DescriptorId == "D2I18N006");
        malformed.Args[0].Should().Be("fr-FR.json");

        // The de-DE catalog is well-formed, so the emitter reports no orphan
        // or missing-key diagnostic for it.
        result.Diagnostics.Should().NotContain(d =>
            d.DescriptorId == "D2I18N002" || d.DescriptorId == "D2I18N004");

        // The emitter still emits the en-US keys.
        result.GeneratedSource.Should().Contain("NOT_FOUND");
    }

    // ----------------------------------------------------------------------
    // Volume / smoke
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_ManyKeys_DoesNotDegrade()
    {
        // Smoke test: 200 synthetic valid keys across several domains and
        // categories. The test makes no performance assertion. It checks that
        // the output is well-formed and that a large valid input raises no
        // diagnostic.
        var sb = new System.Text.StringBuilder("{");
        for (var d = 0; d < 4; d++)
        {
            for (var c = 0; c < 5; c++)
            {
                for (var k = 0; k < 10; k++)
                {
                    if (d + c + k > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('"').Append('d').Append(d).Append("_c").Append(c)
                      .Append("_KEY").Append(k).Append('"').Append(":\"v\"");
                }
            }
        }

        sb.Append('}');

        var result = TKEmitter.Emit(sb.ToString(), sr_noOtherLocales);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("KEY0").And.Contain("KEY9");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static int CountSubstring(string source, string substring)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(substring, idx, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += substring.Length;
        }

        return count;
    }
}
