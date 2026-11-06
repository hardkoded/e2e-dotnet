// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FrameScreen;

/// <summary>
/// Upstream's <c>describe('frame screen')</c> of <c>packages/web/tests/unit/frame-screen.test.ts</c>: <c>browser.frameLocator</c> returns a
/// frame-scoped screen that keeps the web-only escape hatches, <c>locator</c> and <c>frameLocator</c>. Both compose the same <c>frame</c>
/// expression every query in that scope compiles through.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class FrameScreenTests
{
    [Fact]
    public async Task FrameLocator_nests_outermost_first()
    {
        await using var host = await HostAsync();
        var inner = host.Browser.FrameLocator("#outer").FrameLocator("#inner");

        Assert.Equal(
            new LocatorExpression
            {
                Kind = "frame",
                Selector = "#outer",
                Source = new LocatorExpression
                {
                    Kind = "frame",
                    Selector = "#inner",
                    Source = new LocatorExpression { Kind = "selector", Selector = "#drag1" },
                },
            },
            inner.Locator("#drag1").Query);
        Assert.Equal(
            new LocatorExpression
            {
                Kind = "frame",
                Selector = "#outer",
                Source = new LocatorExpression
                {
                    Kind = "frame",
                    Selector = "#inner",
                    Source = new LocatorExpression { Kind = "role", Role = "button" },
                },
            },
            inner.GetByRole("button").Query);
    }

    [Fact]
    public async Task Rejects_an_empty_selector_at_every_level()
    {
        await using var host = await HostAsync();
        var frame = host.Browser.FrameLocator("#result");

        Assert.Contains("browser.frameLocator() requires a nonempty selector", Assert.Throws<TestException>(() => host.Browser.FrameLocator("")).Message, StringComparison.Ordinal);
        Assert.StartsWith("locator() requires a nonempty selector", Assert.Throws<TestException>(() => frame.Locator("")).Message, StringComparison.Ordinal);
        Assert.StartsWith("frameLocator() requires a nonempty selector", Assert.Throws<TestException>(() => frame.FrameLocator("")).Message, StringComparison.Ordinal);
    }

    private sealed class Host(IEngineSession session, Browser browser) : IAsyncDisposable
    {
        public Browser Browser { get; } = browser;

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }

    private static async Task<Host> HostAsync()
    {
        var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        try
        {
            var screen = new Screen(
                _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
                (_, _, _) => Task.CompletedTask,
                () => CancellationToken.None,
                () => { },
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
            var browser = new Browser(session, "web", null, Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => CancellationToken.None, () => { }, new SoftFailures())
            {
                Context = new EngineFixtureContext(screen),
            };
            return new Host(session, browser);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }
}
