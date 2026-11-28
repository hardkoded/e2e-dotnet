// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.SchemaFixtures;

/// <summary>Upstream's <c>describe.each(schemas)('%s schema')</c> for <c>engine-manifest-v1</c> (<c>tests/unit/schema-fixtures.test.ts</c>).</summary>
public sealed class EngineManifestV1SchemaTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("engine-manifest-v1.schema.json");

    [Fact]
    public void Accepts_the_valid_fixture() =>
        Assert.True(Validate.IsValid(ReportSchema.ReadJson("fixtures", "engine-manifest-v1.valid.json")));

    [Fact]
    public void Rejects_the_invalid_fixture() =>
        Assert.False(Validate.IsValid(ReportSchema.ReadJson("fixtures", "engine-manifest-v1.invalid.json")));

    [Fact]
    public void Accepts_a_web_manifest_declaring_payload_uploads() =>
        Assert.True(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "web", "version": "0.1.0", "spiVersion": 1, "platform": "web", "methods": ["engine/observe", "engine/perform"], "actions": ["setInputFiles", "setInputFilePayloads"] }"""
                )
            )
        );

    [Fact]
    public void Rejects_the_duplicate_payload_action_specifically()
    {
        var manifest = JsonNode.Parse(
            """{ "name": "web", "version": "0.1.0", "spiVersion": 1, "methods": ["engine/observe", "engine/perform"], "actions": ["setInputFiles", "setInputFilePayloads", "setInputFilePayloads"] }"""
        )!;

        Assert.False(Validate.IsValid(manifest));
        // Upstream's Ajv stops at its first error, which is this one; the port's validator lists every error, this one first.
        Assert.Equal(("/actions", "uniqueItems"), Validate.Errors(manifest)[0]);
    }

    [Fact]
    public void Rejects_the_invalid_fixture_for_viewportIntersection_without_engine_observe_a_non_boolean_fileApps_and_an_unknown_action_kind()
    {
        var invalid = ReportSchema.ReadJson("fixtures", "engine-manifest-v1.invalid.json");

        Assert.False(Validate.IsValid(invalid));
        var errors = Validate.Errors(invalid);
        Assert.Contains(("/methods", "contains"), errors);
        Assert.Contains(("/fileApps", "type"), errors);
        Assert.Contains(("/actions/1", "enum"), errors);
        // `scrollTo`, the newest kind, is in the enum; only the made-up kind beside it fails.
        Assert.DoesNotContain(errors, error => error.Item1 == "/actions/0");
    }

    [Fact]
    public void Rejects_a_method_listed_twice()
    {
        var manifest = ReportSchema.ReadJson("fixtures", "engine-manifest-v1.valid.json");
        manifest["methods"]!.AsArray().Add("lifecycle/doctor");

        Assert.False(Validate.IsValid(manifest));
    }

    [Fact]
    public void Rejects_selectsDevice_without_lifecycle_prepare()
    {
        var manifest = ReportSchema.ReadJson("fixtures", "engine-manifest-v1.valid.json");
        Assert.True(manifest["selectsDevice"]!.GetValue<bool>());
        var methods = manifest["methods"]!.AsArray();
        methods.Remove(methods.Single(method => method!.GetValue<string>() == "lifecycle/prepare"));

        Assert.False(Validate.IsValid(manifest));
    }
}
