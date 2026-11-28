// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: select')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceSelectTests : SurfaceTestBase
{
    [Fact]
    public async Task Selects_an_option_by_its_label_through_the_driver()
    {
        var (surface, fake, byTestId) = await Launched();
        await surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("- None -"), Operation());
        await surface.PerformAsync(
            byTestId("plan"),
            new LocatorAction.Select(new SelectOption { Value = "Team" }),
            Operation()
        );
        var acts = fake.Of("act").Select(call => (ActRequest)call.Args[0]!).ToArray();
        Assert.Equal(
            [("select", "- None -"), ("select", "Team")],
            acts.Select(act => (act.Action, act.Value)).ToArray()
        );
        await Rejects(
            surface.PerformAsync(
                byTestId("plan"),
                new LocatorAction.Select(new SelectOption { Index = 1 }),
                Operation()
            ),
            "UNSUPPORTED_CAPABILITY"
        );
    }

    [Fact]
    public async Task Reports_an_option_the_list_does_not_have_as_missing()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario { ["act"] = [Failure("ELEMENT_NOT_FOUND", "not_delivered")] }
        );
        await Rejects(
            surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("Team"), Operation()),
            "NOT_ACTIONABLE",
            "no option \"Team\""
        );
        Assert.Equal(["select"], fake.Acted());
    }

    [Fact]
    public async Task Names_a_menu_the_driver_could_not_close_after_a_select_of_a_missing_option()
    {
        var (surface, _, byTestId) = await Launched(
            new Scenario
            {
                ["act"] =
                [
                    Failure(
                        "ELEMENT_NOT_FOUND",
                        "delivered_unverified",
                        "the list has no option \"Enterprise\", and its menu did not close",
                        new Dictionary<string, object?> { ["menu"] = "open" }
                    ),
                ],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("Enterprise"), Operation()),
            "ACTION_MAY_HAVE_COMMITTED",
            "menu is still open",
            false
        );
    }

    [Fact]
    public async Task Reports_a_select_that_may_have_chosen_something_as_possibly_committed()
    {
        var (surface, _, byTestId) = await Launched(
            new Scenario
            {
                ["act"] =
                [
                    Failure("ACTION_FAILED", "delivered_unverified"),
                    Failure("ELEMENT_NOT_FOUND", "delivered_unverified"),
                ],
            }
        );
        await Rejects(
            surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("Team"), Operation()),
            "ACTION_MAY_HAVE_COMMITTED"
        );
        var second = await Rejects(
            surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("Team"), Operation()),
            "ACTION_MAY_HAVE_COMMITTED"
        );
        Assert.DoesNotContain("menu is still open", second.Message);
    }

    [Fact]
    public async Task Maps_a_select_the_driver_refused_by_its_code()
    {
        var refusals = new Dictionary<string, string>
        {
            ["ACTION_NOT_SUPPORTED"] = "UNSUPPORTED_CAPABILITY",
            ["POLICY_DENIED"] = "UNSUPPORTED_CAPABILITY",
            ["TIMEOUT"] = "NOT_ACTIONABLE",
            ["ACTION_FAILED"] = "NOT_ACTIONABLE",
            ["STALE_REF"] = "NODE_STALE",
        };
        foreach (var (refusal, code) in refusals)
        {
            var (surface, _, byTestId) = await Launched(new Scenario { ["act"] = [Failure(refusal, "not_delivered")] });
            await Rejects(surface.PerformAsync(byTestId("plan"), new LocatorAction.Select("Team"), Operation()), code);
        }
    }

    [Fact]
    public async Task Never_types_a_missing_option_into_an_editable_combo_box_as_free_text_a_select_there_is_refused_and_nothing_is_sent()
    {
        var editable = Screen(
            "w-mine",
            new
            {
                name = "Form",
                children = new object[]
                {
                    new
                    {
                        role = "combobox",
                        name = "Plan",
                        ref_id = "@s5:e1",
                        available_actions = new[] { "Click", "SetValue", "SetFocus" },
                    },
                },
            }
        );
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [editable] },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), null);
        await Rejects(
            surface.PerformAsync(
                observed.Roots[0].Children[0].Ref,
                new LocatorAction.Select("Enterprise"),
                Operation()
            ),
            "UNSUPPORTED_CAPABILITY",
            @"editable.*fill\(\)"
        );
        Assert.Empty(Inputs(fake));
    }
}
