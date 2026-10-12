// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineContract;

public sealed class RunnerEngineContractTests
{
    [Fact]
    public async Task Reports_the_step_timeout_when_an_observation_outlives_the_deadline_that_bounded_it()
    {
        // An observation is handed whatever remains of the invocation deadline, so one starting near the end cannot
        // finish. The engine failure that follows describes a truncated budget, not a broken app. Only the second
        // observation stalls, so the first one reads at once.
        var timeout = TimeSpan.FromMilliseconds(300);
        var engine = new StallingEngine(new DocumentEngine(BillingWorld.Create()), timeout + TimeSpan.FromMilliseconds(50));
        var model = new ScriptedModel(_ => ModelResponses.Done("failed", "the Submit button is disabled"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "observe deadline",
            TestTimeout = TimeSpan.FromSeconds(10),
        });

        Exception? error = null;
        try
        {
            await session.App.OpenAsync("/settings/billing");
            await session.Agent.WaitForAsync("the Submit button is enabled", new WaitForOptions
            {
                Timeout = timeout,
                Interval = TimeSpan.FromMilliseconds(100),
            });
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        var failure = Assert.IsType<AgentException>(error);
        Assert.Equal("STEP_TIMEOUT", failure.Code);
        Assert.Contains("the Submit button is disabled", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Wraps an engine so that the second observation outlasts its token, then fails.</summary>
    private sealed class StallingEngine(IEngine inner, TimeSpan stall) : IEngine
    {
        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(await inner.StartAsync(options, cancellationToken).ConfigureAwait(false), stall);

        private sealed class Session(IEngineSession inner, TimeSpan stall) : IEngineSession
        {
            private int _observations;

            public string Route => inner.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

            public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _observations) != 2)
                {
                    return await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(stall, CancellationToken.None).ConfigureAwait(false);
                throw new EngineException("ENGINE_FAILURE", "observation ran out of budget");
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
                inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
