// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;

namespace E2E.Tests.ChromiumSandboxSurface;

/// <summary>The engine hands its launch the <c>ChromiumSandbox</c> option, or the platform default without one.</summary>
public sealed class ChromiumSandboxSurfaceTests
{
    [Fact]
    public async Task Launches_with_the_platform_default_when_the_option_is_omitted()
    {
        Assert.Equal(BrowserConnection.DefaultChromiumSandbox(), await LaunchedSandboxAsync(new WebEngineOptions()));
    }

    [Fact]
    public async Task Launches_with_what_the_option_says()
    {
        Assert.False(await LaunchedSandboxAsync(new WebEngineOptions { ChromiumSandbox = false }));
        Assert.True(await LaunchedSandboxAsync(new WebEngineOptions { ChromiumSandbox = true }));
    }

    private static async Task<bool> LaunchedSandboxAsync(WebEngineOptions options)
    {
        BrowserAcquireOptions? launched = null;
        var engine = new WebEngine(options)
        {
            CreatePlaywright = FakeRemote.Playwright,
            EnsureBrowser = (_, _, _) => Task.CompletedTask,
            LaunchLocal = (_, acquire) =>
            {
                launched = acquire;
                throw new InvalidOperationException("stop after the launch options are known");
            },
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.StartAsync(new EngineStartOptions(), CancellationToken.None)
        );
        return Assert.IsType<BrowserAcquireOptions>(launched).ChromiumSandbox;
    }
}
