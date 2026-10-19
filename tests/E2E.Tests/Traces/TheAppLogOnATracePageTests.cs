// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.Traces.ReportBuilders;

namespace E2E.Tests.Traces;

/// <summary>Upstream's <c>describe('the app log on a trace page')</c> (<c>tests/unit/traces.test.ts</c>).</summary>
public sealed class TheAppLogOnATracePageTests : TracePageKit
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AppLogRecord Logged(int ms, AppLogLevel level, string text, int? step, AppLogSource source = AppLogSource.Console) =>
        new(source, level, text, Epoch.AddMilliseconds(ms), step);

    [Fact]
    public async Task Lists_every_line_the_app_logged_in_one_section_oldest_first_with_its_step_on_the_page_and_one_before_the_first_step_as_such()
    {
        var first = Step(0, api: "app.open", events: [Event(StepEventKind.Navigation, Epoch.AddMilliseconds(10), 0, detail: "navigated to http://127.0.0.1:4100/")]);
        var second = Step(1, status: StepStatus.Failed);
        AppLogRecord[] appLog =
        [
            Logged(40, AppLogLevel.Error, "POST /api/todos 500", 1, AppLogSource.Network),
            Logged(20, AppLogLevel.Info, "booted", 0),
            Logged(30, AppLogLevel.Warning, "slow render", 1),
            Logged(5, AppLogLevel.Info, "early", null),
        ];

        var page = Assert.Single(await PagesOfAsync(Result(id: "dddddddd44", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [first, second], appLog: appLog)])));

        Assert.Contains(
            string.Join(
                '\n',
                "## App log",
                "",
                "What the app logged, oldest first, with the step it happened in.",
                "",
                "- before step 1 · ℹ console: `early`",
                "- step 1 · ℹ console: `booted`",
                "- step 2 · ⚠ console warning: `slow render`",
                "- step 2 · ✗ network error: `POST /api/todos 500`",
                ""),
            page.Text,
            StringComparison.Ordinal);
        Assert.Equal(["↪ navigated to /", "ℹ console: `booted`"], StepLines(page.Text, 1));
    }

    [Fact]
    public async Task Keeps_every_error_past_the_cap_then_the_latest_of_the_rest_and_says_how_many_it_left_out()
    {
        var chatter = Enumerable.Range(0, 60).Select(index => Logged(index, AppLogLevel.Info, $"tick {index}", 0));
        var step = Step(0, status: StepStatus.Failed);
        AppLogRecord[] appLog = [Logged(-1, AppLogLevel.Error, "first failure", 0), .. chatter];

        var page = Assert.Single(await PagesOfAsync(Result(id: "eeeeeeee55", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [step], appLog: appLog)])));

        var section = page.Text.Split("## App log")[1].Split("\n## ")[0];
        var items = section.Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)).ToList();
        Assert.Equal(51, items.Count);
        Assert.Equal("- 11 lines left out", items[0]);
        Assert.Equal("- step 1 · ✗ console error: `first failure`", items[1]);
        Assert.Equal("- step 1 · ℹ console: `tick 59`", items[^1]);
        Assert.DoesNotContain("`tick 10`", section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_the_section_out_when_the_app_logged_nothing()
    {
        var page = Assert.Single(await PagesOfAsync(Result(id: "ffffffff66", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [Step(0)])])));

        Assert.DoesNotContain("## App log", page.Text, StringComparison.Ordinal);
    }
}
