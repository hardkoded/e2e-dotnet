// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Reports;
using AttemptStatus = E2E.Internal.AttemptStatus;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// <c>e2e explore</c> reads the process environment for accounts, so the tests that set an account run apart from the tests that
/// explore.
/// </summary>
internal static class ExploreEnvironment
{
    public const string Name = "ExploreEnvironment";
}

/// <summary>
/// An <see cref="Explore.ExploreAsync"/> that plays a scripted exploration and returns its outcome. The command prints what the run's events
/// carry, so the fake writes them to the process's events as the exploration does: the run's start and plan, the test, the scripted moments as
/// <see cref="ExploreEvent"/>s, the test's end with the outcome's error, and the run's end. A script with no moments writes none.
/// </summary>
internal sealed class FakeExplore(IReadOnlyList<ExploreProgress> moments, ExploreOutcome outcome)
{
    private const string Id = "explore-test";

    /// <summary>The options the command passed.</summary>
    public ExploreOptions? Options { get; private set; }

    public Task<ExploreOutcome> RunAsync(ExploreOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        if (moments.Count == 0)
        {
            return Task.FromResult(outcome);
        }

        var goal = outcome.Explore.Goal;
        var target = options.Target ?? "web";
        RunEvents.Emit(new RunStartedEvent
        {
            RunId = "run-1",
            ProjectId = "project",
            ProjectRoot = "/project",
            ArtifactsRoot = "/project/.e2e/results",
            Targets = [target],
        });
        RunEvents.Emit(new PlanEvent(1, [(Explore.ExploreFile, target, 1)]));
        RunEvents.Emit(new TestStartedEvent(Id, "default", 0, goal, Explore.ExploreFile, null, target));
        foreach (var moment in moments)
        {
            RunEvents.Emit(new ExploreEvent(moment));
        }

        var failed = outcome.Status != RunStatus.Passed;
        var attempt = new ReportAttempt
        {
            Id = "attempt-1",
            StartedAt = DateTimeOffset.UnixEpoch,
            Status = failed ? AttemptStatus.Failed : AttemptStatus.Passed,
            Error = outcome.Error is { } error ? new ReportError(error.Code, error.Message) : null,
        };
        RunEvents.Emit(new TestFinishedEvent(new RunEventResult
        {
            Test = new TestIdentity([goal], Id, Explore.ExploreFile, 0),
            Target = (target, "web"),
            ResultId = Id,
            Status = failed ? Reports.ResultStatus.Failed : Reports.ResultStatus.Passed,
            Recorded = new ReportResult
            {
                Id = Id,
                File = Explore.ExploreFile,
                TitlePath = [goal],
                Status = failed ? ResultStatus.Failed : ResultStatus.Passed,
                Source = new SourceLocation("unknown", 0, 0),
                Attempts = [attempt],
            },
        }));
        RunEvents.Emit(new RunFinishedEvent { Status = failed ? ReportRunStatus.Failed : ReportRunStatus.Passed, ExitCode = outcome.ExitCode });
        return Task.FromResult(outcome);
    }

    /// <summary>Runs <c>e2e explore</c> with the fake and recorded output.</summary>
    public async Task<(int Code, string Stdout, string Stderr)> InvokeAsync(params string[] args)
    {
        using var stdout = new LineFeedWriter();
        using var stderr = new LineFeedWriter();
        var code = await ExploreCommand.RunAsync(args, stdout, stderr, interactive: false, RunAsync, CancellationToken.None);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
