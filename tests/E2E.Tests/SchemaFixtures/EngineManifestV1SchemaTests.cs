// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SchemaFixtures;

/// <summary>Upstream's <c>describe.each(schemas)('%s schema')</c> for <c>engine-manifest-v1</c> (<c>tests/unit/schema-fixtures.test.ts</c>).</summary>
public sealed class EngineManifestV1SchemaTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("engine-manifest-v1.schema.json");

    [Fact]
    public void Accepts_the_valid_fixture() => Assert.True(Validate.IsValid(ReportSchema.ReadJson("fixtures", "engine-manifest-v1.valid.json")));

    [Fact]
    public void Rejects_the_invalid_fixture() => Assert.False(Validate.IsValid(ReportSchema.ReadJson("fixtures", "engine-manifest-v1.invalid.json")));

    [Fact]
    public void Rejects_a_method_listed_twice()
    {
        var manifest = ReportSchema.ReadJson("fixtures", "engine-manifest-v1.valid.json");
        manifest["methods"]!.AsArray().Add("lifecycle/doctor");

        Assert.False(Validate.IsValid(manifest));
    }
}
