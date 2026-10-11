// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.Verbs;

/// <summary>
/// The grammar verbs beyond tap and type against a real model, each on the playground control built for its
/// deterministic twin. Every step is pinned by a locator check, so a model that reached the goal another way
/// still passes and one that only claimed to does not. Ported from upstream's
/// <c>apps/testbed/tests-agent/verbs.e2e.ts</c>. Not ported: "hover reveals the card menu and the agent archives
/// the card", "a right-click opens the file menu and the agent renames the file", "a long press and a double tap
/// are chosen over a plain tap", "a drag moves the card into the done column" and "upload attaches the named
/// project files". The port's agent has no hover, right-click, long-press, drag or upload tool.
/// </summary>
[Category("RealModel")]
public sealed class VerbsTests : E2ETest
{
    protected override string? BaseUrl => Testbed.Url;

    [Test]
    public async Task Check_sets_the_checkbox_and_the_radio_without_flipping_them_back()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("agree to the terms and pick the Medium size, then confirm both are set");
        await Expect.That(Screen.GetByLabel("Agree to terms")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("radio", "Medium")).ToBeCheckedAsync();
    }

    [Test]
    public async Task Scroll_to_brings_the_footnote_into_view()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("bring the Footnote paragraph at the bottom of the page into view");
        await Expect.That(Screen.GetByLabel("Footnote state")).ToHaveTextAsync("in view");
    }

    [Test]
    public async Task Back_returns_to_the_previous_page()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("open the Docs link, then go back to the controls page");
        await Expect.That(Browser).ToHaveURLAsync("/controls");
    }
}
