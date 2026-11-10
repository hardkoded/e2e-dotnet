// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentFallbackHandoff;

/// <summary>What one fallback test's session runs with: the engine, the agent, the judge, and the replay cache.</summary>
internal sealed class FallbackSetup
{
    public required FallbackEngine Engine { get; init; }

    public IStepExecutor? Executor { get; init; }

    public IAgentModel? Model { get; init; }

    public IAgentModel? Judge { get; init; }

    /// <summary>The replay store. Null keeps the session's own file store under a fresh directory.</summary>
    public IStepCache? Cache { get; init; }

    /// <summary>Upstream's <c>cache: 'off'</c> is false.</summary>
    public bool CacheEnabled { get; init; } = true;

    /// <summary>The engine's action timeout. Upstream's <c>actionTimeout</c>, which the default is when unset.</summary>
    public TimeSpan? ActionTimeout { get; init; }
}

/// <summary>
/// What a fallback session left: the error the attempt ended with, and what the <c>agent.act</c> step the instruction ran as recorded.
/// The step record is internal to the core, so the outcome carries its facts.
/// </summary>
public sealed class FallbackOutcome
{
    /// <summary>The error the attempt ended with, or null when it passed.</summary>
    public Exception? Error { get; init; }

    /// <summary>Whether the <c>agent.act</c> step passed. False when the session has no act step.</summary>
    public bool ActPassed { get; init; }

    /// <summary>The replay record of the <c>agent.act</c> step, or null when it replayed nothing.</summary>
    public CacheInfo? ActCache { get; init; }

    /// <summary>Whether the <c>agent.act</c> step sent a screenshot to the model, or null when it recorded none.</summary>
    public bool? ActVisionInput { get; init; }
}

/// <summary>
/// Runs one test's body in a session on the fallback engine (upstream's <c>runProject</c> of its one-test suite). The port runs the
/// body in a session, not a project, so the attempt's error is returned instead of read from a report.
/// </summary>
internal static class FallbackRun
{
    /// <summary>A replay store that answers every read with one entry and takes every write. Upstream's <c>store</c> of <c>cache: { mode: 'read-write' }</c>.</summary>
    public sealed class HitStore(CacheEntry entry) : IStepCache
    {
        public bool Writable => true;

        public CacheLookup Read(string key) => new() { Entry = entry };

        public int? Write(string key, CacheEntry written) => 1;

        public void Delete(string key)
        {
        }
    }

    /// <summary>Starts a session on the setup, runs <paramref name="body"/> in it, and returns the error and the step records it wrote.</summary>
    public static async Task<FallbackOutcome> RunAsync(FallbackSetup setup, Func<E2ESession, Task> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-fallback-" + Guid.NewGuid().ToString("N"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = setup.Engine,
            Model = setup.Model,
            Judge = setup.Judge,
            AgentExecutor = setup.Executor,
            BaseUrl = "https://fixture.test",
            Cache = setup.Cache ?? new FileStepCache(directory),
            CacheEnabled = setup.CacheEnabled,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "semantic fallback handoff",
            ActionTimeout = setup.ActionTimeout ?? TimeSpan.FromSeconds(5),
            AssertionTimeout = TimeSpan.FromSeconds(4),
            StepTimeout = TimeSpan.FromSeconds(10),
            TestTimeout = TimeSpan.FromSeconds(60),
            ResultsDirectory = Path.Combine(directory, "results"),
        });
        Exception? error = null;
        try
        {
            await session.App.OpenAsync();
            await body(session);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        var act = session.StepRecords.SingleOrDefault(step => step.Api == "agent.act");
        return new FallbackOutcome
        {
            Error = error,
            ActPassed = act?.Status == StepStatus.Passed,
            ActCache = act?.Cache,
            ActVisionInput = act?.VisionInput,
        };
    }

    /// <summary>Runs the one instruction the fallback tests give the default agent, as upstream's suite does.</summary>
    public static Task<FallbackOutcome> ActAsync(FallbackSetup setup, TimeSpan? timeout = null) =>
        RunAsync(setup, session => session.Agent.ActAsync("inspect the current result", new ActOptions { Timeout = timeout ?? TimeSpan.FromSeconds(1.5) }));
}
