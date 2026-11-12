// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportStatus;

/// <summary>Upstream's <c>describe('run limits')</c> (<c>tests/unit/report-status.test.ts</c>).</summary>
public sealed class RunLimitsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-run-limits-project");

    /// <summary>Partial: upstream resolves the config with <c>cli: { agents: ['ux'] }</c> and builds the report from its <c>ResolvedConfig</c>. The port has no agent selection on a run, so the limits come from the config's agents (<c>ResolvedAgent.RunLimits</c>), which hold whichever one runs.</summary>
    [Fact]
    public void Partial_fills_the_block_from_the_largest_per_agent_values_and_the_fixed_ones_in_the_report_1_shape()
    {
        var config = E2EConfig.Parse(
            """
            { "agents": { "default": { "maxObservationBytes": 4096, "maxInputTokens": 200000 }, "ux": { "maxObservationBytes": 65536, "maxInputTokens": 8000 } } }
            """,
            Root,
            _ => null);
        var limits = ResolvedAgent.RunLimits(config.Agents.Select(pair => ResolvedAgent.From(pair.Key, pair.Value.CreateOptions())));

        Assert.Equal(new ReportLimits(16_384, 8_192, 65_536, 1_000, 200_000), Build(limits: limits).Run.Limits);
    }

    [Fact]
    public void Reports_the_defaults_when_the_run_failed_before_the_config_resolved()
    {
        Assert.Equal(new ReportLimits(16_384, 8_192, 262_144, 1_000, 64_000), Build().Run.Limits);
    }
}
