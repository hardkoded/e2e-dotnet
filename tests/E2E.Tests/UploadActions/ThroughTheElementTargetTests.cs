// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;
using static E2E.Tests.UploadActions.UploadPage;

namespace E2E.Tests.UploadActions;

/// <summary>
/// <c>setInputFiles</c> against a real page, ported from upstream <c>upload-actions.test.ts</c> (<c>describe.each</c> over the
/// <c>element</c> target), on the element an observation names, which the agent acts through. A file input and a label for one
/// take the files without a click, a button that opens a hidden input's chooser is clicked and the chooser takes them, and a
/// button that opens nothing fails. Upstream calls <c>dispatchLocatorAction</c> and <c>classifyActionError</c>; the port's
/// engine session carries both in <c>PerformAsync</c>. The file input carries an <c>aria-label</c>, so the observation names it, and the
/// label holds a <c>span</c> with a test id, since the observation lists no node for a label, and folds its text into the hidden input's name.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughTheElementTargetTests
{
    [Fact]
    public async Task Sets_a_file_input_directly()
    {
        await RunAsync(
            $"""<input id="avatar" type="file" aria-label="Avatar">{CountClicks}""",
            async session =>
            {
                await session.PerformAsync(await FindByNameAsync(session, "Avatar"), new LocatorAction.SetInputFiles([AvatarFile]), CancellationToken.None);
                Assert.Equal("avatar.png|0", await EvaluateAsync<string>(session, State));
            });
    }

    [Fact]
    public async Task Sets_the_input_a_label_names_without_clicking_the_label()
    {
        await RunAsync(
            $"""<label id="pick" for="avatar"><span data-testid="pick">Choose avatar</span></label><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                await session.PerformAsync(await FindByTestIdAsync(session, "pick"), new LocatorAction.SetInputFiles([AvatarFile]), CancellationToken.None);
                Assert.Equal("avatar.png|0", await EvaluateAsync<string>(session, State));
            });
    }

    [Fact]
    public async Task Clicks_a_button_over_a_hidden_input_and_sets_the_files_through_its_chooser()
    {
        await RunAsync(
            $"""
            <button id="pick" type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button>
            <input id="avatar" type="file" hidden>{CountClicks}
            """,
            async session =>
            {
                await session.PerformAsync(await FindByNameAsync(session, "Choose avatar"), new LocatorAction.SetInputFiles([AvatarFile]), CancellationToken.None);
                Assert.Equal("avatar.png", await FilesAsync(session));
            });
    }

    [Fact]
    public async Task Fails_when_the_button_opens_no_file_chooser()
    {
        await RunAsync(
            $"""<button id="pick" type="button">Choose avatar</button><input id="avatar" type="file" hidden>{CountClicks}""",
            async session =>
            {
                var pick = await FindByNameAsync(session, "Choose avatar");
                var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(pick, new LocatorAction.SetInputFiles([AvatarFile]), CancellationToken.None));
                Assert.Equal("NOT_ACTIONABLE", error.Code);
                Assert.Contains("opened no file chooser", error.Message, StringComparison.Ordinal);
                Assert.Equal("", await FilesAsync(session));
            });
    }

    /// <summary>The names of the files the input holds.</summary>
    private static async Task<string> FilesAsync(IEngineSession session) => (await EvaluateAsync<string>(session, State)).Split('|')[0];

    /// <summary>The element inside a label: the observation lists it as a node of its own, where the label is not one.</summary>
    private static async Task<SemanticNode> FindByTestIdAsync(IEngineSession session, string testId)
    {
        var observation = await session.ObserveAsync(CancellationToken.None);
        return Flatten(observation.Roots).First(node => node.TestId == testId);
    }

    private static async Task<SemanticNode> FindByNameAsync(IEngineSession session, string name)
    {
        var observation = await session.ObserveAsync(CancellationToken.None);
        return Flatten(observation.Roots).First(node => node.Name == name);
    }
}
