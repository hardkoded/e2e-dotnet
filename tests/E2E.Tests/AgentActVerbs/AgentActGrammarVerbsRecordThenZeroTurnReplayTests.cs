// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using static E2E.Tests.AgentActVerbs.GesturesSession;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E.Tests.AgentActVerbs;

/// <summary>
/// <c>agent.act</c> grammar verbs recorded on a first run, then replayed with no model call.
/// Upstream's replay pass also runs hover, drag, and two repeated-press flows. This
/// port has no hover or drag tool, and its press tool takes no repeat count.
/// The engine-name lines are not ported: a replay records no engine events. The upload policy line is.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActGrammarVerbsRecordThenZeroTurnReplayTests(AgentActGrammarVerbsRecordThenZeroTurnReplayTests.Runs runs)
    : IClassFixture<AgentActGrammarVerbsRecordThenZeroTurnReplayTests.Runs>
{
    private const string RoundTrip = "opens a page and comes back";

    [Fact]
    public void Records_the_check_and_scroll_verbs_on_the_first_run()
    {
        foreach (var flow in ReplayFlows)
        {
            var cache = runs.First[flow.Title].Cache;
            Assert.NotNull(cache);
            Assert.Equal(("missed", "no-entry"), (cache.Mode, cache.Reason));
        }
    }

    [Fact]
    public void Replays_check_and_scroll_without_a_model_call_and_runs_a_round_trip_that_changed_nothing_live()
    {
        // Every second-run flow passed its check: RunFlowAsync fails the fixture otherwise.
        // A round trip leaves the screen and the route as it found them: nothing a
        // replay could check, so it is never recorded and the model runs it again.
        var roundTrip = runs.Second[RoundTrip];
        Assert.Equal(("missed", "no-entry"), (roundTrip.Cache?.Mode, roundTrip.Cache?.Reason));
        Assert.Equal(runs.Second.Values.Sum(result => result.ModelCalls), roundTrip.ModelCalls);
        foreach (var flow in ReplayFlows.Where(entry => entry.Title != RoundTrip))
        {
            var step = runs.Second[flow.Title];
            Assert.Equal("self-finalized", step.Cache?.Mode);
            Assert.Equal(0, step.ModelCalls);
        }

        // The replay authorizes the recorded paths against the project root again.
        var policy = Assert.Single(runs.SecondSteps[UploadFlow.Title].Single(step => step.Api == "agent.act").Events, item => item.Kind == StepEventKind.Policy);
        Assert.Equal(("upload.path", PolicyDecision.Allowed), (policy.Name, policy.Decision));
    }

    /// <summary>Both passes over the replay flows, sharing one cache: the first records, the second replays.</summary>
    public sealed class Runs : IAsyncLifetime
    {
        public string CacheDirectory { get; } = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));

        public Dictionary<string, ActResult> First { get; } = [];

        public Dictionary<string, ActResult> Second { get; } = [];

        internal Dictionary<string, IReadOnlyList<StepRecord>> SecondSteps { get; } = [];

        public async Task InitializeAsync()
        {
            using var site = await StartSiteAsync();
            foreach (var flow in ReplayFlows)
            {
                First[flow.Title] = await RunFlowAsync(site, flow, ModelFor(flow), CacheDirectory);
            }

            foreach (var flow in ReplayFlows)
            {
                (Second[flow.Title], SecondSteps[flow.Title]) = await RunFlowWithStepsAsync(site, flow, ModelFor(flow), CacheDirectory);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
