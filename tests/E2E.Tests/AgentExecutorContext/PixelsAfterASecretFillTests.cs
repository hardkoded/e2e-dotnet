// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>
/// .NET-only: upstream proves the pixels an executor's context keeps after a secret fill through its restored-session and serial
/// groups, which the port does not have, and has no test of <c>pixelsWithheld</c> on an executor's context.
/// </summary>
public sealed class PixelsAfterASecretFillTests
{
    [Fact]
    public async Task Keeps_the_pixels_once_the_executor_filled_a_secret()
    {
        var before = new List<(VisionDegradation? Withheld, bool Pixels)>();
        var executor = new TestExecutor("filler", async (context, _) =>
        {
            var screen = await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true });
            before.Add((screen.PixelsWithheld, screen.Pixels is not null));
            await context.Actions.TypeSecretAsync(TargetFor(screen.Text, "textbox \"Password\""), "admin.password");
            var after = await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true });
            before.Add((after.PixelsWithheld, after.Pixels is not null));
            return Passed("filled the password");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ScreenshotEngine(WorldWithAPassword(_ => { }), _ => ScreenshotEngine.Png()),
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            TestTitle = "filled",
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync(
            "sign in",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass", "password") } });
        session.Complete();

        Assert.Equal([(null, true), (null, true)], before);
    }

    [Fact]
    public async Task Says_the_engine_captures_none_when_it_declares_no_screenshot()
    {
        VisionDegradation? withheld = null;
        var executor = new TestExecutor("looker", async (context, _) =>
        {
            withheld = (await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true })).PixelsWithheld;
            return Passed("looked");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new E2E.Engine.DocumentEngine(CounterPage.World()),
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            TestTitle = "no pixels",
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync("look at the screen");
        session.Complete();

        Assert.Equal(VisionDegradation.UnsupportedCapability, withheld);
    }
}
