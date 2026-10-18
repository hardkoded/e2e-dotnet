// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Traces;

/// <summary>Upstream's <c>describe('traces')</c> (<c>tests/integration/traces.test.ts</c>), driven through a session over an engine that reports its screens.</summary>
public sealed class TracesTests
{
    // Partial: upstream also reads the screens and the environment from the run report and from the trace page. The port has neither yet.
    [Fact]
    public async Task Keeps_the_screen_each_step_left_and_what_the_engine_said_the_attempt_ran_on()
    {
        var engine = new ReportingEngine();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, TestTitle = "traces > saves", BaseUrl = "http://127.0.0.1:4100", ActionTimeout = TimeSpan.FromMilliseconds(300) });

        await session.App.OpenAsync("/");
        await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();
        await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();

        Assert.Equal(new Dictionary<string, string> { ["device"] = "Pixel 9", ["os"] = "Android 16" }, session.Environment);
        var steps = session.Steps;
        var first = steps[1].Screen!;
        var second = steps[2].Screen!;
        Assert.Equal(2, first.Nodes);
        Assert.Empty(first.Changes);
        Assert.Equal(1, second.Since);
        Assert.Equal(["added button \"Undo\""], second.Changes);
    }

    // Partial: the report and the trace page, where upstream reads "no screens", have no counterpart yet; the steps carry none.
    [Fact]
    public async Task Asks_an_attempt_that_keeps_no_trace_for_no_screens_and_takes_none_from_what_the_test_observes()
    {
        var engine = new ReportingEngine();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "traces > saves, untraced",
            BaseUrl = "http://127.0.0.1:4100",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            Trace = new ResolvedRecording(RecordingMode.Off, RecordingSource.Target),
        });

        await session.App.OpenAsync("/");
        await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();

        Assert.False(engine.AskedForScreens);
        Assert.NotEmpty(session.Steps);
        Assert.All(session.Steps, step => Assert.Null(step.Screen));
    }

    private sealed class ReportingEngine : IEngine
    {
        public bool AskedForScreens { get; private set; }

        public string Platform => "document";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            AskedForScreens = options.Screen is not null;
            options.Environment?.Invoke(new Dictionary<string, string> { ["device"] = "Pixel 9", ["os"] = "Android 16" });
            return Task.FromResult<IEngineSession>(new Session(options));
        }
    }

    private sealed class Session(EngineStartOptions options) : IEngineSession
    {
        private int _taps;

        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(Screen(_taps >= 2 ? ["Submit", "Undo"] : ["Submit"], "observe"));

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            _taps++;
            options.Screen?.Invoke(Screen(_taps == 1 ? ["Submit"] : ["Submit", "Undo"], $"t{_taps}"));
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Observation Screen(string[] names, string mint) => new()
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "root", Role = "document", Children = [.. names.Select((name, index) => new SemanticNode { Ref = $"{(name == "Submit" ? "submit" : mint + "-" + index)}", Role = "button", Name = name })] }],
        };
    }
}
