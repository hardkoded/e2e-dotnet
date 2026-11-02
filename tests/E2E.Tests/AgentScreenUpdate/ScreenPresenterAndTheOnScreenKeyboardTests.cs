// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentScreenUpdate;

/// <summary><c>describe('ScreenPresenter and the on-screen keyboard')</c> of <c>agent-screen-update.test.ts</c>.</summary>
public sealed class ScreenPresenterAndTheOnScreenKeyboardTests
{
    private static readonly string[] Keyboard =
    [
        " #n20 keyboard \"Padding-Left\"",
        "  #n21 key \"q\"",
        "  #n22 key \"w\"",
    ];

    private static readonly string[] Form =
    [
        "#n1 document \"Sign in\"",
        " #n2 heading \"Welcome back\"",
        " #n3 textbox \"Email\" value=\"a@b.c\"",
        " #n4 textbox \"Password\" value=<secure>",
        " #n5 link \"Forgot password\"",
        " #n6 checkbox \"Remember me\"",
        " #n7 button \"Log in\"",
        " #n8 text \"No account yet?\"",
    ];

    private static ExecutorObservation Screen(string revision, IEnumerable<string> lines, bool truncated = false) =>
        ScreenPresenterTests.Screen(revision, lines, truncated: truncated);

    [Fact]
    public void Tells_the_model_when_an_action_closed_the_keyboard_so_a_swallowed_tap_is_retried()
    {
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", [.. Form, .. Keyboard]));
        var text = presenter.Update(Screen("b2", Form), new ScreenUpdateOptions { Lead = "Tapped #n7.", ExpectChange = true });
        Assert.Contains("removed #n20 keyboard \"Padding-Left\"", text, StringComparison.Ordinal);
        Assert.Contains("The on-screen keyboard closed with this action.", text, StringComparison.Ordinal);
        Assert.Contains("act on it again now that the keyboard is down", text, StringComparison.Ordinal);
        var keys = Enumerable.Range(0, 40).Select(i => $"  #k{i} key \"{i}\"");
        presenter.Initial(Screen("b3", [.. Form, " #n20 keyboard \"Padding-Left\"", .. keys]));
        var whole = presenter.Update(Screen("b4", Form), new ScreenUpdateOptions { Lead = "Tapped #n3.", ExpectChange = true });
        Assert.Contains("changed substantially", whole, StringComparison.Ordinal);
        Assert.Contains("The on-screen keyboard closed with this action.", whole, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_Stays_quiet_on_a_truncated_listing_an_explicit_dismissal_or_a_keyboard_that_stays_or_appears()
    {
        // Upstream checks `update` and `present`; the port has the first only, since `present` carries the screenshot.
        var presenter = new ScreenPresenter();
        presenter.Initial(Screen("b1", [.. Form, .. Keyboard]));
        Assert.DoesNotContain(
            "keyboard closed",
            presenter.Update(Screen("b2", Form, truncated: true), new ScreenUpdateOptions { Lead = "Tapped #n7.", ExpectChange = true }),
            StringComparison.Ordinal);
        presenter.Initial(Screen("b1", [.. Form, .. Keyboard]));
        Assert.DoesNotContain(
            "keyboard closed",
            presenter.Update(Screen("b2", Form), new ScreenUpdateOptions { Lead = "Dismissed the keyboard.", KeyboardNote = false }),
            StringComparison.Ordinal);
        presenter.Initial(Screen("b3", Form));
        Assert.DoesNotContain("keyboard closed", presenter.Update(Screen("b4", [.. Form, .. Keyboard]), new ScreenUpdateOptions { Lead = "Tapped #n2." }), StringComparison.Ordinal);
        Assert.DoesNotContain("keyboard closed", presenter.Update(Screen("b5", [.. Form, .. Keyboard]), new ScreenUpdateOptions { Lead = "Tapped #n3." }), StringComparison.Ordinal);
    }
}
