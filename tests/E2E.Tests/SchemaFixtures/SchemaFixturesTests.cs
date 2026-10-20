// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SchemaFixtures;

/// <summary>
/// The first top-level <c>it</c> of <c>tests/unit/schema-fixtures.test.ts</c>. The second (it publishes every schema) waits for the step that packs the schema. Upstream ships four schemas (<c>agent-judgment-v1</c>,
/// <c>agent-judgment-v2</c>, <c>report-v1</c>, <c>session-v1</c>); the port ships <c>report-v1</c> only.
/// </summary>
public sealed class SchemaFixturesTests
{
    private static string[] Schemas => [.. Directory.GetFiles(ReportSchema.Root, "*.schema.json").Select(file => Path.GetFileName(file)[..^".schema.json".Length])];

    [Fact]
    public void Ships_a_schema_for_every_fixture_pair()
    {
        var fixtures = Directory.GetFiles(ReportSchema.SchemaPath("fixtures"))
            .Select(file => System.Text.RegularExpressions.Regex.Replace(Path.GetFileName(file), @"\.(valid|invalid)\.json\z", ""));

        Assert.Equal(Schemas.Order(), fixtures.Distinct().Order());
    }
}
