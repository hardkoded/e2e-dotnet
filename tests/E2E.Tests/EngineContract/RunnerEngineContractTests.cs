// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;

namespace E2E.Tests.EngineContract;

/// <summary>
/// Ported from upstream's <c>engine-contract.test.ts</c>, whose tests run a project through the CLI over a fake engine. The
/// port runs each body in a session over a custom <see cref="IEngine"/>. Not ported, because the port has no such feature:
/// every other test of the file (the settleAttempt one is ported, as a partial) needs the <c>defineEngine</c> hooks (init, dispose, endAttempt), the runner
/// (retries, workers, serial suites, exit codes, run errors, plan notices), the report, artifacts, sessions and state,
/// engine-contributed fixtures, or the <c>requires</c> test option. The video tests of the file are in
/// <c>VideoArtifactsTests</c>. Also not ported, with a feature gap: "names
/// APP_UNREACHABLE, with the URL, when the network refused the navigation", "re-observes when an engine reports a retryable
/// observation failure", and "fails an agent call when a non-retryable observation failure repeats". Of the two vision-hint tests
/// the file keeps, the second is partial and renamed: the port has no step record, so it cannot read the first judgment's
/// <c>visionDegraded</c> or <c>visionInput</c>.
/// </summary>
public sealed class RunnerEngineContractTests
{
    [Fact]
    public async Task Maps_a_hung_startAttempt_to_an_environment_unavailable_error_and_tells_the_hook_to_stop()
    {
        // Partial: the port names the code ENVIRONMENT_UNAVAILABLE, not LAUNCH_TIMEOUT, and a failed launch has no
        // session to end, so there is no attemptsEnded count to check. The category, the retry and the report are the runner's.
        var engine = new HangingEngine();
        var error = await Assert.ThrowsAsync<EngineException>(() => E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "launch hang",
            LaunchTimeout = TimeSpan.FromMilliseconds(50),
        }));
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", error.Code);
        Assert.Contains("launch timeout", error.Message, StringComparison.Ordinal);
        Assert.True(engine.Stopped);
    }

    [Fact]
    public async Task Surfaces_UNSUPPORTED_ARTIFACT_before_any_attempt_when_a_target_demands_more_than_the_engine_offers_PARTIAL_a_session_is_refused_where_a_run_has_no_attempt_started()
    {
        // The engine hangs when started, so a refusal that comes back at all came before the engine started.
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new HangingEngine(),
            TestTitle = "artifact",
            TargetName = "fake",
            Video = new ResolvedRecording(RecordingMode.On, RecordingSource.Target),
        }));
        Assert.Equal("UNSUPPORTED_ARTIFACT", error.Code);
        Assert.Equal("target \"fake\" (engine web) cannot record video, and the target sets video: 'on'", error.Message);
    }

    [Fact]
    public async Task A_failure_settleAttempt_throws_after_the_body_is_the_verdict_with_its_own_code_PARTIAL_the_phase_the_cleanup_field_and_the_exit_code_are_the_runners()
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new SettlingEngine(new DocumentEngine(BillingWorld.Create())),
            BaseUrl = "https://billing.test",
            TestTitle = "taps a node",
        });
        await session.App.OpenAsync("/settings/billing");
        var error = await Assert.ThrowsAsync<TestException>(() => session.SettleAttemptAsync());
        Assert.Equal("ASSERTION_FAILED", error.Code);
        session.Complete(error);
    }

    [Fact]
    public async Task Reaching_a_fixture_the_engine_does_not_contribute_is_a_configuration_error()
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            BaseUrl = "https://billing.test",
            TestTitle = "needs browser",
        });
        await session.App.OpenAsync("/settings/billing");
        var error = await Assert.ThrowsAsync<EngineException>(() => session.Browser.GotoAsync("/somewhere"));
        Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
        Assert.Contains("browser.goto needs a browser engine", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_the_step_timeout_when_an_observation_outlives_the_deadline_that_bounded_it()
    {
        // An observation is handed whatever remains of the invocation deadline, so one starting near the end cannot
        // finish. The engine failure that follows describes a truncated budget, not a broken app. Only the second
        // observation stalls, so the first one reads at once.
        var timeout = TimeSpan.FromMilliseconds(300);
        var engine = new StallingEngine(new DocumentEngine(BillingWorld.Create()), TimeSpan.FromMilliseconds(50));
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

    [Fact]
    public async Task Leaves_vision_out_of_an_inconclusive_judgment_on_an_engine_that_declares_no_screenshot_capture()
    {
        var error = await InconclusiveAsync(new DocumentEngine(BillingWorld.Create()));
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        // A retry with Vision = VisionMode.On could only record UnsupportedCapability here.
        Assert.Equal("the tree lists no state for the Submit button", error.Message);
    }

    [Fact]
    public async Task Leaves_the_vision_hint_off_once_a_pixel_request_of_the_attempt_was_degraded()
    {
        // The engine declares screenshots and returns no pixels: the first judgment asked for them, got the tree, and
        // recorded the degradation. Asking again could only degrade again, so the hint would send the user down a path
        // this attempt has already seen fail.
        var error = await InconclusiveAsync(new ScreenshotEngine(BillingWorld.Create()), vision: true);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        Assert.Equal("the tree lists no state for the Submit button", error.Message);
    }

    private static async Task<AgentException> InconclusiveAsync(IEngine engine, bool vision = false)
    {
        var model = new ScriptedModel(request => request.Messages[0].Content!.Contains("Statement: the Submit button is visible", StringComparison.Ordinal)
            ? ModelResponses.Done("passed", "the Submit button is listed")
            : ModelResponses.Done("failed", "the tree lists no state for the Submit button", "ASSERTION_INCONCLUSIVE"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Judge = model,
            BaseUrl = "https://billing.test",
            TestTitle = "inconclusive judgment",
            TestTimeout = TimeSpan.FromSeconds(10),
        });
        await session.App.OpenAsync("/settings/billing");
        if (vision)
        {
            await session.Agent.AssertAsync("the Submit button is visible", new AssertOptions { Vision = VisionMode.On });
        }

        var failure = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the Submit button is enabled"));
        session.Complete(failure);
        return failure;
    }

    internal sealed class HangingEngine : IEngine
    {
        private CancellationToken _token;

        public bool Stopped => _token.IsCancellationRequested;

        public string Platform => "web";

        public string Version => "1.0";

        public EngineCapabilities Capabilities => EngineCapabilities.None;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            _token = cancellationToken;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    /// <summary>Wraps an engine whose session throws from <c>SettleAttemptAsync</c> (the fake engine's <c>onSettleAttempt</c>).</summary>
    private sealed class SettlingEngine(IEngine inner) : IEngine
    {
        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(IEngineSession inner) : IEngineSession
        {
            public string Route => inner.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => inner.ObserveAsync(cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
                inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public Task SettleAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
                throw new TestException("ASSERTION_FAILED", "a callback the body left running failed");

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    /// <summary>Wraps an engine so that the second observation fails 50 ms after its token is cancelled.</summary>
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

                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await Task.Delay(stall, CancellationToken.None).ConfigureAwait(false);
                }

                throw new EngineException("ENGINE_FAILURE", "observation ran out of budget");
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
                inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
