// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('explore runs'). The port's reporter is the view of
/// <c>e2e explore</c>, which prints as the list reporter does without a live window, so the tests drive the command with a
/// scripted exploration. Not ported: "badges the record with the explored target after its test has finished" (the port has no
/// test result, so the one badge is printed at the start), and "with a live window, shows the exploration in the window and
/// prints the record with its steps once the run is over" (the port has no live window).
/// </summary>
[Collection(ExploreEnvironment.Name)]
public sealed partial class ExploreRunsTests
{
    private const string Goal = "Explore checkout like a first-time buyer";

    private static readonly DateTimeOffset Start = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    private static readonly ExploreBudgets Budgets = new(4, TimeSpan.FromMilliseconds(300_000));

    private static readonly ReportExploreFinding Finding = new(
        "f-1",
        0,
        1,
        ReportExploreFindingKind.Issue,
        4,
        "Cart total shows $0.00 with two items",
        "The total equals the sum of the two line items",
        "The cart shows \"Total: $0.00\" under two items priced $12.00 and $8.00 each",
        ["Open the catalog", "Add two books to the cart", "Open the cart"],
        Start.AddSeconds(5),
        "/cart",
        ".e2e/results/explore-checkout/attempt-1/screenshots/1-finding-1.png");

    private static readonly ReportExploreFinding Warning = new(
        "f-2",
        1,
        2,
        ReportExploreFindingKind.Warning,
        1,
        "Footer misspells Receive",
        "Receive",
        "Recieve",
        ["Scroll to the footer"],
        Start.AddSeconds(20),
        "/");

    private static readonly OpenedStep Step1 = new(1, "Cart", "Add two books and open the cart");

    private static readonly OpenedStep Step2 = new(2, "Checkout", "Pay for the cart");

    private static readonly ReportExploreStep Step1Done = new(1, "Cart", Step1.Instruction, ReportExploreStepStatus.Passed, Start.AddSeconds(1), TimeSpan.FromMilliseconds(32_000), "Cart holds two items");

    private static readonly ReportExploreStep Step2Done = new(
        2,
        "Checkout",
        Step2.Instruction,
        ReportExploreStepStatus.Failed,
        Start.AddSeconds(40),
        TimeSpan.FromMilliseconds(91_000),
        "The Pay button stayed disabled with every field filled",
        "STEP_FAILED");

    private const string Assessment = "Checkout cannot be completed. Script the cart total.";

    /// <summary>The whole exploration from its start to the closing assessment, as the run's stream carries it.</summary>
    private static List<ExploreProgress> Play() =>
    [
        new ExploreProgress.Started(Goal, Budgets),
        new ExploreProgress.Planning(false),
        new ExploreProgress.StepStarted(Step1),
        new ExploreProgress.FindingReported(Finding),
        new ExploreProgress.StepFinished(Step1Done),
        new ExploreProgress.Planning(false),
        new ExploreProgress.StepStarted(Step2),
        new ExploreProgress.FindingReported(Warning),
        new ExploreProgress.StepFinished(Step2Done),
        new ExploreProgress.Finished(ReportExploreEnded.Finished, Assessment),
    ];

    private static ExploreOutcome Failed() => new(
        RunStatus.Failed,
        1,
        new TestException("ASSERTION_FAILED", "exploration found 1 issue(s): [severity 4] Cart total shows $0.00 with two items"),
        new ReportExplore(Goal, Budgets, ReportExploreEnded.Finished, [Step1Done, Step2Done], [Finding, Warning], Assessment));

    /// <summary>Partial: the port prints no run header, counts no actions in a step (it has no engine events), and names the screenshot by its path under the project root.</summary>
    [Fact]
    public async Task Without_a_live_window_streams_the_header_each_finding_and_each_step_as_they_happen_then_the_findings_with_their_evidence()
    {
        var (code, stdout, stderr) = await new FakeExplore(Play(), Failed()).InvokeAsync("--target", "web", Goal);

        Assert.Equal(1, code);
        Assert.Equal("", stderr);
        Assert.Equal(
            string.Join(
                '\n',
                "",
                " ❯ |web| Exploring  Explore checkout like a first-time buyer",
                "   ⚑ high issue  Cart total shows $0.00 with two items (/cart)",
                "   ✓ 1  Cart 32.00s · 1 finding",
                "   ⚑ trivial warning  Footer misspells Receive (/)",
                "   × 2  Checkout 91.00s · 1 finding failed",
                "        The Pay button stayed disabled with every field filled",
                "   ended: the agent covered the goal",
                "",
                new string('⎯', 34) + " Findings 2 " + new string('⎯', 34),
                "",
                " 1. high issue       Cart total shows $0.00 with two items",
                "    /cart · step 1",
                "    expected  The total equals the sum of the two line items",
                "    actual    The cart shows \"Total: $0.00\" under two items priced $12.00 and",
                "              $8.00 each",
                "    steps     1. Open the catalog",
                "              2. Add two books to the cart",
                "              3. Open the cart",
                "    evidence  .e2e/results/explore-checkout/attempt-1/screenshots/1-finding-1.png",
                "",
                " 2. trivial warning  Footer misspells Receive",
                "    / · step 2",
                "    expected  Receive",
                "    actual    Recieve",
                "    steps     1. Scroll to the footer",
                "",
                " Assessment",
                "   Checkout cannot be completed. Script the cart total.",
                "",
                "   Findings  1 issue | 1 warning",
                "      Steps  2 of 4 · 1 passed · 1 failed · the agent covered the goal",
                "   Start at  10:00:00",
                "   Duration  0ms",
                "",
                ""),
            Stable(stdout));
    }

    /// <summary>Partial: the port's config declares one target, so the badge is the one named with --target and cannot differ from a first configured target.</summary>
    [Fact]
    public async Task Badges_the_exploration_with_the_target_its_test_started_on_not_the_first_configured_one()
    {
        var (_, stdout, _) = await new FakeExplore([new ExploreProgress.Started(Goal, Budgets)], Failed()).InvokeAsync("--target", "mixed");

        var header = stdout.Split('\n').First(line => line.Contains("Exploring", StringComparison.Ordinal));
        Assert.Contains("|mixed|", header, StringComparison.Ordinal);
        Assert.DoesNotContain("|capacity|", header, StringComparison.Ordinal);
    }

    /// <summary>Partial: the run is the command's, with a scripted exploration, and its failure is the outcome's error.</summary>
    [Fact]
    public async Task Keeps_a_failure_that_is_not_the_findings_verdict_such_as_a_run_that_explored_nothing()
    {
        var outcome = new ExploreOutcome(
            RunStatus.Blocked,
            1,
            new AgentException("AUTOMATION_UNSUPPORTED", "exploration concluded nothing: no step ran and no finding was recorded"),
            new ReportExplore(Goal, Budgets, ReportExploreEnded.Aborted, [], []));
        var moments = new ExploreProgress[] { new ExploreProgress.Started(Goal, Budgets), new ExploreProgress.Finished(ReportExploreEnded.Aborted) };

        var (code, stdout, _) = await new FakeExplore(moments, outcome).InvokeAsync("--target", "web", Goal);

        var lines = stdout.Split('\n');
        Assert.Equal(1, code);
        Assert.Contains("   no step ran", lines);
        Assert.Contains("   ended: the run was cut short", lines);
        Assert.Contains("AUTOMATION_UNSUPPORTED: exploration concluded nothing", stdout, StringComparison.Ordinal);
        Assert.Contains("   Findings  none", lines);
        Assert.Contains("      Steps  0 of 4 · the run was cut short", lines);
    }

    /// <summary>The clock rows are the only ones that differ between runs.</summary>
    private static string Stable(string text) => Duration().Replace(Clock().Replace(text, "Start at  10:00:00"), "Duration  0ms");

    [GeneratedRegex(@"Start at  \d\d:\d\d:\d\d")]
    private static partial Regex Clock();

    [GeneratedRegex(@"Duration  [\d.]+m?s")]
    private static partial Regex Duration();
}
