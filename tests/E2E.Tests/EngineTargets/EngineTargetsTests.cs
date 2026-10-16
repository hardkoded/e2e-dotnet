// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.EngineTargets;

/// <summary>
/// An agent step over a toy engine and a hand-rolled executor, with no model and no browser. Not ported, because the port has no such
/// feature: every other test of upstream's file needs the <c>defineEngine</c> hooks (prepare, init, state, isolation, fixtures,
/// <c>requires</c>), the runner (workers, the report, notices, the cleanup budget), or the screen tier over <c>engine.locate</c>.
/// </summary>
public sealed partial class EngineTargetsTests
{
    /// <summary>
    /// Partial: the port reads the lifecycle of the engine and the counter it drove, and has no report to read the target's engine name and
    /// platform from. The verbs are the ones the engine's capabilities allow: the port's engines declare capabilities, not action kinds.
    /// </summary>
    [Fact]
    public async Task Runs_an_agent_step_over_the_adapter_with_lifecycle_and_honest_provenance()
    {
        var toy = new ToyEngine();
        var executor = new TestExecutor("toy-tapper", async (context, _) =>
        {
            // The executor is told which grammar the surface honors: no scroll and no back, since the toy declares neither.
            var verbs = string.Join(",", context.Target.Verbs.Select(verb => verb.ToString()).Order(StringComparer.Ordinal));
            if (context.Target.Platform != "ios" || verbs != "Check,Clear,DoubleTap,Navigate,Press,Select,Tap,Type,TypeSecret")
            {
                return Failed("unexpected target " + context.Target.Platform + " " + verbs);
            }

            for (var round = 0; round < 5; round++)
            {
                var observation = await context.ObserveAsync();
                var match = CounterReading().Match(observation.Text);
                if (match.Success && match.Groups["value"].Value == "2")
                {
                    return Passed("counter reached 2 after " + round + " taps");
                }

                await context.Actions.TapAsync(new ExecutorTarget("increment"));
            }

            return Failed("counter never reached 2");
        })
        {
            Version = "1",
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = toy,
            AgentExecutor = executor,
            MaxModelCalls = 10,
            CacheEnabled = false,
            TestTitle = "agent drives the toy device",
        });

        var result = await session.Agent.ActAsync("increment the counter to 2");
        session.Complete();

        Assert.Equal(2, toy.Count);
        Assert.Contains("counter reached 2", result.Summary, StringComparison.Ordinal);
        Assert.Equal("ios", toy.Platform);
        Assert.Equal(["init"], toy.Lifecycle);
        await session.DisposeAsync();
        Assert.Equal(["init", "dispose"], toy.Lifecycle);
    }

    [GeneratedRegex("status \"count\" text=\"(?<value>\\d+)\"")]
    private static partial Regex CounterReading();

    /// <summary>A two-node screen: a counter value and a button that increments it. Refs are stable, as a device's are not but this toy's are.</summary>
    private sealed class ToyEngine : IEngine
    {
        public List<string> Lifecycle { get; } = [];

        public int Count { get; private set; }

        public string Platform => "ios";

        public string Version => "1.0.0";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            Lifecycle.Add("init");
            return Task.FromResult<IEngineSession>(new Session(this));
        }

        private sealed class Session(ToyEngine engine) : IEngineSession
        {
            public string Route => "/";

            public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new Observation
                {
                    Route = "/",
                    Roots =
                    [
                        new SemanticNode { Ref = "counter", Role = "status", Name = "count", Text = engine.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        new SemanticNode { Ref = "increment", Role = "button", Name = "Increment" },
                    ],
                });

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
            {
                if (node.Ref != "increment" || action is not LocatorAction.Tap)
                {
                    throw new EngineException("NOT_ACTIONABLE", "the toy only taps its button");
                }

                engine.Count++;
                return Task.CompletedTask;
            }

            public Task PressAsync(string key, CancellationToken cancellationToken) => throw new EngineException("UNSUPPORTED_CAPABILITY", "the toy has no keyboard");

            public ValueTask DisposeAsync()
            {
                engine.Lifecycle.Add("dispose");
                return ValueTask.CompletedTask;
            }
        }
    }
}
