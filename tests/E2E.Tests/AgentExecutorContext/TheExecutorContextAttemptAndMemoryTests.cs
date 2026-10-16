// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>
/// The attempt an executor's context names, and the memory it keeps. Upstream runs two tests of a project and reads the context
/// each step received. The port runs two sessions, one per test, which is what a test framework adapter does.
/// </summary>
public sealed class TheExecutorContextAttemptAndMemoryTests : IAsyncLifetime
{
    private readonly List<Probe> _probes = [];
    private readonly TestExecutor _probing;

    public TheExecutorContextAttemptAndMemoryTests()
    {
        _probing = new TestExecutor("context-probe", (context, _) =>
        {
            _probes.Add(new Probe(
                context.Step.Instruction,
                context.Attempt.TestId,
                context.Attempt.AttemptId,
                context.Attempt.Index,
                context.Step.Index,
                context.Attempt.Memory.TryGetValue("probe", out var before) ? before : null,
                context.Attempt.CancellationToken));
            context.Attempt.Memory["probe"] = _probes.Count;
            return Task.FromResult(Passed("done: " + context.Step.Instruction));
        })
        {
            Version = "1",
        };
    }

    public async Task InitializeAsync()
    {
        await RunTestAsync("first test runs two steps", "step one", "step two");
        await RunTestAsync("second test starts fresh", "step three");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Passes_both_tests_and_ran_every_step_through_the_executor()
    {
        Assert.Equal(["step one", "step two", "step three"], _probes.Select(probe => probe.Instruction));
    }

    /// <summary>
    /// Partial: the port has no step timeline, so it numbers the agent steps of the attempt from zero. Upstream's <c>app.open</c>
    /// is step 0 and the first act is step 1.
    /// </summary>
    [Fact]
    public void Names_the_test_and_attempt_and_numbers_the_step_in_the_attempt_timeline()
    {
        Assert.Equal("first test runs two steps", _probes[0].TestId);
        Assert.NotEmpty(_probes[0].AttemptId);
        Assert.Equal(0, _probes[0].AttemptIndex);
        Assert.Equal(0, _probes[0].StepIndex);
        Assert.Equal(1, _probes[1].StepIndex);
        Assert.Equal(_probes[0].AttemptId, _probes[1].AttemptId);
        Assert.NotEqual(_probes[0].AttemptId, _probes[2].AttemptId);
        Assert.Equal("second test starts fresh", _probes[2].TestId);
    }

    [Fact]
    public void Keeps_memory_across_the_steps_of_one_attempt_and_never_across_tests()
    {
        Assert.Null(_probes[0].MemoryBefore);
        Assert.Equal(1, _probes[1].MemoryBefore);
        Assert.Null(_probes[2].MemoryBefore);
    }

    [Fact]
    public void Ends_the_attempt_signal_once_the_attempt_is_over()
    {
        Assert.All(_probes, probe => Assert.True(probe.Token.IsCancellationRequested));
    }

    private async Task RunTestAsync(string title, params string[] steps)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            AgentExecutor = _probing,
            BaseUrl = "https://counter.test",
            TestTitle = title,
        });
        await session.App.OpenAsync("/");
        foreach (var step in steps)
        {
            await session.Agent.ActAsync(step);
        }

        session.Complete();
    }

    private sealed record Probe(string Instruction, string TestId, string AttemptId, int AttemptIndex, int StepIndex, object? MemoryBefore, CancellationToken Token);
}
