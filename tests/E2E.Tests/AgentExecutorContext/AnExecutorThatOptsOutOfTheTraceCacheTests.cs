// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

public sealed class AnExecutorThatOptsOutOfTheTraceCacheTests
{
    /// <summary>
    /// Partial: the port reads the step's cache participation from <see cref="ActResult.Cache"/>, since it has no step record.
    /// </summary>
    [Fact]
    public async Task Leaves_no_cache_participation_on_its_steps_where_the_default_executor_has_one()
    {
        var cached = await ActWithAsync(ExecutorCache.Inherit);
        var uncached = await ActWithAsync(ExecutorCache.Off);

        Assert.NotNull(cached.Cache);
        Assert.Null(uncached.Cache);
    }

    private static async Task<ActResult> ActWithAsync(ExecutorCache cache)
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-executor-cache", Guid.NewGuid().ToString("n"));
        var passing = new TestExecutor("passer", (_, _) => Task.FromResult(Passed("nothing to do"))) { Cache = cache };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            AgentExecutor = passing,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "one act step",
        });
        await session.App.OpenAsync("/");
        var result = await session.Agent.ActAsync("look at the screen");
        session.Complete();
        return result;
    }
}
