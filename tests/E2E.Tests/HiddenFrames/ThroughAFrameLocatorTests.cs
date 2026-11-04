// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.HiddenFrames;

/// <summary>
/// .NET-only: the user-visible reads upstream's testbed tests (<c>locator-frame.e2e.ts</c>, 'should treat elements inside hidden iframe as
/// hidden' and 'should wait for hidden iframe to become visible') check through a frame locator. The port has no testbed, so they run
/// here, against a page of their own.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughAFrameLocatorTests
{
    private const string Hidden = """
        <!doctype html><title>Hidden</title>
        <iframe name="frame" style="visibility: hidden" srcdoc="<button onclick='window.__clicked = true'>Button</button>"></iframe>
        <iframe name="parent" srcdoc="<button>Parent</button>"></iframe>
        """;

    [Fact]
    public async Task Reads_an_element_inside_a_hidden_iframe_as_hidden()
    {
        await RunAsync(async session =>
        {
            var button = session.Browser.FrameLocator("[name=frame]").Locator("button");
            Assert.False(await button.IsVisibleAsync());
            Assert.True(await button.IsHiddenAsync());
            Assert.Equal(1, await button.CountAsync());
            await Expect.That(button).ToBeHiddenAsync();
            await Expect.That(button).ToBeAttachedAsync();
            await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
            Assert.Equal(0, await session.Browser.FrameLocator("[name=frame]").GetByRole("button").CountAsync());
        });
    }

    [Fact]
    public async Task Waits_for_a_hidden_iframe_to_become_visible()
    {
        await RunAsync(async session =>
        {
            var button = session.Browser.FrameLocator("[name=frame]").Locator("button");
            await Expect.That(button).ToBeHiddenAsync();
            var tap = button.TapAsync();
            await Task.Delay(300);
            Assert.False(tap.IsCompleted);

            await session.Browser.EvaluateAsync<object?>("() => { document.querySelector('[name=frame]').style.visibility = 'visible'; }");

            await tap;
            Assert.True(await session.Browser.EvaluateAsync<bool>("() => document.querySelector('[name=frame]').contentWindow.__clicked === true"));
            await Expect.That(button).ToBeVisibleAsync();
        });
    }

    private static async Task RunAsync(Func<E2ESession, Task> body)
    {
        using var site = await TinySite.StartAsync(Hidden);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "unused")),
            BaseUrl = site.Url,
            TestTitle = "hidden frames",
            ActionTimeout = TimeSpan.FromSeconds(5),
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await session.App.OpenAsync("/");
        await Expect.That(session.Screen.GetByRole("button", "Parent")).ToBeVisibleAsync();
        await body(session);
    }
}
