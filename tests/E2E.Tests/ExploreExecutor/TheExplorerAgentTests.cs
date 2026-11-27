// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using State = E2E.ExploreState;

namespace E2E.Tests.ExploreExecutor;

/// <summary>Upstream's "the explorer agent" describe (explore-executor.test.ts, tester-army/e2e#1993).</summary>
public sealed class TheExplorerAgentTests
{
    [Fact]
    public void Puts_its_own_charter_over_the_built_in_commit_rule_submitting_twice_and_revisiting_after_a_save_are_deliberate()
    {
        var explorer = global::E2E.ExploreExecutor.ExplorerAgent(
            new ExplorerOptions(null!, "default", new AgentOptions { System = "Be careful." }, null!, _ => { })
        );

        // The built-in rules come first in the system prompt; the explorer's guidance follows them and says it wins.
        Assert.Contains("Never repeat a commit", DefaultAgent.BaseRules, StringComparison.Ordinal);
        Assert.Contains("Be careful.", explorer.System, StringComparison.Ordinal);
        Assert.Contains(
            "These rules take precedence over the built-in rules above where they differ",
            explorer.System,
            StringComparison.Ordinal
        );
        Assert.Contains("submitting twice quickly", explorer.System, StringComparison.Ordinal);
        Assert.Contains(
            "After a save, reload or revisit and check what was kept.",
            explorer.System,
            StringComparison.Ordinal
        );
    }
}

/// <summary>Upstream's "the finding tool" describe (explore-executor.test.ts, tester-army/e2e#2270).</summary>
public sealed class TheFindingToolTests
{
    private static readonly JsonObject Report = new()
    {
        ["title"] = "Total shows $0.00",
        ["kind"] = "issue",
        ["severity"] = 4,
        ["expected"] = "a total",
        ["actual"] = "$0.00",
        ["reproduction"] = new JsonArray("open the cart"),
    };

    private static State NewState() => new("goal", new ExploreBudgets(4, TimeSpan.FromMilliseconds(300_000)));

    /// <summary>Starts a report that waits in <paramref name="pending"/>, its observation or its evidence screenshot's save, until the release.</summary>
    private static (Task<string> Done, TaskCompletionSource Release, List<string> Attached) Reporting(
        State state,
        string pending
    )
    {
        var tool = global::E2E.ExploreExecutor.WithFindingTool(new AgentOptions(), state).Tools![
            global::E2E.ExploreExecutor.FindingToolName
        ];
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attached = new List<string>();
        var context = new AgentToolContext(new ReportingContext(pending, release.Task, attached), mutates: false);
        return (tool.RunAsync((JsonObject)Report.DeepClone(), context, CancellationToken.None), release, attached);
    }

    [Theory]
    [InlineData("observe")]
    [InlineData("attachScreenshot")]
    public async Task Records_a_finding_against_the_step_it_was_reported_in_when_that_step_ended_at_its_deadline_during(
        string pending
    )
    {
        var state = NewState();
        state.BeginStep("Cart", "open the cart");
        var (done, release, _) = Reporting(state, pending);
        await Task.Delay(50);
        state.EndStep(ReportExploreStepStatus.Exhausted, "step timed out", "STEP_TIMEOUT");
        state.BeginStep("Checkout", "pay");
        release.SetResult();
        await done;
        Assert.Equal<int?>([1], state.Findings.Select(finding => finding.Step));
    }

    [Fact]
    public async Task Records_nothing_once_the_exploration_is_over()
    {
        var state = NewState();
        state.BeginStep("Cart", "open the cart");
        var (done, release, _) = Reporting(state, "attachScreenshot");
        await Task.Delay(50);
        state.EndStep(ReportExploreStepStatus.Exhausted, "step timed out", "STEP_TIMEOUT");
        state.End(ReportExploreEnded.Time);
        state.Close();
        release.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => done);
        Assert.Matches("exploration is over", error.Message);
        Assert.Empty(state.Findings);
    }

    [Fact]
    public async Task Keeps_no_screenshot_for_a_finding_the_closed_record_refuses()
    {
        var state = NewState();
        state.BeginStep("Cart", "open the cart");
        var (done, release, attached) = Reporting(state, "observe");
        await Task.Delay(50);
        state.EndStep(ReportExploreStepStatus.Exhausted, "step timed out", "STEP_TIMEOUT");
        state.End(ReportExploreEnded.Time);
        state.Close();
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => done);
        Assert.Empty(attached);
    }

    private sealed class ReportingContext : StepExecutorContext
    {
        private readonly string _pending;
        private readonly Task _released;
        private readonly List<string> _attached;

        [SetsRequiredMembers]
        public ReportingContext(string pending, Task released, List<string> attached)
        {
            _pending = pending;
            _released = released;
            _attached = attached;
            Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = "explore" };
            Attempt = new ExecutorAttempt { TestId = "test", AttemptId = "attempt" };
            Target = new ExecutorTargetInfo
            {
                Name = "web",
                Platform = "web",
                Verbs = new HashSet<ExecutorVerb>(),
            };
            Budgets = null!;
            Actions = null!;
        }

        public override async Task<ExecutorObservation> ObserveAsync(
            ExecutorObserveOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            if (_pending == "observe")
            {
                await _released;
            }

            return new ExecutorObservation("")
            {
                Revision = "3",
                Pixels = new ExecutorPixels([], "image/png", 1, 1, 1, 0),
            };
        }

        public override async Task<string> AttachScreenshotAsync(
            ExecutorPixels pixels,
            string label,
            CancellationToken cancellationToken = default
        )
        {
            _attached.Add(label);
            if (_pending == "attachScreenshot")
            {
                await _released;
            }

            return "attempt-1:artifact:0";
        }
    }
}
