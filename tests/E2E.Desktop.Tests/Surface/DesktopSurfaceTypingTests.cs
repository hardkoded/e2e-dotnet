// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: typing')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceTypingTests : SurfaceTestBase
{
    private static object[] CancelledInputs(FakeDriver fake) =>
        [.. fake.Of("cancelInput").Select(entry => entry.Args[0]!)];

    /// <summary>The AppKit form with the focus moved from the username field to the Sign in button, as the <c>replace</c> calls of upstream's tests do.</summary>
    private static Answer FocusOnSignIn(Answer form) =>
        Replaced(
            Replaced(form, "\"states\":[\"focused\"]", "\"states\":[]", all: false),
            "\"native_id\":{\"kind\":\"ax_identifier\",\"value\":\"sign-in\"}",
            "\"native_id\":{\"kind\":\"ax_identifier\",\"value\":\"sign-in\"},\"states\":[\"focused\"]",
            all: false
        );

    [Fact]
    public async Task Types_keystrokes_into_the_focused_field_into_the_app_without_headed_and_through_the_real_keyboard_with_it()
    {
        var (surface, fake, _) = await Launched();
        await surface.TypeTextAsync("ada", false, Operation());
        var typed = Last<ActRequest>(fake, "act")!;
        Assert.Equal(
            ("@s1y1hxmo5pexrl:e1", "type", "ada", false),
            (typed.Ref, typed.Action, typed.Value, typed.Physical)
        );
        var headed = await Launched([], new LaunchOptions { Options = new DesktopOptions { Headed = true } });
        await headed.Surface.TypeTextAsync("ada", false, Operation());
        var physical = Last<ActRequest>(headed.Fake, "act")!;
        Assert.Equal(("type", true), (physical.Action, physical.Physical));
    }

    [Fact]
    public async Task Stops_the_text_still_to_type_once_the_call_is_cut_off_so_no_key_lands_after_the_step_gave_up()
    {
        var (surface, fake, _) = await Launched(new Scenario { ["act"] = [Ok(null) with { SleepMs = 2_000 }] });
        using var controller = new CancellationTokenSource(100);
        await Rejects(surface.TypeTextAsync("ada", false, Operation(signal: controller.Token)), "CANCELLED");
        var inputId = Last<ActRequest>(fake, "act")!.InputId;
        Assert.Equal([(object)inputId!], CancelledInputs(fake));
        // A typing call that answers in time cancels nothing.
        var quick = await Launched();
        await quick.Surface.TypeTextAsync("ada", false, Operation());
        Assert.Empty(quick.Fake.Of("cancelInput"));
    }

    [Fact]
    public async Task Cancels_a_tap_the_call_gave_up_on_so_the_driver_sends_no_press_after_the_step_moved_on()
    {
        var (surface, fake, byTestId) = await Launched(new Scenario { ["act"] = [Ok(null) with { SleepMs = 2_000 }] });
        var signIn = byTestId("sign-in");
        using var controller = new CancellationTokenSource(100);
        await Rejects(
            surface.PerformAsync(signIn, new LocatorAction.Tap(), Operation(signal: controller.Token)),
            "CANCELLED"
        );
        var inputId = Last<ActRequest>(fake, "act")!.InputId;
        Assert.Equal([(object)inputId!], CancelledInputs(fake));
    }

    [Fact]
    public async Task Cancels_a_key_press_the_call_gave_up_on_so_the_driver_sends_no_key_after_the_step_moved_on()
    {
        var (surface, fake, _) = await Launched(new Scenario { ["press"] = [Ok(null) with { SleepMs = 2_000 }] });
        using var controller = new CancellationTokenSource(100);
        await Rejects(surface.PressKeyAsync("Enter", Operation(signal: controller.Token)), "CANCELLED");
        var press = Last<PressRequest>(fake, "press")!;
        Assert.True(press.TimeoutMs > 0);
        Assert.Equal([(object)press.InputId!], CancelledInputs(fake));
    }

    [Fact]
    public async Task Replaces_the_focused_field_as_a_keyboard_does_select_all_and_delete_and_never_by_an_accessibility_clear()
    {
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")] }
        );
        await surface.TypeTextAsync("ada", true, Operation());
        Assert.Equal(["selectAll", "press backspace", "type"], Inputs(fake));
        // A field whose selection accessibility cannot set gets the select-all key through the real keyboard; in the background, nothing.
        Scenario Refused() =>
            new()
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["act"] = [Failure("ACTION_NOT_SUPPORTED", "not_delivered"), Ok(null)],
            };
        var keys = await Launched(Refused(), new LaunchOptions { Options = new DesktopOptions { Headed = true } });
        await keys.Surface.TypeTextAsync("ada", true, Operation());
        Assert.Equal(["selectAll", "press cmd+a", "press backspace", "type"], Inputs(keys.Fake));
        var background = await Launched(Refused());
        await Rejects(background.Surface.TypeTextAsync("ada", true, Operation()), "NOT_ACTIONABLE", @"Cmd\+A");
        Assert.Equal(["selectAll"], Inputs(background.Fake));
    }

    [Fact]
    public async Task Sends_nothing_when_the_control_with_keyboard_focus_takes_no_text_so_no_select_all_or_delete_reaches_a_list_or_button()
    {
        var focusedButton = FocusOnSignIn(SnapshotAnswer("macos/appkit-form"));
        var (surface, fake, _) = await Launched(new Scenario { ["snapshot"] = [focusedButton] });
        foreach (var replace in new[] { true, false })
        {
            await Rejects(surface.TypeTextAsync("ada", replace, Operation()), "NOT_ACTIONABLE", "takes no text");
        }

        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Refuses_a_NUL_before_any_key_is_sent_so_a_replace_never_empties_the_field_for_nothing()
    {
        var (surface, fake, _) = await Launched();
        await Rejects(surface.TypeTextAsync("a\0b", true, Operation()), "NOT_ACTIONABLE");
        Assert.Empty(Inputs(fake));
        // Long text is the driver's to type in chunks.
        await surface.TypeTextAsync(new string('a', 20_000), false, Operation());
        Assert.Equal(["type"], fake.Acted());
    }

    [Fact]
    public async Task Sends_no_delete_when_select_all_moved_focus_off_the_field_so_it_never_reaches_another_control()
    {
        var form = Renamed("macos/appkit-form", "w-8032", "w-mine");
        var moved = FocusOnSignIn(form);
        var (surface, fake, _) = await Launched(new Scenario { ["snapshot"] = [form, form, form, moved] });
        await Rejects(
            surface.TypeTextAsync("ada", true, Operation()),
            "ACTION_MAY_HAVE_COMMITTED",
            "lost keyboard focus after select all"
        );
        Assert.Equal(["selectAll"], Inputs(fake));
    }

    [Fact]
    public async Task Types_nothing_when_the_delete_moved_focus_off_the_field_so_the_type_never_takes_focus_back_from_the_app()
    {
        var form = Renamed("macos/appkit-form", "w-8032", "w-mine");
        var moved = FocusOnSignIn(form);
        var (surface, fake, _) = await Launched(new Scenario { ["snapshot"] = [form, form, form, form, moved] });
        await Rejects(
            surface.TypeTextAsync("ada", true, Operation()),
            "ACTION_MAY_HAVE_COMMITTED",
            "lost keyboard focus after the delete"
        );
        Assert.Equal(["selectAll", "press backspace"], Inputs(fake));
    }

    [Fact]
    public async Task Reports_a_type_after_a_clear_that_changed_the_field_as_possibly_committed()
    {
        var (surface, _, _) = await Launched(
            new Scenario { ["act"] = [Ok(null), Failure("STALE_REF", "not_delivered")] }
        );
        await Rejects(surface.TypeTextAsync("ada", true, Operation()), "ACTION_MAY_HAVE_COMMITTED", retryable: false);
    }

    [Fact]
    public async Task Reads_again_rather_than_find_no_focused_field_or_nothing_to_scroll_when_a_read_runs_out_of_time()
    {
        var window = new
        {
            name = "Form",
            bounds = new
            {
                x = 0,
                y = 0,
                width = 200,
                height = 200,
            },
        };
        var cut = Screen(
            "w-mine",
            new
            {
                window.name,
                window.bounds,
                subtree_truncated = true,
                children = new object[]
                {
                    new
                    {
                        role = "button",
                        name = "OK",
                        ref_id = "@s:e1",
                        bounds = new
                        {
                            x = 10,
                            y = 10,
                            width = 40,
                            height = 20,
                        },
                    },
                },
            },
            new { complete = false, truncated = true }
        );
        var whole = Screen(
            "w-mine",
            new
            {
                window.name,
                window.bounds,
                children = Array.Empty<object>(),
            }
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [whole, cut] },
            new LaunchOptions { Observe = false }
        );
        await Rejects(
            surface.TypeTextAsync("ada", false, Operation()),
            "NODE_STALE",
            "which field has keyboard focus",
            true
        );
        await Rejects(
            surface.PerformAsync("root", new LocatorAction.Swipe(ScrollDirection.Down), Operation()),
            "NODE_STALE",
            "what in it scrolls",
            true
        );
    }
}
