// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Board;

/// <summary>
/// The tests of upstream's <c>apps/testbed/tests/board.e2e.ts</c> (all three outside a <c>describe</c>), against the playground's
/// <c>/board</c> page. All three are ported.
/// </summary>
public sealed class BoardTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Hover_reveals_the_card_menu_action()
    {
        await App.OpenAsync("/board");

        await Screen.GetByText("Card actions", exact: true).HoverAsync();
        await Screen.GetByRole("button", "Archive card").TapAsync();

        await Expect.That(Screen.GetByLabel("Board state")).ToHaveTextAsync("Design review is archived");
    }

    [Test]
    public async Task SetInputFiles_resolves_paths_from_the_project_root()
    {
        await App.OpenAsync("/board");

        await Screen.GetByLabel("Attachment").SetInputFilesAsync("fixtures/attachment.txt");

        await Expect.That(Screen.GetByLabel("Attachment state")).ToHaveTextAsync("attachment.txt");
    }

    [Test]
    public async Task Dragging_the_card_marks_it_done()
    {
        await App.OpenAsync("/board");

        await Screen.GetByText("Design review", exact: true).DragToAsync(Screen.GetByLabel("Done column"));

        await Expect.That(Screen.GetByLabel("Board state")).ToHaveTextAsync("Design review is done");
    }
}
