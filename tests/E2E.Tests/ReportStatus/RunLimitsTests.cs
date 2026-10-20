// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>
/// Upstream's <c>describe('run limits')</c> (<c>tests/unit/report-status.test.ts</c>). Not ported: "fills the block from the largest
/// per-agent values and the fixed ones", since the port's config rejects the agent keys <c>maxObservationBytes</c> and <c>maxInputTokens</c>.
/// </summary>
public sealed class RunLimitsTests
{
    [Fact]
    public void Reports_the_defaults_when_the_run_failed_before_the_config_resolved()
    {
        Assert.Equal(new ReportLimits(16_384, 8_192, 262_144, 1_000, 64_000), Build().Run.Limits);
    }
}
