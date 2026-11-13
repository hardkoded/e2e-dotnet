// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.AgentScreenUpdate;

/// <summary><c>describe('ScreenPresenter')</c> of <c>agent-screen-update.test.ts</c>.</summary>
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

    internal static ExecutorObservation Screen(
        string revision,
        IEnumerable<string> lines,
        string? path = null,
        bool truncated = false,
        bool treeUnavailable = false,
        ExecutorPixels? pixels = null,
        VisionDegradation? pixelsWithheld = null,
        ExecutorTabs? tabs = null) =>
        new(string.Join('\n', lines)) { Revision = revision, Path = path, Tabs = tabs, Truncated = truncated, TreeUnavailable = treeUnavailable, Pixels = pixels, PixelsWithheld = pixelsWithheld, Viewport = new TraceViewport(1280, 720) };

    private static ExecutorPixels Shot(byte value) => new([value], "image/png", 1, 1, 1, 0);

    [Fact]
    public void Retires_semantic_history_through_repeated_unavailable_captures_and_shows_recovery_whole()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", ["#root", " #save button \"Save\""]));
        var pixels = Shot(1);
        foreach (var revision in new[] { "b2", "b3" })
        {
            var output = presenter.Present(Screen(
                revision,
                ["[semantic capture unavailable: no nodes were read; previous node ids are no longer valid. Use the screenshot, never infer absence from this listing]"],
                truncated: true,
                treeUnavailable: true,
                pixels: pixels));
            Assert.Same(pixels, output.Pixels);
            var text = output.Text;
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
    public void Says_a_tab_the_app_opened_stays_in_the_background_then_shows_another_tab_whole_even_when_its_ids_match()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", Home, "/"));
        var opened = presenter.Update(Screen("b2", Home, "/", tabs: new ExecutorTabs(2, 1)), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true });

        // The tab is the tap's effect: no control is blamed for the unchanged screen.
        Assert.Contains("Tapped #n3.\n\nThe app opened a new tab (tab 2 of 2). The screen still shows tab 1; use switch_tab to see it. Screen unchanged since revision b1", opened, StringComparison.Ordinal);
        string[] popup = ["#n1 document \"Verification\"", " #n2 text \"Code TAB-88421\""];
        var switched = presenter.Update(Screen("b3", popup, "/verify", tabs: new ExecutorTabs(2, 2)), new ScreenUpdateOptions { Lead = "Switched to tab 2." });
        Assert.Contains("The screen shows tab 2 of 2 now. Current screen (revision b3, path /verify, tab 2 of 2, 2 nodes):", switched, StringComparison.Ordinal);
        Assert.Contains(" #n2 text \"Code TAB-88421\"", switched, StringComparison.Ordinal);
        Assert.DoesNotContain("changed #n1", switched, StringComparison.Ordinal);
        var back = presenter.Update(Screen("b4", Home, "/", tabs: new ExecutorTabs(2, 1)), new ScreenUpdateOptions { Lead = "Switched to tab 1." });
        Assert.Contains("The screen shows tab 1 of 2 now. Current screen (revision b4, path /, tab 1 of 2, 6 nodes):", back, StringComparison.Ordinal);
        Assert.Contains("Screen unchanged since revision b4", presenter.Update(Screen("b5", Home, "/", tabs: new ExecutorTabs(2, 1))), StringComparison.Ordinal);
        var two = presenter.Update(Screen("b6", Home, "/", tabs: new ExecutorTabs(4, 1)));
        Assert.Contains("The app opened 2 new tabs (tabs 3 to 4 of 4). The screen still shows tab 1; use switch_tab to see one.", two, StringComparison.Ordinal);
        var closed = presenter.Update(Screen("b7", Home, "/"));
        Assert.Contains("The other tabs closed; the screen shows the one tab still open. Current screen (revision b7, path /, 6 nodes):", closed, StringComparison.Ordinal);
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
    public void Says_no_listed_node_changed_after_an_action_never_that_it_had_no_effect_when_no_screenshot_was_compared()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home));
        // A tap that only fills a drawn dot leaves the tree as it was: the tree cannot tell a drawn effect from none.
        var treeOnly = presenter.Present(Screen("b2", Home), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.Contains("No listed node changed within the wait after this action", treeOnly, StringComparison.Ordinal);
        Assert.Contains("a drawn change needs a screenshot to see", treeOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", treeOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("look for another way", treeOnly, StringComparison.Ordinal);
        // The first screenshot has none before it to differ from, so it proves no more than the tree.
        var first = presenter.Present(Screen("b3", Home, pixels: Shot(1)), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.DoesNotContain("had no visible effect", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_listing_as_the_only_evidence_and_says_to_try_another_way_when_the_engine_captures_no_screenshot()
    {
        // An engine that captures none says so once asked; from then on the listing is all there is.
        var denied = new ScreenPresenter();
        denied.Open(Screen("c1", Home));
        denied.Present(Screen("c2", Home, pixelsWithheld: VisionDegradation.UnsupportedCapability));
        var after = denied.Present(Screen("c3", Home), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.Contains("No screenshot can be taken in this step", after, StringComparison.Ordinal);
        Assert.Contains("look for another way rather than repeating it", after, StringComparison.Ordinal);
        Assert.DoesNotContain("needs a screenshot to see", after, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_the_screenshot_changed_not_that_the_control_had_no_effect_when_the_listing_stood_still_under_a_moved_image()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home, pixels: Shot(1)));
        var moved = presenter.Present(Screen("b2", Home, pixels: Shot(2)), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.Contains("Tapped #n3.", moved, StringComparison.Ordinal);
        Assert.Contains("listed nodes unchanged; the screenshot changed", moved, StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", moved, StringComparison.Ordinal);
        // The same image again: the tree describes this screen, so the control is blamed as before.
        var still = presenter.Present(Screen("b3", Home, pixels: Shot(2)), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.Contains("had no visible effect", still, StringComparison.Ordinal);
        Assert.DoesNotContain("the screenshot changed", still, StringComparison.Ordinal);
        // A moved image is reported whether or not a change was expected.
        var observed = presenter.Present(Screen("b4", Home, pixels: Shot(3)), new ScreenUpdateOptions { Lead = "Observed.", ExpectChange = false }).Text;
        Assert.Contains("listed nodes unchanged; the screenshot changed", observed, StringComparison.Ordinal);
        Assert.DoesNotContain("Screen unchanged since", observed, StringComparison.Ordinal);
    }

    [Fact]
    public void Treats_unproven_masking_as_one_screens_refusal_not_the_steps()
    {
        var presenter = new ScreenPresenter();
        presenter.Open(Screen("b1", Home));
        presenter.Present(Screen("b2", Home, pixelsWithheld: VisionDegradation.MaskingUnproven));
        var later = presenter.Present(Screen("b3", Home), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true }).Text;
        Assert.DoesNotContain("No screenshot can be taken in this step", later, StringComparison.Ordinal);
        Assert.Contains("a drawn change needs a screenshot to see", later, StringComparison.Ordinal);
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
