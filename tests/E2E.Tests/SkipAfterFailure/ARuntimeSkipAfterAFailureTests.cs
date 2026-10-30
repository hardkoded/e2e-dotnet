// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;

namespace E2E.Tests.SkipAfterFailure;

/// <summary>
/// Upstream's <c>describe('a runtime skip after a failure')</c> of <c>skip-after-failure.test.ts</c>. The other <c>it</c> of the describe are
/// ported in <c>E2E.NUnit.Tests/SkipAfterFailure</c>; the one here needs the report's exit code, which the NUnit project does not run.
/// </summary>
public sealed class ARuntimeSkipAfterAFailureTests
{
    /// <summary>Partial: upstream runs a skipped test whose engine's <c>endAttempt</c> throws; the port has no such engine, so this feeds a skipped result whose cleanup failed to the report.</summary>
    [Fact]
    public void Exits_3_when_a_skipped_test_cleanup_fails_keeping_the_test_skipped_PARTIAL_the_report_derives_the_exit_code_from_a_result_built_by_hand()
    {
        var cleanup = ReportBuilders.Error() with { Category = "infrastructure", Phase = "cleanup" };
        var skipped = ReportBuilders.Result(attempts: [ReportBuilders.Attempt(AttemptStatus.Skipped, secondaryErrors: [cleanup]) with { Cleanup = AttemptCleanup.Failed }]) with { Status = ResultStatus.Skipped };

        Assert.Equal((ReportRunStatus.Error, 3), ReportFile.StatusOf([skipped]));
    }
}
