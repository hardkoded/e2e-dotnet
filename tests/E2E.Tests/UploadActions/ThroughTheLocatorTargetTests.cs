// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.UploadActions.UploadPage;

namespace E2E.Tests.UploadActions;

/// <summary>
/// <c>setInputFiles</c> through a screen locator, ported from upstream <c>upload-actions.test.ts</c> (<c>describe.each</c> over
/// the <c>locator</c> target). Upstream's locator is a CSS selector; this port finds the control by label, test id, or role; the label's own text is a span with a test id, since the observation lists no node for a label.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughTheLocatorTargetTests
{
    [Fact]
    public async Task Sets_a_file_input_directly()
    {
        await RunWithScreenAsync(
            $"""<input id="avatar" type="file" aria-label="Avatar">{CountClicks}""",
            async session =>
            {
                await session.Screen.GetByLabel("Avatar").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png|0", await session.Browser.EvaluateAsync<string>(State));
            });
    }

    [Fact]
    public async Task Sets_the_input_a_label_names_without_clicking_the_label()
    {
        await RunWithScreenAsync(
            $"""<label id="pick" for="avatar"><span data-testid="pick">Choose avatar</span></label><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                await session.Screen.GetByTestId("pick").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png|0", await session.Browser.EvaluateAsync<string>(State));
            });
    }

    [Fact]
    public async Task Clicks_a_button_over_a_hidden_input_and_sets_the_files_through_its_chooser()
    {
        await RunWithScreenAsync(
            $"""
            <button id="pick" type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button>
            <input id="avatar" type="file" hidden>{CountClicks}
            """,
            async session =>
            {
                await session.Screen.GetByRole("button", "Choose avatar").SetInputFilesAsync(AvatarFile);
                Assert.Equal("avatar.png", (await session.Browser.EvaluateAsync<string>(State))!.Split('|')[0]);
            });
    }

    [Fact]
    public async Task Fails_when_the_button_opens_no_file_chooser()
    {
        await RunWithScreenAsync(
            $"""<button id="pick" type="button">Choose avatar</button><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                var error = await Assert.ThrowsAsync<EngineException>(() => session.Screen.GetByRole("button", "Choose avatar").SetInputFilesAsync(AvatarFile));
                Assert.Equal("NOT_ACTIONABLE", error.Code);
                Assert.Contains("opened no file chooser", error.Message, StringComparison.Ordinal);
                Assert.Equal("", (await session.Browser.EvaluateAsync<string>(State))!.Split('|')[0]);
            });
    }
}
