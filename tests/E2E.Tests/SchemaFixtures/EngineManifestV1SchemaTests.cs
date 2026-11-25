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
        var invalid = ReportSchema.ReadJson("fixtures", "engine-manifest-v1.invalid.json");

        Assert.False(Validate.IsValid(invalid));
        Assert.Contains(("/actions", "uniqueItems"), Validate.Errors(invalid));
        // Upstream's Ajv lists that error alone; with the repeat removed the same manifest is valid, so the repeat is the only fault.
        invalid["actions"]!.AsArray().RemoveAt(2);
        Assert.True(Validate.IsValid(invalid));
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
