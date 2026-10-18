// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepScreens.ScreenKit;
using Screens = global::E2E.StepScreens;

namespace E2E.Tests.StepScreens;

/// <summary>Upstream's <c>describe('compareScreens')</c> (<c>step-screens.test.ts</c>).</summary>
public sealed class CompareScreensTests
{
    [Fact]
    public void Tells_a_first_screen_by_its_size_and_where_it_was()
    {
        var first = Screens.CompareScreens(null, Screen("#root document \"Todos\"", " #n2 button \"Add\""));

        Assert.Equal("http://app.test/todos", first.Location);
        Assert.Equal(2, first.Nodes);
        Assert.Empty(first.Changes);
        Assert.Null(first.Since);
        Assert.Null(first.More);
    }

    [Fact]
    public void Pairs_a_node_by_id_as_changed_and_by_text_where_ids_are_new()
    {
        var before = Screen("#root document", " #n2 textbox \"New todo\"", " #n3 button \"Add\"", " #n4 text=\"0 left\"");
        var after = Screen("#root document", " #n2 textbox \"New todo\" value=\"Milk\"", " #n9 button \"Add\"", " #n10 listitem \"Milk\"");

        var compared = Screens.CompareScreens((1, before), after);

        Assert.Equal(1, compared.Since);
        Assert.Equal(4, compared.Nodes);
        Assert.Equal(["changed textbox \"New todo\" value=\"Milk\" (was: textbox \"New todo\")", "added listitem \"Milk\"", "removed text=\"0 left\""], compared.Changes);
    }

    [Fact]
    public void Reads_an_unchanged_screen_as_no_changes_and_counts_the_changes_past_its_bound()
    {
        string[] lines = ["#root document", .. Enumerable.Range(0, 20).Select(index => $" #a{index} button \"Item {index}\"")];

        Assert.Empty(Screens.CompareScreens((0, Screen(lines)), Screen(lines)).Changes);

        var grown = Screens.CompareScreens((0, Screen("#root document")), Screen(lines));
        Assert.Equal(12, grown.Changes.Count);
        Assert.Equal(8, grown.More);
    }
}
