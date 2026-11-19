// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.HtmlReporter.Tests.Reporter;

/// <summary>.NET-only: the options of the config entry (upstream's <c>htmlReporter(options)</c> is called in code).</summary>
public sealed class ConfigureTests
{
    private static void Configure(string json) => new HtmlReporter().Configure(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Accepts_outputFile_and_media()
    {
        Configure("""{ "outputFile": "reports/index.html", "media": "embed" }""");
        Configure("""{ "media": "none" }""");
    }

    [Theory]
    [InlineData("""{ "theme": "dark" }""", "\"theme\" is not supported")]
    [InlineData("""{ "media": "zip" }""", "\"media\" must be")]
    [InlineData("""{ "media": 1 }""", "\"media\" must be a string")]
    [InlineData("""{ "outputFile": 1 }""", "\"outputFile\" must be a string")]
    [InlineData("""[]""", "options must be an object")]
    public void Rejects_what_it_does_not_know_naming_the_option_and_never_its_value(string json, string message) =>
        Assert.Contains(message, Assert.Throws<ArgumentException>(() => Configure(json)).Message);
}
