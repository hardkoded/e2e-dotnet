// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.FrameHierarchy;

/// <summary>
/// <c>test.describe('frame hierarchy')</c> of upstream's <c>apps/testbed/tests/frame-hierarchy.e2e.ts</c> (Playwright's
/// <c>frame-hierarchy.spec.ts</c>). A frame the original finds in <c>page.frames()</c> is reached through <c>FrameLocator</c>. Not
/// ported, as upstream: should send events when frames are manipulated dynamically, should persist mainFrame on cross-process
/// navigation, should not send attach/detach events for main frame, should detach child frames on navigation, should support
/// framesets, should report frame.name(), should report frame.parent(), should report different frame instance when frame
/// re-attaches, should refuse to display x-frame-options:deny iframe, should return frame.page() (frame events, Frame objects,
/// console events).
/// </summary>
public sealed class FrameHierarchyTests : PlaywrightTest
{
    [Test]
    public async Task Should_handle_nested_frames()
    {
        await Browser.GotoAsync("/frames/nested-frames.html");
        await Expect.That(Browser.FrameLocator("[name=aframe]").Locator("div")).ToHaveTextAsync("Hi, I'm frame");
        await Expect.That(Browser.FrameLocator("[name=\"2frames\"]").FrameLocator("[name=dos]").Locator("div")).ToHaveTextAsync("Hi, I'm frame");
        await Expect.That(Browser.FrameLocator("[name=\"2frames\"]").FrameLocator("[name=uno]").Locator("div")).ToHaveTextAsync("Hi, I'm frame");
        await Expect.That(Browser.FrameLocator("[name=\"2frames\"]").Locator("iframe")).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("iframe")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Should_send_framenavigated_when_navigating_on_anchor_URLs()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync("/empty.html#foo");
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/empty.html#foo"));
    }

    [Test]
    public async Task Should_report_frame_from_inside_shadow_DOM()
    {
        await Browser.GotoAsync("/shadow.html");
        await Browser.EvaluateAsync<object>("""
            async (url) => {
              const frame = document.createElement('iframe');
              frame.src = url;
              document.body.shadowRoot.appendChild(frame);
              await new Promise((x) => (frame.onload = x));
            }
            """, $"{PREFIX}/empty.html");
        await Expect.That(Browser.Locator("iframe")).ToHaveCountAsync(1);
        await Expect.That(Browser.FrameLocator("iframe").Locator("body")).ToHaveCountAsync(1);
    }
}
