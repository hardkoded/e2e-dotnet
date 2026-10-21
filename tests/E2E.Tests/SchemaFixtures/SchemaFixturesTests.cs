// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SchemaFixtures;

/// <summary>
/// The two top-level <c>it</c> of <c>tests/unit/schema-fixtures.test.ts</c>. Upstream ships four schemas (<c>agent-judgment-v1</c>,
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

    // Partial: upstream reads package.json's files and expands the glob. The port reads the project file's packed items and lists the same glob;
    // that dotnet pack puts exactly those files in the package was checked by hand.
    [Fact]
    public void Publishes_every_schema_and_no_fixture_the_project_file_names_the_glob_and_the_glob_matches_the_schema_set()
    {
        const string shipped = @"..\..\schema\*.schema.json";
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(ReportSchema.Root, "..", "src", "E2E", "E2E.csproj"));
        var packed = project.Descendants("None")
            .Where(item => (string?)item.Attribute("Pack") == "true" && (string?)item.Attribute("PackagePath") == "schema")
            .Select(item => (string?)item.Attribute("Include"))
            .ToList();

        Assert.Contains(shipped, packed);
        var matched = Directory.GetFiles(ReportSchema.Root, shipped[(shipped.LastIndexOf('\\') + 1)..]).Select(file => Path.GetFileName(file)[..^".schema.json".Length]);
        Assert.Equal(Schemas.Order(), matched.Order());
        Assert.DoesNotContain(matched, name => name.Contains("fixture", StringComparison.Ordinal));
    }
}
