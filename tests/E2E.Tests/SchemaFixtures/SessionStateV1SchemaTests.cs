// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SchemaFixtures;

/// <summary>Upstream's <c>describe.each(schemas)('%s schema')</c> for <c>session-state-v1</c> (<c>tests/unit/schema-fixtures.test.ts</c>).</summary>
public sealed class SessionStateV1SchemaTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("session-state-v1.schema.json");

    [Fact]
    public void Accepts_the_valid_fixture() => Assert.True(Validate.IsValid(ReportSchema.ReadJson("fixtures", "session-state-v1.valid.json")));

    [Fact]
    public void Rejects_the_invalid_fixture() => Assert.False(Validate.IsValid(ReportSchema.ReadJson("fixtures", "session-state-v1.invalid.json")));
}
