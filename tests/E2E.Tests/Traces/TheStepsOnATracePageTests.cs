// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.Traces.ReportBuilders;

namespace E2E.Tests.Traces;

/// <summary>Upstream's <c>describe('the steps on a trace page')</c> (<c>tests/unit/traces.test.ts</c>).</summary>
public sealed class TheStepsOnATracePageTests : TracePageKit
{
    [Fact]
    public async Task Tell_the_caches_decision_first_then_the_actions_polls_and_app_log_in_the_order_they_happened()
    {
        var page = Assert.Single(await PagesOfAsync(Failing));

        Assert.Equal(
            [
                $"cache: replayed 1 of 3 recorded actions, then the agent took over: a recorded target is not on the screen; at action 2 of 3, tap button \"Apply\"; recording saved (`.e2e/cache/{Entry}.json`)",
                "tap button \"Coupon\" (12ms)",
                "✗ network error: `POST /api/coupon 500 Internal Server Error`",
            ],
            StepLines(page.Text, 1));
        Assert.Equal(["expect gave up after 50 reads in 5.0s: text \"$10\" (1 match) x50"], StepLines(page.Text, 2));
    }

    /// <summary>.NET-only: upstream's fixture carries no <c>recordedBy</c>, so no <c>it</c> of its page tests names the model line.</summary>
    [Fact]
    public async Task Name_the_model_that_recorded_a_replayed_step_and_what_became_of_its_recording()
    {
        var step = Step(
            0,
            StepKind.Agent,
            "agent.act",
            "add the coupon",
            cache: new CacheInfo { Mode = "self-finalized", ReplayedActions = 2, TotalActions = 2, Entry = Entry, RecordedBy = "fake-loop/model-A" },
            cacheWrite: CacheWrite.Kept);

        var page = Assert.Single(await PagesOfAsync(Result(id: "cccccccc33", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [step])])));

        Assert.Equal(
            [$"cache: replayed all 2 recorded actions, no model call; recorded with `fake-loop/model-A`; recording kept (`.e2e/cache/{Entry}.json`)"],
            StepLines(page.Text, 1));
    }

    [Fact]
    public async Task Leave_out_a_poll_that_passed_on_its_first_read_and_name_a_failed_action_with_its_code()
    {
        var step = Step(
            0,
            status: StepStatus.Failed,
            events:
            [
                Event(StepEventKind.Poll, At(1), 3, StepEventStatus.Passed, "expect", 1),
                Event(StepEventKind.Engine, At(2), 30, StepEventStatus.Failed, "tap", code: "NOT_ACTIONABLE", detail: "tap button \"Save\""),
            ]);

        var page = Assert.Single(await PagesOfAsync(Result(id: "aaaaaaaa11", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [step])])));

        Assert.Equal(["✗ tap button \"Save\": **NOT_ACTIONABLE**"], StepLines(page.Text, 1));
    }

    [Fact]
    public async Task Keep_every_failure_and_the_latest_events_of_a_step_with_more_than_the_page_has_room_for()
    {
        var events = Enumerable.Range(0, 30).Select(index =>
            Event(StepEventKind.Engine, At(index), 1, index == 2 ? StepEventStatus.Failed : StepEventStatus.Passed, "tap", detail: $"tap button \"{index}\""));
        var step = Step(0, StepKind.Agent, "agent.act", "tap them all", StepStatus.Failed, events);

        var page = Assert.Single(await PagesOfAsync(Result(id: "bbbbbbbb22", status: ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, steps: [step])])));

        var lines = StepLines(page.Text, 1);
        Assert.Equal("10 events left out", lines[0]);
        Assert.Equal("✗ tap button \"2\"", lines[1]);
        Assert.Equal("tap button \"29\" (1ms)", lines[^1]);
        Assert.Equal(21, lines.Count);
    }

    [Fact]
    public async Task Tell_how_the_screen_changed_by_the_step_where_the_page_went_and_what_the_app_said_in_passing()
    {
        var open = Step(0, api: "app.open", label: "/", screen: new StepScreen { Location = "http://127.0.0.1:4100/todos", Nodes = 33, Changes = [] });
        var tap = Step(
            1,
            status: StepStatus.Failed,
            events: [Event(StepEventKind.Navigation, At(2), 0, detail: "navigated to http://127.0.0.1:4100/login")],
            screen: new StepScreen { Location = "http://127.0.0.1:4100/login", Nodes = 5, Since = 0, Changes = ["added heading \"Sign in\"", "removed heading \"Todos\""], More = 3 });
        var still = Step(2, api: "locator.tap", screen: new StepScreen { Nodes = 5, Since = 1, Changes = [] });

        var page = Assert.Single(await PagesOfAsync(Result(
            id: "cccccccc33",
            status: ResultStatus.Failed,
            attempts:
            [
                Attempt(
                    AttemptStatus.Failed,
                    environment: new Dictionary<string, string> { ["browser"] = "chromium 141.0", ["user agent"] = "Mozilla/5.0" },
                    steps: [open, tap, still],
                    appLog: [new AppLogRecord(AppLogSource.Console, AppLogLevel.Info, "loaded 3 todos", At(1), 1)]),
            ])));

        Assert.Contains("Ran on: browser `chromium 141.0` · user agent `Mozilla/5.0`", page.Text, StringComparison.Ordinal);
        Assert.Equal(["screen: 33 nodes at `/todos`"], StepLines(page.Text, 1));
        Assert.Equal(["ℹ console: `loaded 3 todos`", "↪ navigated to /login", "screen: 5 changes since step 1 at `/login`, 5 nodes"], StepLines(page.Text, 2));
        Assert.Contains("     - `added heading \"Sign in\"`\n     - `removed heading \"Todos\"`\n     - 3 more\n", page.Text, StringComparison.Ordinal);
        Assert.Equal(["screen: unchanged since step 2"], StepLines(page.Text, 3));
    }
}
