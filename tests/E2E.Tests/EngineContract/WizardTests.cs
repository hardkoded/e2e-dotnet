// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;

namespace E2E.Tests.EngineContract;

/// <summary>
/// Upstream's <c>test.describe('wizard', { serial: true, retries: 1 })</c> of <c>engine-contract.test.ts</c>. The port has no runner, no
/// serial suites and no <c>endAttempt</c>, so only the cleanup-exit-code <c>it</c> (tester-army/e2e#1233) is ported, as a partial.
/// </summary>
public sealed class WizardTests
{
    /// <summary>Partial: upstream runs a project over a fake engine whose <c>endAttempt</c> throws; the port has no <c>endAttempt</c>, so this feeds a passed result whose cleanup failed to the exit code the report derives.</summary>
    [Fact]
    public void A_failing_endAttempt_marks_cleanup_failed_with_a_secondary_error_and_exits_3_even_though_the_test_passed_PARTIAL_the_report_derives_the_exit_code_from_a_result_built_by_hand()
    {
        var cleanup = ReportBuilders.Error(message: "attempt end failed: close exploded") with { Category = "infrastructure", Phase = "cleanup" };
        var passed = ReportBuilders.Result(attempts: [ReportBuilders.Attempt(AttemptStatus.Passed, secondaryErrors: [cleanup]) with { Cleanup = AttemptCleanup.Failed }]);

        Assert.Equal((ReportRunStatus.Error, 3), ReportFile.StatusOf([passed]));
    }
}
