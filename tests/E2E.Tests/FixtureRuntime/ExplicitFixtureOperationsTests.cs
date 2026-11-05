// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>describe('explicit fixture operations')</c> in <c>fixture-runtime.test.ts</c>. Its other <c>it</c>s declare a contributed
/// fixture through <c>context.fixture(...)</c>, which the port does not have: its fixtures record their steps through
/// <see cref="EngineFixtureContext"/>, so they are not ported.
/// </summary>
public sealed class ExplicitFixtureOperationsTests
{
    // Partial: the port's fixture calls RunOperationAsync itself (no declared operations), and the engine error comes from the call's body.
    [Fact]
    public async Task Partial_Maps_an_EngineError_a_contributed_method_throws_onto_the_runner_taxonomy_and_leaves_other_errors_as_thrown()
    {
        var engine = new GadgetEngine();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, BaseUrl = "https://gadget.test", TestTitle = "gadget", CacheEnabled = false });
        var context = engine.Context!;

        var lost = await Assert.ThrowsAsync<TestException>(() => Run(context, "gadget.lost", "", new EngineException("INVALID_STATE", "appstate requires an active session")));
        Assert.Equal(("APP_NOT_OPEN", "appstate requires an active session"), (lost.Code, lost.Message));

        var slow = await Assert.ThrowsAsync<TestException>(() => Run(context, "gadget.slow", "", new EngineException("OPERATION_TIMEOUT", "appstate timed out")));
        Assert.Equal(("ACTION_FAILED", "operation timed out in gadget.slow"), (slow.Code, slow.Message));

        var wait = await Assert.ThrowsAsync<TestException>(() => Run(context, "gadget.wait", JsonSerializer.Serialize("foo.css"), new EngineException("OPERATION_TIMEOUT", "wait timed out")));
        Assert.Equal(("ACTION_FAILED", "operation timed out in gadget.wait \"foo.css\""), (wait.Code, wait.Message));

        var denied = await Assert.ThrowsAsync<TestException>(() => Run(context, "gadget.denied", "", new TestException("POLICY_DENIED", "no")));
        Assert.Equal("POLICY_DENIED", denied.Code);

        var plain = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(context, "gadget.plain", "", new InvalidOperationException("plain failure")));
        Assert.Equal("plain failure", plain.Message);
    }

    // .NET-only: upstream's `app` fixture goes through the same recorder as a contributed fixture; the port's App records its steps itself.
    [Fact]
    public async Task App_steps_map_the_engine_error_the_session_throws_with_the_operation_in_the_message()
    {
        var engine = new GadgetEngine { OpenFailure = new EngineException("OPERATION_TIMEOUT", "open timed out") };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, BaseUrl = "https://gadget.test", TestTitle = "gadget", CacheEnabled = false });
        var timeout = await Assert.ThrowsAsync<TestException>(() => session.App.OpenAsync("/orders"));
        Assert.Equal(("ACTION_FAILED", "operation timed out in app.open /orders"), (timeout.Code, timeout.Message));

        engine.OpenFailure = new EngineException("INVALID_STATE", "no open app");
        var closed = await Assert.ThrowsAsync<TestException>(() => session.App.OpenAsync());
        Assert.Equal("APP_NOT_OPEN", closed.Code);

        engine.OpenFailure = new EngineException("CANCELLED", "cancelled by the caller");
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.App.OpenAsync());
        Assert.Equal(("CANCELLED", "operation cancelled"), (cancelled.Code, cancelled.Message));
    }

    // .NET-only: Browser.OnDialogAsync and the download waits ask for the browser session inside their step, so a missing one is the
    // configuration error GotoAsync reports, not a raw EngineException.
    [Fact]
    public async Task Browser_calls_that_need_a_browser_engine_report_a_configuration_error_inside_their_step()
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = new GadgetEngine(), BaseUrl = "https://gadget.test", TestTitle = "gadget", CacheEnabled = false });
        var dialog = await Assert.ThrowsAsync<ConfigurationException>(() => session.Browser.OnDialogAsync(DialogHandler.Accept));
        var download = await Assert.ThrowsAsync<ConfigurationException>(() => session.Browser.WaitForDownloadAsync(() => Task.CompletedTask));
        var downloads = await Assert.ThrowsAsync<ConfigurationException>(() => session.Browser.WaitForDownloadsAsync(() => Task.CompletedTask, 1));
        Assert.All([dialog, download, downloads], error => Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code));
        Assert.Equal(["browser.onDialog", "browser.waitForDownload", "browser.waitForDownloads"], session.StepRecords.Select(step => step.Api));
    }

    private static Task Run(EngineFixtureContext context, string api, string label, Exception failure) =>
        context.RunOperationAsync<bool>(StepKind.Resource, api, label, () => Task.FromException<bool>(failure));

    private sealed class GadgetEngine : IEngine
    {
        public EngineFixtureContext? Context { get; set; }

        public Exception? OpenFailure { get; set; }

        public string Platform => "fake";

        public string Version => "1";

        public EngineCapabilities Capabilities => EngineCapabilities.None;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<IEngineSession>(new Session(this));

        private sealed class Session(GadgetEngine engine) : IEngineSession, IEngineFixtureHost
        {
            public string Route => "/";

            public Task OpenAsync(string url, CancellationToken cancellationToken) =>
                engine.OpenFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new Observation { Route = "/", Roots = [] });

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

            public void CreateFixtures(EngineFixtureContext context) => engine.Context = context;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
