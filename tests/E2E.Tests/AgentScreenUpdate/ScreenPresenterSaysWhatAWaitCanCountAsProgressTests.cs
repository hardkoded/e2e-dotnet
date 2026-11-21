// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentScreenUpdate;

/// <summary>The <c>describe('says what a wait can count as progress')</c> inside <c>describe('ScreenPresenter')</c> of <c>agent-screen-update.test.ts</c>.</summary>
public sealed class ScreenPresenterSaysWhatAWaitCanCountAsProgressTests
{
    private static string[] Home => ScreenPresenterTests.Home;

    private static ExecutorObservation Screen(
        string revision,
        IEnumerable<string> lines,
        string? path = null,
        bool truncated = false,
        ExecutorTabs? tabs = null
    ) => ScreenPresenterTests.Screen(revision, lines, path, truncated, tabs: tabs);

    private static ExecutorObservation NoTree(string revision, string? path = null, ExecutorTabs? tabs = null) =>
        ScreenPresenterTests.Screen(
            revision,
            ["[semantic capture unavailable: no nodes were read]"],
            path,
            truncated: true,
            treeUnavailable: true,
            tabs: tabs
        );

    private static readonly ScreenUpdateOptions Look = new() { Lead = "Observed." };

    [Fact]
    public void Names_a_new_location_when_nothing_listed_changed_and_does_not_blame_the_control_for_it()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home, "/products/new"));
        var tapped = presenter
            .Present(
                Screen("b2", Home, "/products/123"),
                new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }
            )
            .Text;
        Assert.Contains("The location changed to /products/123. ", tapped, StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", tapped, StringComparison.Ordinal);

        // A look is what the wait counts: observe's result leads with "Observed." and the line after it.
        var looked = presenter.Present(Screen("b3", Home, "/products/124"), Look).Text;
        Assert.Contains("The location changed to /products/124. ", looked, StringComparison.Ordinal);
        Assert.True(ScreenPresenter.ReportsScreenChange(looked));
        var still = presenter.Present(Screen("b4", Home, "/products/124"), Look).Text;
        Assert.DoesNotContain("The location changed", still, StringComparison.Ordinal);
        Assert.False(ScreenPresenter.ReportsScreenChange(still));
    }

    [Fact]
    public void Counts_tree_recovery_even_when_the_location_contains_the_unavailable_status()
    {
        var presenter = new ScreenPresenter();
        const string location = "Editor\nsemantic capture unavailable";
        presenter.Open(Screen("b1", Home, location));
        Assert.False(ScreenPresenter.ReportsScreenChange(presenter.Present(NoTree("b2", location), Look).Text));
        Assert.False(ScreenPresenter.ReportsScreenChange(presenter.Present(NoTree("b3", location), Look).Text));
        var back = presenter.Present(Screen("b4", Home, location), Look).Text;
        Assert.Contains("Current screen (revision b4", back, StringComparison.Ordinal);
        Assert.True(ScreenPresenter.ReportsScreenChange(back));
    }

    [Fact]
    public void Counts_a_listed_change_even_when_the_location_contains_the_unchanged_status()
    {
        var presenter = new ScreenPresenter();
        const string location = "Editor): no listed node changed. ";
        presenter.Open(Screen("b1", Home, location, truncated: true));
        var still = presenter.Update(Screen("b2", Home, location, truncated: true));
        Assert.False(ScreenPresenter.ReportsScreenChange(still));
        var changed = presenter.Update(Screen("b3", [.. Home, "#n7 button \"Save\""], location, truncated: true));
        Assert.True(ScreenPresenter.ReportsScreenChange(changed));
    }

    [Fact]
    public void Names_a_new_location_while_the_screen_has_no_tree()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home, "/product/edit"));
        Assert.False(ScreenPresenter.ReportsScreenChange(presenter.Present(NoTree("b2", "/product/edit"), Look).Text));
        var moved = presenter.Present(NoTree("b3", "/product/42"), Look).Text;
        Assert.Contains("The location changed to /product/42. ", moved, StringComparison.Ordinal);
        Assert.True(ScreenPresenter.ReportsScreenChange(moved));
    }

    [Fact]
    public void Does_not_take_a_capture_that_leaves_the_location_out_for_navigation_there_or_back()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home, "/edit"));
        var without = presenter.Present(Screen("b2", Home), Look).Text;
        var back = presenter.Present(Screen("b3", Home, "/edit"), Look).Text;
        var treeless = presenter.Present(NoTree("b4"), Look).Text;
        foreach (var text in new[] { without, back, treeless })
        {
            Assert.DoesNotContain("The location changed", text, StringComparison.Ordinal);
            Assert.False(ScreenPresenter.ReportsScreenChange(text));
        }

        // A location reported again after a gap is compared with the last one known.
        Assert.Contains(
            "The location changed to /view. ",
            presenter.Present(Screen("b5", Home, "/view"), Look).Text,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Reports_no_tab_or_location_change_on_the_first_result_of_a_presenter_that_was_never_opened()
    {
        foreach (var active in new[] { 1, 2 })
        {
            var output = new ScreenPresenter()
                .Present(
                    Screen("b1", Home, "/p", tabs: new ExecutorTabs(2, active)),
                    new ScreenUpdateOptions { Lead = "Tapped #n3." }
                )
                .Text;
            Assert.DoesNotContain("The app opened", output, StringComparison.Ordinal);
            Assert.DoesNotContain("The screen shows tab", output, StringComparison.Ordinal);
            Assert.DoesNotContain("The location changed", output, StringComparison.Ordinal);
            Assert.Contains($"tab {active} of 2", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Says_a_tab_opened_even_while_the_screen_has_no_tree()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home));
        presenter.Present(NoTree("b2"), Look);
        var opened = presenter.Present(NoTree("b3", tabs: new ExecutorTabs(2, 1)), Look).Text;
        Assert.Contains("The app opened", opened, StringComparison.Ordinal);
        Assert.True(ScreenPresenter.ReportsScreenChange(opened));
    }
}
