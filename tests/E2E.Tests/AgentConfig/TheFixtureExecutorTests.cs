// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentConfig;

/// <summary>.NET-only: a test base class sets the default agent's executor in code, since the config file cannot.</summary>
public sealed class TheFixtureExecutorTests
{
    [Fact]
    public async Task Runs_the_agent_executor_of_a_fixture_as_the_default_agent()
    {
        var seen = new List<string>();
        var fixture = new Fixture(new TestExecutor("fixture-brain", (context, _) =>
        {
            seen.Add(context.Step.Instruction);
            return Task.FromResult(Passed("done"));
        }));
        await fixture.StartAsync();

        var result = await fixture.ActAsync("look at the screen");
        await fixture.FinishAsync();

        Assert.Equal("done", result.Summary);
        Assert.Equal(["look at the screen"], seen);
    }

    private sealed class Fixture(IStepExecutor executor) : E2EFixture
    {
        protected override E2EConfig Config => E2EConfig.Parse("{}", Path.GetTempPath(), _ => null);

        protected override IEngine CreateEngine() => new DocumentEngine(CounterPage.World());

        protected override string? BaseUrl => "https://counter.test";

        protected override CacheMode CacheMode => CacheMode.Off;

        protected override IStepExecutor? AgentExecutor => executor;

        public Task StartAsync() => StartSessionAsync("fixture executor", 1, null, () => false, CancellationToken.None);

        public Task<ActResult> ActAsync(string instruction) => Agent.ActAsync(instruction);

        public async Task FinishAsync()
        {
            var session = TakeSession()!;
            session.Complete();
            await session.DisposeAsync();
        }
    }
}
