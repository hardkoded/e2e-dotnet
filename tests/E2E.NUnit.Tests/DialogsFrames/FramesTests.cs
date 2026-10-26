// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.DialogsFrames;

/// <summary>
/// <c>test.describe('frames')</c> of upstream's <c>apps/testbed/tests/dialogs-frames.e2e.ts</c>, against the playground's
/// <c>/frames</c> page and the editor it embeds.
/// </summary>
public sealed class FramesTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Queries_and_actions_scoped_inside_an_iframe()
    {
        await App.OpenAsync("/frames");
        var editor = Browser.FrameLocator("#editor");
        await editor.GetByLabel("Note").FillAsync("hello from outside");
        await editor.GetByRole("button", "Save note").TapAsync();
        await Expect.That(editor.GetByRole("status", "Note state")).ToHaveTextAsync("saved: hello from outside");
    }

    [Test]
    public async Task Frame_queries_do_not_leak_into_the_host_page()
    {
        await App.OpenAsync("/frames");
        await Expect.That(Screen.GetByRole("heading", "Frames")).ToBeVisibleAsync();
        await Expect.That(Browser.FrameLocator("#editor").GetByRole("heading", "Frames")).ToBeHiddenAsync();
    }
}
