// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Traces;

namespace E2E.Tests.EngineContract;

/// <summary>
/// Upstream's <c>test.describe('wizard', { serial: true, retries: 1 })</c> of <c>engine-contract.test.ts</c>. The port has no runner and no
/// serial suites, so only the cleanup-exit-code <c>it</c> (tester-army/e2e#1233) is ported, as a partial.
/// </summary>
public sealed class WizardTests : IDisposable
{
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-wizard-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    /// <summary>Partial: upstream runs a project through the runner over a fake engine whose <c>endAttempt</c> throws; the port has no runner, so this runs one session over an engine whose close throws and reads the session's report (the attempt's <c>cleanup</c> and secondary errors, and the exit code the report derives).</summary>
    [Fact]
    public async Task A_failing_endAttempt_marks_cleanup_failed_with_a_secondary_error_and_exits_3_even_though_the_test_passed_PARTIAL_a_session_stands_for_the_run()
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new FailingCloseEngine(new InvalidOperationException("close exploded")),
            TestTitle = "taps a node",
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });
        session.Complete();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());

        var report = session.Report()!;
        var result = Assert.Single(report.Run.Results);
        Assert.Equal(Reports.ResultStatus.Passed, result.Status);
        var attempt = Assert.Single(result.Attempts);
        Assert.Equal(Reports.AttemptCleanup.Failed, attempt.Cleanup);
        Assert.Contains(attempt.SecondaryErrors, error => error.Phase == Reports.ErrorPhase.Cleanup);
        Assert.Equal(3, report.Run.ExitCode);
    }
}
