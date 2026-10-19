// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Traces;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('detailLines')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class DetailLinesTests
{
    [Fact]
    public void Lays_an_assertions_expected_and_observed_one_per_line_with_the_match_count_and_says_what_a_locator_asked_for_and_how_long_it_waited()
    {
        Assert.Equal(
            ["Expected: text \"2 remaining\"", "Observed: text \"1 remaining\" (1 match)"],
            FailureText.DetailLines(ReportBuilders.Error(details: new() { Expected = "text \"2 remaining\"", Observed = "text \"1 remaining\"", Matches = 1 })));
        Assert.Equal(
            ["Asked for: button \"Add todo item\"", "Waited: 3.0s"],
            FailureText.DetailLines(ReportBuilders.Error(details: new() { Role = "button", Name = "Add todo item", WaitedMs = 3000 })));
        Assert.Equal(["Asked for: test id \"todo\""], FailureText.DetailLines(ReportBuilders.Error(details: new() { TestId = "todo" })));
        Assert.Equal(["Expected: two &lt;b&gt;labels&lt;/b&gt;"], FailureText.DetailLines(ReportBuilders.Error(details: new() { Expected = "two <b>labels</b>" })));
    }
}
