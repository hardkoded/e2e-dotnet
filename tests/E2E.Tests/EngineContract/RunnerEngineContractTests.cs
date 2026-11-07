// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.Select;
using static E2E.Tests.Select.SelectKit;

namespace E2E.Tests.EngineContract;

/// <summary>
/// Ported from upstream's <c>engine-contract.test.ts</c>, whose tests run a project through the CLI over a fake engine. The
/// port runs each body in a session over a custom <see cref="IEngine"/>. Not ported, because the port has no such feature:
/// every other test of the file (the settleAttempt one is ported, as a partial) needs the <c>defineEngine</c> hooks (init, endAttempt), the runner
/// (retries, workers, serial suites, plan notices), or sessions across tests and state. The tests that read a run are
/// partial: one session stands for the run, and its report for the run's. "runs a contributed fixture with harness
/// discipline: steps, namespaces, accessors, bounds, matchers, artifacts" is not ported: the port has no engine-contributed
/// fixtures (issue 1197). Also not ported, with a feature gap: "re-observes when an engine reports a retryable observation failure", and
/// "fails an agent call when a non-retryable observation failure repeats". The video tests of the file are in
/// <c>VideoArtifactsTests</c>. Of the two vision-hint tests the file keeps, the second is partial and renamed: the port has no step
/// record, so it cannot read the first judgment's <c>visionDegraded</c> or <c>visionInput</c>.
/// </summary>
public sealed class RunnerEngineContractTests : IDisposable
{
    // The output directory holds the results and the run's report.json beside them.
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-engine-contract-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

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
    public async Task Partial_Threads_a_consistent_live_OperationContext_through_every_engine_call()
    {
        var fake = Fake();
        await using var session = await StartAsync(fake, "taps a node");
        await session.App.OpenAsync("/settings/billing");
        await session.Screen.GetByRole("button", "Submit").TapAsync();

        var calls = fake.Calls;
        Assert.True(calls.Count > 2);
        foreach (var call in calls)
        {
            Assert.False(call.Cancelled, call.Method);
            Assert.True(call.Timeout is null || call.Timeout >= TimeSpan.FromMilliseconds(1), call.Method);
            Assert.True(call.Context is null || call.Context.Origin == OperationOrigin.Test, call.Method);
            if (call.Context is { } context)
            {
                Assert.Equal(session.RunId, context.RunId);
                Assert.NotEqual("", context.AttemptId);
                Assert.Equal(session.AttemptId, context.AttemptId);
            }
        }

        // Partial: only the calls that take an OperationContext carry the ids (Open, Observe, Press and the rest take none).
        var withContext = calls.Where(call => call.Context is not null).ToList();
        Assert.True(withContext.Count > 0);
        Assert.Single(withContext.Select(call => call.Context!.AttemptId).Distinct());

        var performs = calls.Where(call => call.Method == nameof(IEngineSession.PerformAsync)).ToList();
        Assert.NotEmpty(performs);
        Assert.All(performs, call => Assert.Equal(OperationOrigin.Test, call.Context?.Origin));
        Assert.NotEmpty(session.RunId);
        Assert.NotEmpty(session.AttemptId);
        Assert.Single(fake.AttemptIds);
        Assert.Equal(session.AttemptId, fake.AttemptIds[0]);
        Assert.Equal(session.RunId, Assert.Single(fake.RunIds));
    }

    [Fact]
    public async Task Classifies_EngineErrors_from_fixture_surfaces_app_open_with_the_canonical_mapping_and_a_plain_Error_as_ENGINE_FAILURE_PARTIAL_a_session_stands_for_the_run()
    {
        var failure = Fake(onNavigate: _ => throw new EngineException("ENGINE_FAILURE", "renderer crashed"));
        var (result, run) = await FailOpeningAsync(failure, "taps a node");
        Assert.Equal(Reports.ResultStatus.Failed, result.Status);
        Assert.Equal(Reports.ErrorCategory.Infrastructure, result.Attempts[0].Error!.Category);
        Assert.Equal("ENGINE_FAILURE", result.Attempts[0].Error!.Code);
        Assert.Equal(3, run.ExitCode);
        Assert.Equal(1, failure.Disposed);

        var unsupported = Fake(onNavigate: _ => throw new EngineException("UNSUPPORTED_CAPABILITY", "deep links unsupported"));
        var (secondResult, second) = await FailOpeningAsync(unsupported, "taps a node");
        Assert.Equal(Reports.ErrorCategory.Configuration, secondResult.Attempts[0].Error!.Category);
        Assert.Equal("UNSUPPORTED_CAPABILITY", secondResult.Attempts[0].Error!.Code);
        Assert.Equal(2, second.ExitCode);
        Assert.Equal(1, unsupported.Disposed);

        var plain = Fake(onNavigate: _ => throw new InvalidOperationException("renderer gone"));
        var (thirdResult, third) = await FailOpeningAsync(plain, "taps a node");
        Assert.Equal(Reports.ResultStatus.Failed, thirdResult.Status);
        Assert.Equal(Reports.ErrorCategory.Infrastructure, thirdResult.Attempts[0].Error!.Category);
        Assert.Equal("ENGINE_FAILURE", thirdResult.Attempts[0].Error!.Code);
        Assert.Contains("renderer gone", thirdResult.Attempts[0].Error!.Message, StringComparison.Ordinal);
        Assert.Equal(3, third.ExitCode);
        Assert.Equal(1, plain.Disposed);
    }

    /// <summary>.NET-only (no upstream <c>it</c>): the engine-to-session adapter keeps a cancellation a cancellation, as it keeps a plain error an ENGINE_FAILURE.</summary>
    [Fact]
    public async Task A_cancellation_an_engine_throws_stays_a_cancellation_and_is_not_normalized_to_ENGINE_FAILURE()
    {
        var fake = Fake(onNavigate: _ => throw new OperationCanceledException("cancelled"));
        await using var session = await StartAsync(fake, "taps a node");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.App.OpenAsync("/settings/billing"));
        session.Complete();
    }

    [Fact]
    public async Task Names_APP_UNREACHABLE_with_the_URL_when_the_network_refused_the_navigation_PARTIAL_a_session_stands_for_the_run()
    {
        var refused = Fake(onNavigate: url => throw new EngineException("ENGINE_FAILURE", $"navigation failed: page.goto: net::ERR_CONNECTION_REFUSED at {url}"));
        var (result, run) = await FailOpeningAsync(refused, "taps a node");
        Assert.Equal(Reports.ResultStatus.Failed, result.Status);
        var error = result.Attempts[0].Error!;
        Assert.Equal(Reports.ErrorCategory.Infrastructure, error.Category);
        Assert.Equal("APP_UNREACHABLE", error.Code);
        Assert.Matches(
            @"^nothing answered at https?://\S+ \(ERR_CONNECTION_REFUSED\); start the app there, point the target's app.url at where it runs, or give the target an app.command so the runner starts it\z",
            error.Message);
        Assert.Equal(3, run.ExitCode);
    }

    [Fact]
    public async Task A_failing_dispose_is_a_cleanup_run_error_and_fails_the_run_PARTIAL_the_engine_session_is_released_with_each_attempt_and_a_session_stands_for_the_run()
    {
        var fake = Fake(onDispose: () => throw new InvalidOperationException("device lease release exploded"));
        await using var session = await StartAsync(fake, "taps a node");
        await session.App.OpenAsync("/settings/billing");
        await session.Screen.GetByRole("button", "Submit").TapAsync();
        session.Complete();
        await Assert.ThrowsAnyAsync<Exception>(() => session.DisposeAsync().AsTask());

        var report = session.Report()!;
        var result = Assert.Single(report.Run.Results);
        Assert.Equal(Reports.ResultStatus.Passed, result.Status);
        var attempt = Assert.Single(result.Attempts);
        Assert.Equal(Reports.AttemptCleanup.Failed, attempt.Cleanup);
        var disposal = Assert.Single(attempt.SecondaryErrors, error => error.Message.Contains("device lease release exploded", StringComparison.Ordinal));
        Assert.Equal(Reports.ErrorPhase.Cleanup, disposal.Phase);
        Assert.Equal(Reports.ErrorCategory.Infrastructure, disposal.Category);
        Assert.Equal(3, report.Run.ExitCode);
    }

    [Fact]
    public void Gates_requires_against_the_declared_capability_set_at_selection_PARTIAL_the_selection_is_read_where_a_run_reads_the_statuses()
    {
        // The engine declares a capability of its own, as upstream's fake does with its gadget fixture (the port has no contributed fixtures).
        var col = CollectionOf(
            "tests/requires.e2e.ts",
            new Decl("needs gadget") { Requires = ["gadget"] },
            new Decl("needs web") { Requires = ["web"] });
        var cfg = ConfigOf(targets: [Web("fake", new ResolvedEngine("fake", new HashSet<string> { "actions", "location", "observation", "gadget" }))]);
        var selection = Selector.Select(col, cfg, new SelectionFilters());
        var gadget = Assert.Single(selection.Pairs, pair => pair.Test.Title == "needs gadget");
        Assert.Equal(Disposition.Run, gadget.Disposition);
        var web = Assert.Single(selection.Pairs, pair => pair.Test.Title == "needs web");
        Assert.Equal(Disposition.Skip, web.Disposition);
        Assert.Equal("capability-unavailable", web.Skip?.Cause);
    }

    [Fact]
    public async Task Registers_an_engine_screenshot_as_an_attempt_artifact()
    {
        var fake = Fake();
        await using var session = await StartAsync(fake, "takes evidence");
        await session.App.OpenAsync("/settings/billing");
        await session.App.ScreenshotAsync("after-open");
        session.Complete();
        await session.DisposeAsync();

        var result = Assert.Single(session.Report()!.Run.Results);
        Assert.Equal(Reports.ResultStatus.Passed, result.Status);
        Assert.Contains(result.Attempts[0].Artifacts, artifact => artifact.Kind == Reports.ArtifactKind.Screenshot);
        Assert.Contains(fake.Calls, call => call.Method == "ScreenshotArtifactAsync(after-open)");
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
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => session.Browser.GotoAsync("/somewhere"));
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

    private static FakeEngine Fake(Action<string>? onNavigate = null, Action? onDispose = null) =>
        new(new DocumentEngine(BillingWorld.Create("Submit"))) { OnNavigate = onNavigate, OnDispose = onDispose };

    private Task<E2ESession> StartAsync(IEngine engine, string title) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            BaseUrl = "https://billing.test",
            TestTitle = title,
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });

    /// <summary>Runs a test whose <c>app.open</c> fails, and returns its result and the run it stands for.</summary>
    private async Task<(Reports.ReportResult Result, Reports.ReportRun Run)> FailOpeningAsync(FakeEngine engine, string title)
    {
        await using var session = await StartAsync(engine, title);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => session.App.OpenAsync("/settings/billing"));
        session.Complete(error);
        await session.DisposeAsync();
        var run = session.Report()!.Run;
        return (Assert.Single(run.Results), run);
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
