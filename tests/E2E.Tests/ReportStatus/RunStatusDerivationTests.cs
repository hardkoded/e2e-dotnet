// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>Upstream's <c>describe('run status derivation')</c> (<c>tests/unit/report-status.test.ts</c>).</summary>
public sealed class RunStatusDerivationTests
{
    [Fact]
    public void Is_blocked_when_every_failing_result_carries_a_blockable_code()
    {
        var document = Build([FailedResult(Blocked)]);

        Assert.Equal(ReportRunStatus.Blocked, document.Run.Status);
    }

    [Fact]
    public void Stays_failed_when_one_result_failed_on_the_product()
    {
        var document = Build([FailedResult(Blocked, "blocked"), FailedResult(ProductFailure, "product")]);

        Assert.Equal(ReportRunStatus.Failed, document.Run.Status);
    }

    [Fact]
    public void Keeps_a_run_level_error_visible_instead_of_calling_the_run_blocked()
    {
        var document = Build([FailedResult(Blocked)], ReportRunStatus.Error, 3, [ReportWriteFailure]);

        Assert.Equal(ReportRunStatus.Error, document.Run.Status);
        Assert.Equal(["REPORT_WRITE_FAILED"], document.Run.Errors.Select(error => error.Code));
    }
}
