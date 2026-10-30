// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('summary'). The port has no <c>ListReporter</c>, so only the
/// cache tally the reporter prints in its <c>Cache</c> row is ported, over <see cref="Format"/>. Not ported: the rest of the
/// describe, which drives the reporter with run events.
/// </summary>
public sealed class SummaryTests
{
    [Fact]
    public void Partial_tallies_the_trace_cache_under_the_AI_row_by_step_leaving_zero_counts_out()
    {
        // Upstream feeds the four steps to the reporter and reads the line after the AI row; here the row is built from the tally.
        var steps = new[]
        {
            Step("replayed-1", new CacheInfo { Mode = "self-finalized", ReplayedActions = 3, TotalActions = 3 }, 0),
            Step("replayed-2", new CacheInfo { Mode = "self-finalized", ReplayedActions = 2, TotalActions = 2 }, 0),
            Step("handed-off", new CacheInfo { Mode = "agent-concluded", Reason = "end-mismatch", ReplayedActions = 1, TotalActions = 3 }, 2),
            Step("uncached", null, 3),
        };

        var pc = new Colors(false);
        var cache = Format.CacheText(pc, Format.StepsCacheTally([steps]));

        Assert.Equal("      Cache  2 replayed · 1 handed off", Format.PadTitle(pc, "Cache") + cache);
    }

    private static StepRecord Step(string id, CacheInfo? cache, int calls) =>
        new()
        {
            Id = id,
            Index = 0,
            Kind = StepKind.Agent,
            Api = "agent.act",
            Label = "do it",
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = 10,
            Cache = cache,
            Model = new StepModelInfo { Provider = "openai", Model = "gpt-5.6-luna-fast", Calls = calls, InputTokens = 1_000, OutputTokens = 100 },
        };
}
