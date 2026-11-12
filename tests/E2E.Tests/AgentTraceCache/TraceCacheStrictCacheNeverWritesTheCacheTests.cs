// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Tests.AgentTraceCache;

/// <summary>Upstream: packages/e2e/tests/integration/agent-trace-cache.test.ts, describe('trace cache: --strict-cache never writes the cache').</summary>
public sealed class TraceCacheStrictCacheNeverWritesTheCacheTests
{
    private static RunProjectOptions Options(TwoTapCounter.ExecutorRecord record, bool strictCache, IReadOnlyList<InMemoryTest>? tests = null) =>
        TwoTapCounter.RunOptions(record) with
        {
            Config = """{ "cache": { "mode": "read-write" }, "retries": 1 }""",
            StrictCache = strictCache,
            Tests = tests ?? TwoTapCounter.Suite(),
        };

    [Fact]
    public async Task Replays_on_a_retry_and_leaves_the_entry_exactly_as_recorded_when_the_first_attempt_fails()
    {
        var project = RunProject.CreateProject();
        var cacheDirectory = Path.Combine(project.Dir, ".e2e", "cache");
        var failOnce = Path.Combine(project.Dir, "fail-once");

        // The suite fails its first attempt, after the step, when the marker file exists.
        IReadOnlyList<InMemoryTest> suite =
        [
            new InMemoryTest(TwoTapCounter.Title, async session =>
            {
                await session.App.OpenAsync("/");
                await session.Agent.ActAsync(TwoTapCounter.Instruction);
                if (File.Exists(failOnce))
                {
                    File.Delete(failOnce);
                    throw new InvalidOperationException("the first attempt fails before anything verified the step");
                }

                await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("2");
            }),
        ];
        try
        {
            var recording = await RunProject.RunExistingAsync(project, Options(new TwoTapCounter.ExecutorRecord(), false, suite));
            Assert.Equal(0, recording.ExitCode);
            var recorded = TwoTapCounter.FileState(cacheDirectory);
            var entry = Path.GetFileNameWithoutExtension(Directory.GetFiles(cacheDirectory).Single());

            await File.WriteAllTextAsync(failOnce, "");
            var strict = new TwoTapCounter.ExecutorRecord();
            var outcome = await RunProject.RunExistingAsync(project, Options(strict, true, suite));
            Assert.Equal(0, outcome.ExitCode);
            var attempts = RunProject.ResultByTitle(outcome, TwoTapCounter.Title).Attempts;
            Assert.Equal([AttemptStatus.Failed, AttemptStatus.Passed], attempts.Select(attempt => attempt.Status));
            Assert.Equal(0, strict.Calls);
            foreach (var attempt in attempts)
            {
                var cached = Assert.Single(attempt.Steps, step => step.Api == "agent.act").Cache!;
                Assert.Equal((E2E.Reports.CacheMode.SelfFinalized, 2, 2, entry), (cached.Mode, cached.ReplayedActions, cached.TotalActions, cached.Entry));
            }

            Assert.Equal(recorded, TwoTapCounter.FileState(cacheDirectory));
        }
        finally
        {
            project.Cleanup();
        }
    }

    [Fact]
    public async Task Runs_a_never_recorded_step_live_without_recording_it()
    {
        var project = RunProject.CreateProject();
        try
        {
            var strict = new TwoTapCounter.ExecutorRecord();
            var outcome = await RunProject.RunExistingAsync(project, Options(strict, true));
            Assert.Equal(0, outcome.ExitCode);
            Assert.Equal(1, strict.Calls);
            Assert.False(Directory.Exists(Path.Combine(project.Dir, ".e2e", "cache")));
        }
        finally
        {
            project.Cleanup();
        }
    }
}
