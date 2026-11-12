// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Traces;

namespace E2E.Tests.SkipAfterFailure;

/// <summary>
/// Upstream's <c>describe('a runtime skip after a failure')</c> of <c>skip-after-failure.test.ts</c>. The other <c>it</c> of the describe are
/// ported in <c>E2E.NUnit.Tests/SkipAfterFailure</c>; the one here needs the report's exit code, which the NUnit project does not run.
/// </summary>
public sealed class ARuntimeSkipAfterAFailureTests : IDisposable
{
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-skip-cleanup-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    /// <summary>Partial: upstream runs a project through the runner whose engine's <c>endAttempt</c> throws, with <c>failOnSkippedFailure</c> and a test that calls <c>test.skip</c>, and reads the run's output; the port has no runner, so this runs a skipped session over an engine whose close throws and reads the session's report (cleanup error, status, exit code). The output check ("Skipped After Failure" absent) has no port.</summary>
    [Fact]
    public async Task Exits_3_when_a_skipped_test_cleanup_fails_keeping_the_test_skipped_PARTIAL_a_session_stands_for_the_run()
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new FailingCloseEngine(new InvalidOperationException("cleanup failed")),
            TestTitle = "clean skip",
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });
        session.Complete(new SkipException("not applicable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());

        var report = session.Report()!;
        var result = Assert.Single(report.Run.Results);
        Assert.Equal(Reports.ErrorPhase.Cleanup, Assert.Single(Assert.Single(result.Attempts).SecondaryErrors).Phase);
        Assert.Equal(Reports.ResultStatus.Skipped, result.Status);
        Assert.Equal(3, report.Run.ExitCode);
    }
}
