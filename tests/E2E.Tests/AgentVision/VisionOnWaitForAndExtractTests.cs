// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;

namespace E2E.Tests.AgentVision;

/// <summary>
/// <c>Vision</c> on <c>WaitForAsync</c> and <c>ExtractAsync</c>, which upstream's <c>agent-vision.test.ts</c> does not
/// run (it asks for pixels on <c>assert</c> alone). The first test is a partial of "is shaped by the pixels when a
/// capture carries them, so a canvas that redrew counts as changed" (<c>observationShape</c>, <c>agent-observation.test.ts</c>):
/// the port compares the looks of a <c>WaitForAsync</c> by the screen text, so the test reads the effect on its judgments.
/// The others are .NET-only: no upstream test covers pixels on <c>waitFor</c> or <c>extract</c>.
/// </summary>
public sealed class VisionOnWaitForAndExtractTests
{
    [Fact]
    public async Task Judges_a_vision_waitFor_again_when_only_the_pixels_changed()
    {
        // The tree never changes, as a canvas that redraws. The same waitFor without vision judges once (see AgentJudgmentTierTests).
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the animation has not finished"));
        var engine = new ScreenshotEngine(World(), shade => ScreenshotEngine.Png((byte)shade));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.WaitForAsync(
                    "the animation has finished",
                    new WaitForOptions { Interval = TimeSpan.FromMilliseconds(100), Timeout = TimeSpan.FromSeconds(2), MaxModelCalls = 4, Vision = VisionMode.On });
            },
            engine,
            judge);

        Assert.Equal("STEP_BUDGET_EXHAUSTED", Assert.IsType<AgentException>(result.Error).Code);
        Assert.Equal(4, judge.CallCount);
        Assert.All(judge.Requests, request => Assert.Single(request.Messages[0].Images!));
    }

    [Fact]
    public async Task Sends_the_screenshot_beside_the_tree_to_an_extraction_that_asks_for_vision()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Call("extract", new { data = new { heading = "About" } }));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                Assert.Equal("About", (await ctx.Agent.ExtractAsync<Reading>("the heading", new ExtractOptions { Vision = VisionMode.On })).Heading);
            },
            new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            judge);

        Assert.Null(result.Error);
        var message = judge.Requests[0].Messages[0];
        Assert.Single(message.Images!);
        Assert.Contains("heading \"About\"", message.Content, StringComparison.Ordinal);
        Assert.Contains("A screenshot of the current screen is attached, 4x2 pixels.", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_an_extraction_that_asks_for_pixels_only_the_screenshot_alone()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Call("extract", new { data = new { heading = "About" } }));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.ExtractAsync<Reading>("the heading", new ExtractOptions { Vision = VisionMode.Only });
            },
            new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            judge);

        Assert.Null(result.Error);
        var message = judge.Requests[0].Messages[0];
        Assert.Single(message.Images!);
        Assert.DoesNotContain("heading \"About\"", message.Content, StringComparison.Ordinal);
        Assert.Contains("no accessibility tree is attached, on purpose", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_a_waitFor_that_asks_for_pixels_only_when_the_engine_captures_none()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "it does"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.WaitForAsync("the heading is About", new WaitForOptions { Vision = VisionMode.Only });
            },
            new ScreenshotEngine(World()),
            judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("UNSUPPORTED_CAPABILITY", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, judge.CallCount);
    }

    [Fact]
    public async Task Degrades_to_the_tree_when_the_engine_masked_fewer_regions_than_the_tree_has_secure_nodes()
    {
        var world = World().Map("/login", page => page.Textbox("Password", "", secure: true));
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "it does"));
        var engine = new MaskingNothingEngine(world);
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/login");
                await ctx.Agent.AssertAsync("a password field is shown", new AssertOptions { Vision = VisionMode.On });
                var denied = await Assert.ThrowsAsync<AgentException>(() => ctx.Agent.AssertAsync("a password field is shown", new AssertOptions { Vision = VisionMode.Only }));
                Assert.Equal("POLICY_DENIED", denied.Code);
                Assert.Contains("MASKING_UNPROVEN", denied.Message, StringComparison.Ordinal);
            },
            engine,
            judge);

        Assert.Null(result.Error);
        Assert.Equal(1, judge.CallCount);
        Assert.Null(judge.Requests[0].Messages[0].Images);
    }

    [Fact]
    public async Task Refuses_a_vision_mode_that_is_not_one_of_the_three()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/about");
                await ctx.Agent.AssertAsync("the heading is About", new AssertOptions { Vision = (VisionMode)7 });
            },
            new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            new ScriptedModel(_ => ModelResponses.Done("passed", "it does")));

        Assert.Equal("INVALID_ARGUMENT", Assert.IsType<TestException>(result.Error).Code);
    }

    private static DocumentWorld World() => CounterPage.World().Map("/about", page => page.Heading("About"));

    private static async Task<CoreTests.Attempt> RunAsync(Func<TestContext, Task> body, IEngine engine, ScriptedModel judge)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "vision on waitFor and extract",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
            ResultsDirectory = Path.Combine(Path.GetTempPath(), "e2e-vision-" + Guid.NewGuid().ToString("N")),
        });
        Exception? error = null;
        try
        {
            await body(session.Context);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return new CoreTests.Attempt(error, session.ModelCalls, session.Replayed, session.Missed);
    }

    private sealed class Reading
    {
        public required string Heading { get; init; }
    }

    /// <summary>An engine whose screenshots cover no region, whatever the tree lists.</summary>
    private sealed class MaskingNothingEngine(DocumentWorld world) : IEngine
    {
        private readonly DocumentEngine _inner = new(world);

        public string Platform => _inner.Platform;

        public string Version => _inner.Version;

        public EngineCapabilities Capabilities => _inner.Capabilities | EngineCapabilities.Screenshot;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(IEngineSession inner) : IEngineSession
        {
            public string Route => inner.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => inner.ObserveAsync(cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new EngineScreenshot(ScreenshotEngine.Png(), 1, 0));

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
