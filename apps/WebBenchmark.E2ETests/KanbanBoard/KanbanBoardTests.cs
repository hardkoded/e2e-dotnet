// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.KanbanBoard;

/// <summary>Ported from <c>describe('kanban board')</c> in upstream's <c>apps/web-benchmark/tests/kanban-board.e2e.ts</c>.</summary>
public sealed class KanbanBoardTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheKanbanBoard() => await App.OpenAsync("/e/kanban-board");

    [Test]
    public async Task Pointer_drags_move_both_cards_into_the_goal_layout()
    {
        var done = Screen.GetByTestId("column-done");
        var inProgress = Screen.GetByTestId("column-in-progress");

        await Screen.GetByTestId("card-fix-payment-bug").DragToAsync(done);
        await Expect.That(done.GetByText("Fix payment bug")).ToBeVisibleAsync();
        await Expect.That(done.GetByText("Empty")).ToBeHiddenAsync();

        await Screen.GetByTestId("card-refactor-onboarding").DragToAsync(inProgress);
        await Expect.That(inProgress.GetByText("Refactor onboarding")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("column-todo").GetByText("Update pricing page")).ToBeVisibleAsync();

        await Screen.GetByRole("button", "Submit board").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Board matches the goal");
    }

    [Test]
    public async Task A_swipe_along_a_path_drags_a_card_as_well()
    {
        await Screen.SwipeAsync(new SwipePathOptions
        {
            From = await Support.CenterOfAsync(Screen.GetByTestId("card-fix-payment-bug")),
            To = await Support.CenterOfAsync(Screen.GetByTestId("column-done")),
        });
        await Expect.That(Screen.GetByTestId("column-done").GetByText("Fix payment bug")).ToBeVisibleAsync();
    }

    [Test]
    public async Task A_plain_tap_moves_nothing_and_the_untouched_board_is_rejected()
    {
        await Screen.GetByTestId("card-fix-payment-bug").TapAsync();
        await Expect.That(Screen.GetByTestId("column-todo").GetByText("Fix payment bug")).ToBeVisibleAsync();
        await Screen.GetByRole("button", "Submit board").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Board does not match the goal yet");
    }
}
