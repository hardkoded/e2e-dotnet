// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.Lifecycle;

// .NET-only: the web hooks that take a context bound it by its timeout (upstream's guard(operation, ...)). Upstream's surface-guard
// tests cover the guard itself, not each hook, so this one proves the port's budget ends a real screenshot and state capture.
[Collection(BrowserCollection.Name)]
public sealed class WebEngineOperationBudgetTests
{
    [Fact]
    public async Task Ends_a_screenshot_and_a_state_capture_at_the_timeout_of_the_context_they_take()
    {
        using var site = await FixtureApp.StartAsync();
        await using var session = await OpenAttemptAsync(new WebEngine(new WebEngineOptions { Headless = true }), site.Url);
        var tight = OperationContext.Test with { Timeout = TimeSpan.FromMilliseconds(1) };

        var shot = await Assert.ThrowsAsync<EngineException>(() => session.ScreenshotAsync(tight, CancellationToken.None));
        Assert.Equal(EngineErrorCodes.OperationTimeout, shot.Code);

        var state = await Assert.ThrowsAsync<EngineException>(() => session.CaptureStateAsync(tight, CancellationToken.None));
        Assert.Equal(EngineErrorCodes.OperationTimeout, state.Code);
    }

    private static async Task<IEngineSession> OpenAttemptAsync(WebEngine engine, string url)
    {
        var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = url, ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);
        await session.OpenAsync(url, CancellationToken.None);
        return session;
    }
}
