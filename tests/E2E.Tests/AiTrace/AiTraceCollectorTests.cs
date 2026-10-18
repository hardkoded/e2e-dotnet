// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.AiTrace;

/// <summary>Upstream's <c>AiTraceCollector</c> describe.</summary>
public sealed class AiTraceCollectorTests
{
    [Fact]
    public void Merges_snapshots_and_orders_records_by_start_time()
    {
        var collector = new AiTraceCollector();
        static AiTraceRun Run(string id, string startedAt) => new() { Id = id, StartedAt = startedAt };
        static AiTraceStep Step(string id, string runId, string startedAt) => new() { Id = id, RunId = runId, StepNumber = 1, ModelId = "m", StartedAt = startedAt, DurationMs = 1, Input = "{}" };
        collector.Merge(new AiTraceDocument { Runs = [Run("b", "2026-01-01T00:00:02Z")], Steps = [Step("s2", "b", "2026-01-01T00:00:02Z")] });
        collector.Merge(new AiTraceDocument { Runs = [Run("a", "2026-01-01T00:00:01Z")], Steps = [Step("s1", "a", "2026-01-01T00:00:01Z")] });

        var document = collector.Document();

        Assert.Equal(["a", "b"], document.Runs.Select(run => run.Id));
        Assert.Equal(["s1", "s2"], document.Steps.Select(step => step.Id));
        Assert.Equal(2, collector.Size);
    }
}
