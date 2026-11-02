// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.AgentScreenUpdate;

/// <summary>
/// <c>describe('ScreenPresenter')</c> of <c>agent-screen-update.test.ts</c>. The port has no screenshots on <c>act</c>, so the presenter takes
/// the tree alone. Not ported: 'keeps the listing as the only evidence, and says to try another way, when the engine captures no screenshot',
/// 'says the screenshot changed, not that the control had no effect, when the listing stood still under a moved image', and 'treats unproven
/// masking as one screen's refusal, not the step's'. Each checks how the presenter compares screenshots (<c>open</c>, <c>present</c>,
/// <c>pixelsWithheld</c>), which the port does not have.
/// </summary>
public sealed class ScreenPresenterTests
{
    private static readonly string[] Home =
    [
        "#n1 document \"Home\"",
        " #n2 heading \"Welcome\"",
        " #n3 button \"Increment\"",
        " #n4 status \"Counter\" text=\"0\"",
        " #n5 link \"About\" href=\"/about\"",
        " #n6 textbox \"Email\"",
    ];

    internal static ExecutorObservation Screen(string revision, IEnumerable<string> lines, string? path = null, bool truncated = false, bool treeUnavailable = false) =>
        new(string.Join('\n', lines)) { Revision = revision, Path = path, Truncated = truncated, TreeUnavailable = treeUnavailable };

    [Fact]
    public void Partial_Retires_semantic_history_through_repeated_unavailable_captures_and_shows_recovery_whole()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", ["#root", " #save button \"Save\""]));
        foreach (var revision in new[] { "b2", "b3" })
        {
            var text = presenter.Update(Screen(
                revision,
                ["[semantic capture unavailable: no nodes were read; previous node ids are no longer valid. Use the screenshot, never infer absence from this listing]"],
                truncated: true,
                treeUnavailable: true));
            Assert.Contains("semantic capture unavailable", text, StringComparison.Ordinal);
            Assert.Contains("previous node ids are no longer valid", text, StringComparison.Ordinal);
            Assert.DoesNotContain("keeps the id", text, StringComparison.Ordinal);
            Assert.DoesNotContain("no listed node changed", text, StringComparison.Ordinal);
            Assert.DoesNotContain("removed", text, StringComparison.Ordinal);
        }

        var recovered = presenter.Update(Screen("b4", ["#root", " #save button \"Save\""]));
        Assert.Contains("Current screen (revision b4", recovered, StringComparison.Ordinal);
        Assert.Contains("#save button \"Save\"", recovered, StringComparison.Ordinal);
        Assert.DoesNotContain("Screen changes", recovered, StringComparison.Ordinal);
    }

    [Fact]
    public void Sends_the_first_screen_whole_with_its_revision_path_and_size()
    {
        var presenter = new ScreenPresenter();
        var text = presenter.Initial(Screen("b1", Home, "/"));
        Assert.Equal("Current screen (revision b1, path /, 6 nodes):", text.Split('\n')[0]);
        Assert.Contains(" #n3 button \"Increment\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_a_changed_and_an_added_line_in_document_order_keyed_by_stable_ids()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var next = Home.ToList();
        next[3] = " #n4 status \"Counter\" text=\"1\"";
        next.Add(" #n7 button \"Late arrival\"");
        var text = presenter.Update(Screen("b2", next), new ScreenUpdateOptions { Lead = "Tapped #n3." });
        Assert.Equal(
            [
                "Tapped #n3.",
                "",
                "Screen changes since revision b1 (now revision b2, 7 nodes): 1 added, 1 changed. Every node not listed as removed is still on screen under the id you have.",
                "changed #n4 status \"Counter\" text=\"1\" (was: #n4 status \"Counter\" text=\"0\")",
                "added #n7 button \"Late arrival\"",
            ],
            text.Split('\n'));
    }

    [Fact]
    public void Lists_removed_lines_after_the_additions()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var text = presenter.Update(Screen("b2", Home.Where(line => !line.Contains("#n5", StringComparison.Ordinal))));
        Assert.Contains("1 removed.", text, StringComparison.Ordinal);
        Assert.Contains("removed #n5 link \"About\" href=\"/about\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("added ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_so_when_nothing_changed_and_blames_the_action_when_one_was_expected_to_change_something()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        Assert.Equal(
            "Screen unchanged since revision b1 (re-observed as revision b2); the ids you have stay valid.",
            presenter.Update(Screen("b2", Home)));
        var afterAction = presenter.Update(Screen("b3", Home), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true });
        Assert.Contains("Tapped #n3.", afterAction, StringComparison.Ordinal);
        Assert.Contains("re-observed as revision b3", afterAction, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_Says_no_listed_node_changed_after_an_action_never_that_it_had_no_effect_when_no_screenshot_was_compared()
    {
        // The first look at a screenshot has none before it to compare, which the port cannot show: only the tree-only half is checked.
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var treeOnly = presenter.Update(Screen("b2", Home), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true });
        Assert.Contains("No listed node changed within the wait after this action", treeOnly, StringComparison.Ordinal);
        Assert.Contains("a drawn change needs a screenshot to see", treeOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", treeOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("look for another way", treeOnly, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_focus_moving_which_every_action_does_when_deciding_what_changed()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var focused = Home.ToList();
        focused[2] = " #n3 button \"Increment\" [focused]";
        Assert.Contains(
            "No listed node changed within the wait",
            presenter.Update(Screen("b2", focused), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }),
            StringComparison.Ordinal);
        var typed = focused.ToList();
        typed[5] = " #n6 textbox \"Email\" value=\"a\" [focused]";
        typed[2] = " #n3 button \"Increment\"";
        var text = presenter.Update(Screen("b3", typed));
        Assert.Contains("changed #n6 textbox \"Email\" value=\"a\" (was: #n6 textbox \"Email\")", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Increment", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Falls_back_to_the_whole_screen_when_most_of_it_changed()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        string[] other =
        [
            "#n20 document \"Pricing\"",
            " #n21 heading \"Plans\"",
            " #n22 button \"Choose Pro\"",
            " #n23 button \"Choose Team\"",
        ];
        var text = presenter.Update(Screen("b2", other, "/pricing"), new ScreenUpdateOptions { Lead = "Tapped #n5." });
        Assert.Contains("The screen changed substantially since revision b1. Current screen (revision b2, path /pricing, 4 nodes):", text, StringComparison.Ordinal);
        Assert.Contains(" #n22 button \"Choose Pro\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("added ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Compares_against_the_newest_screen_it_rendered_whole_or_as_changes()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var typed = Home.ToList();
        typed[5] = " #n6 textbox \"Email\" value=\"ada@example.test\"";
        presenter.Update(Screen("b2", typed));
        var pressed = typed.ToList();
        pressed[3] = " #n4 status \"Counter\" text=\"1\"";
        var text = presenter.Update(Screen("b3", pressed));
        // The email line was already reported and is not repeated.
        Assert.DoesNotContain("Email", text, StringComparison.Ordinal);
        Assert.Contains("changed #n4 status \"Counter\" text=\"1\" (was: #n4 status \"Counter\" text=\"0\")", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Notes_a_truncated_screen_on_a_change_update()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var text = presenter.Update(Screen("b2", [.. Home, " #n8 button \"More\""], truncated: true));
        Assert.Contains("1 added. The screen listing is truncated", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_no_removals_against_a_truncated_screen_whose_missing_nodes_were_cut_not_gone()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        // The walk stopped after four nodes: the link and the textbox may well still exist.
        var text = presenter.Update(Screen("b2", Home.Take(4), truncated: true));
        Assert.False(Regex.IsMatch(text, "^removed ", RegexOptions.Multiline, TimeSpan.FromSeconds(5)), text);
        Assert.DoesNotContain("Screen unchanged", text, StringComparison.Ordinal);
        Assert.Contains("no listed node changed. The screen listing is truncated", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_call_a_node_changed_when_only_its_depth_moved()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home));
        var next = Home.Select(line => line.Contains("#n4 ", StringComparison.Ordinal) ? " " + line : line);
        Assert.Contains("Screen unchanged since revision b1", presenter.Update(Screen("b2", next)), StringComparison.Ordinal);
    }
}
