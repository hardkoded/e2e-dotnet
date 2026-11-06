// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.NUnit.Tests.Todos;

/// <summary>
/// <c>test.describe('todos')</c> of upstream's <c>apps/testbed/tests/todos.e2e.ts</c>, against the playground's
/// <c>/todos</c> page. All seven tests are ported. The class carries the <c>todos</c> category (upstream's <c>tags: ['todos']</c>).
/// </summary>
[Category("todos")]
public sealed class TodosTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // Upstream's beforeEach. NUnit runs the base class's SetUp (which starts the session) before this one.
    [SetUp]
    public async Task OpenTodos()
    {
        await App.OpenAsync("/todos");
    }

    private async Task AddAsync(params string[] titles)
    {
        foreach (var title in titles)
        {
            await Screen.GetByLabel("New todo").FillAsync(title);
            await Screen.GetByLabel("New todo").PressAsync("Enter");
        }
    }

    [Test]
    public async Task Adds_todos_with_the_button_and_the_keyboard()
    {
        await Screen.GetByLabel("New todo").FillAsync("Write spec");
        await Screen.GetByRole("button", "Add").TapAsync();
        await Screen.GetByLabel("New todo").FillAsync("Ship runner");
        await Screen.GetByLabel("New todo").PressAsync("Enter");

        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(2);
        await Expect.That(Screen.GetByTestId("todo").First()).ToContainTextAsync("Write spec");
        await Expect.That(Screen.GetByTestId("todo").Last()).ToContainTextAsync("Ship runner");
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("2 remaining");
    }

    [Test]
    public async Task Ignores_empty_submissions()
    {
        await Screen.GetByRole("button", "Add").TapAsync();
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Completes_and_filters_todos()
    {
        await AddAsync("One", "Two", "Three");
        await Screen.GetByLabel("Two").CheckAsync();
        await Expect.That(Screen.GetByLabel("Two")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("2 remaining");

        var filter = Screen.GetByRole("tablist", "Filter");
        await Expect.That(filter.GetByRole("tab")).ToHaveCountAsync(3);
        await filter.GetByRole("tab", "Open").TapAsync();
        await Expect.That(Screen.GetByRole("tab", "Open")).ToBeSelectedAsync();
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(2);
        await Expect.That(Screen.GetByText("Two")).ToBeHiddenAsync();

        await Screen.GetByRole("tab", "Done").TapAsync();
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByTestId("todo").First()).ToContainTextAsync("Two");

        await Screen.GetByRole("tab", "All").TapAsync();
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Reads_and_checks_every_todo_at_once()
    {
        await AddAsync("Alpha", "Beta", "Gamma");
        await Expect.That(Screen.GetByTestId("todo")).ToContainTextAsync(["Alpha", "Beta", "Gamma"]);
        await Expect.That(Screen.GetByRole("button", new Regex("^Delete"))).ToHaveTextAsync(
            ["Delete Alpha", new Regex("Beta$"), "Delete Gamma"]);
        Assert.That(
            await Screen.GetByRole("button", new Regex("^Delete")).AllTextContentsAsync(),
            Is.EqualTo(new[] { "Delete Alpha", "Delete Beta", "Delete Gamma" }));

        foreach (var todo in await Screen.GetByTestId("todo").AllAsync())
        {
            await todo.GetByRole("checkbox").CheckAsync();
        }

        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("0 remaining");
        Assert.That(await Screen.GetByRole("button", "Add").IsDisabledAsync(), Is.False);

        await Screen.GetByRole("tab", "Open").TapAsync();
        await Expect.That(Screen.GetByTestId("todo")).Not.ToBeAttachedAsync();
        Assert.That(await Screen.GetByTestId("todo").IsHiddenAsync(), Is.True);
        Assert.That(await Screen.GetByTestId("todo").AllAsync(), Is.Empty);
    }

    [Test]
    public async Task Deletes_todos()
    {
        await AddAsync("Disposable");
        await Screen.GetByRole("button", "Delete Disposable").TapAsync();
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(0);
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("0 remaining");
    }

    [Test]
    public async Task Persists_across_restart_and_clears_with_clearState()
    {
        await AddAsync("Survive restart");
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(1);

        await App.RestartAsync();
        await App.OpenAsync("/todos");
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByTestId("todo").First()).ToContainTextAsync("Survive restart");

        await App.ClearStateAsync();
        await App.OpenAsync("/todos");
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Attempts_start_from_a_logically_fresh_session()
    {
        // Storage written by earlier tests in this file must not leak in.
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(0);
    }
}
