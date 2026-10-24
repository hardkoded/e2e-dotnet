// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.Explore.ExploreHarness;
using Explorer = E2E.Explore;

namespace E2E.Tests.Explore;

/// <summary>
/// .NET-only: what <c>e2e explore</c> reports when the engine's <c>finish</c> hook fails. Upstream runs the exploration as a run, which
/// records a cleanup failure as a run error and folds it into the exit code (<c>run/runner.ts</c>, <c>currentExitCode</c>); the port's
/// exploration has no runner, so these read the outcome <see cref="Explorer.ExploreAsync"/> returns. No upstream <c>it</c> covers it.
/// </summary>
public sealed class ExploreCleanupTests
{
    private sealed class FinishFailing(Exception failure) : ScreenshotEngine(CounterPage.World(), _ => ScreenshotEngine.Png()), IPreparableEngine
    {
        public Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken) => Task.FromResult(new EnginePrepareResult());

        public Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken) => Task.FromException(failure);
    }

    private static ExploreModel Finishing() => new(_ => ExploreModel.Finish("Looked."), _ => ExploreModel.Done("passed", "unused"));

    [Fact]
    public async Task Returns_the_outcome_with_a_failed_finish_as_an_infrastructure_error()
    {
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), Finishing().Model, [], engine: new FinishFailing(new InvalidOperationException("lease release refused"))));

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(RunStatus.Failed, outcome.Status);
        var error = Assert.IsType<EngineException>(outcome.Error);
        Assert.Equal(EngineErrorCodes.EngineFailure, error.Code);
        Assert.Contains("lease release refused", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_findings_verdict_in_the_outcome_when_finish_fails_too()
    {
        var plans = 0;
        var model = new ExploreModel(
            _ => ++plans == 1 ? ExploreModel.Step("Counter", "Look at the counter") : ExploreModel.Finish("One defect."),
            call => call.Turn == 1 ? ExploreModel.ReportFinding(ExploreModel.CounterFinding()) : ExploreModel.Done("passed", "Looked"));
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), model.Model, [], engine: new FinishFailing(new InvalidOperationException("lease release refused"))));

        Assert.Single(outcome.Explore.Findings);
        Assert.NotNull(outcome.Error);
        Assert.Equal(3, outcome.ExitCode);
    }

    [Fact]
    public async Task Redacts_the_secrets_in_an_e2e_error_a_failed_finish_throws()
    {
        var credentials = new Dictionary<string, UserCredential> { ["ada"] = new("ada@example.test", Secret.Create("password", "bookworm")) };
        var failure = new EngineException("CLEANUP_TIMEOUT", "release refused for bookworm");
        var outcome = await Explorer.ExploreAsync(Options(TempRoot(), Finishing().Model, [], engine: new FinishFailing(failure), credentials: credentials));

        var error = Assert.IsType<EngineException>(outcome.Error);
        Assert.Equal("CLEANUP_TIMEOUT", error.Code);
        Assert.DoesNotContain("bookworm", error.Message, StringComparison.Ordinal);
    }
}
