// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.HtmlReporter.Tests.Reporter;

public sealed class EmbedPayloadTests
{
    [Fact]
    public void Keeps_a_value_that_closes_the_script_element_inside_the_JSON()
    {
        const string title = "</script><script>alert(1)</script><!--";
        var report = ReporterFixtures.ReadReport();
        report["run"]!["project"]!["id"] = title;
        var page = Payload.Embed(ReporterFixtures.Template, ReporterFixtures.PayloadOf(report));

        Assert.Single(Regex.Matches(page, "</script>"));
        Assert.Equal(title, (string?)ReporterFixtures.ParsePayload(page)["report"]!["run"]!["project"]!["id"]);
    }

    [Fact]
    public void Refuses_a_template_without_exactly_one_placeholder()
    {
        var payload = ReporterFixtures.PayloadOf(ReporterFixtures.ReadReport());

        Assert.Matches(
            "exactly once",
            Assert.Throws<InvalidOperationException>(() => Payload.Embed("<html></html>", payload)).Message
        );
        Assert.Matches(
            "exactly once",
            Assert
                .Throws<InvalidOperationException>(() =>
                    Payload.Embed(string.Concat(Payload.Placeholder, Payload.Placeholder), payload)
                )
                .Message
        );
    }
}
