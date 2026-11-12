// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.DismissKeyboard;

/// <summary>
/// .NET-only: no upstream <c>it</c> runs the <c>dismiss_keyboard</c> tool through the agent (upstream's tests only list the verb and fake the
/// host). These check the path end to end on an engine that can hide its keyboard: the tool, the action event, the trace, and the replay.
/// </summary>
public sealed class DismissKeyboardTests
{
    private static async Task<(List<string> Tools, string Results, List<ActionEvent> Events, int Dismissed, StepRecord Step)> RunAsync(FileStepCache? cache, bool canDismiss = true)
    {
        var device = new KeyboardDevice(canDismiss);
        var tools = new List<string>();
        var results = new List<string>();
        var model = new ScriptedModel(request =>
        {
            tools.AddRange(request.Tools.Select(tool => tool.Name));
            results.AddRange(request.Messages.Where(message => message.Role == "tool").Select(message => message.Content ?? ""));
            return request.Messages.Count(message => message.Role == "tool") == 0 ? ModelResponses.Call("dismiss_keyboard", new { }) : ModelResponses.Done("passed", "ok");
        });
        var events = new List<ActionEvent>();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = device,
            Model = model,
            BaseUrl = "https://device.test",
            Cache = cache,
            CacheEnabled = cache is not null,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "dismiss the keyboard",
        });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("hide the keyboard", new ActOptions { OnAction = e => { events.Add(e); return Task.CompletedTask; } });
        if (cache is not null)
        {
            await Expect.That(session.Screen.GetByRole("button", "Go")).ToBeVisibleAsync();
        }

        session.Complete();
        return (tools, string.Join("\n", results), events, device.Dismissed, session.StepRecords.Single(step => step.Api == "agent.act"));
    }

    [Fact]
    public async Task Offers_dismiss_keyboard_and_reports_the_action_when_the_engine_can_hide_its_keyboard()
    {
        var run = await RunAsync(null);
        Assert.Contains("dismiss_keyboard", run.Tools);
        Assert.Equal(1, run.Dismissed);
        Assert.Contains("Dismissed the keyboard.", run.Results, StringComparison.Ordinal);
        var action = Assert.Single(run.Events);
        Assert.Equal(ActionName.DismissKeyboard, action.ActionName);
        Assert.Equal("dismiss the keyboard", action.Summary);
    }

    [Fact]
    public async Task Does_not_offer_dismiss_keyboard_when_the_engine_cannot_hide_its_keyboard()
    {
        var device = new KeyboardDevice(false);
        var tools = new List<string>();
        var model = new ScriptedModel(request =>
        {
            tools.AddRange(request.Tools.Select(tool => tool.Name));
            return ModelResponses.Done("passed", "ok");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = device, Model = model, BaseUrl = "https://device.test", CacheEnabled = false, TestTitle = "no dismissal" });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("look");
        session.Complete();
        Assert.DoesNotContain("dismiss_keyboard", tools);
    }

    [Fact]
    public async Task Replays_a_recorded_dismissal_from_the_cache_with_no_model_call()
    {
        var directory = Directory.CreateTempSubdirectory("dismiss-keyboard").FullName;
        try
        {
            var cache = new FileStepCache(directory);
            var recorded = await RunAsync(cache);
            Assert.Equal(1, recorded.Dismissed);

            var replayed = await RunAsync(cache);
            Assert.Equal(1, replayed.Dismissed);
            Assert.Equal(0, replayed.Step.Metrics?.ModelCalls);
            Assert.Equal(ActionName.DismissKeyboard, Assert.Single(replayed.Events).ActionName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A one-button screen with a keyboard node that goes once the engine is asked to dismiss it.</summary>
    private sealed class KeyboardDevice(bool canDismiss) : IEngine
    {
        public int Dismissed { get; private set; }

        public string Platform => "ios";

        public string Version => "1";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Keyboard;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => Task.FromResult<IEngineSession>(new Session(this, canDismiss));

        private sealed class Session(KeyboardDevice device, bool canDismiss) : IEngineSession
        {
            public string Route => "/";

            public bool CanDismissKeyboard => canDismiss;

            public Task DismissKeyboardAsync(CancellationToken cancellationToken)
            {
                device.Dismissed++;
                return Task.CompletedTask;
            }

            public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            {
                var children = new List<SemanticNode> { new() { Ref = "go", Role = "button", Name = "Go", Rect = new BoundingBox(10, 10, 50, 20) } };
                if (device.Dismissed == 0)
                {
                    children.Add(new SemanticNode { Ref = "kb", Role = "keyboard", Name = "Keyboard", Rect = new BoundingBox(0, 500, 400, 300) });
                }

                return Task.FromResult(new Observation
                {
                    Route = "/",
                    Viewport = new TraceViewport(400, 800),
                    Roots = [new SemanticNode { Ref = "root", Rect = new BoundingBox(0, 0, 400, 800), Children = children }],
                });
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task BackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RestartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task ClearStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
