// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Engine;

/// <summary>
/// Upstream's <c>describe('createEngineSession')</c> (<c>engine.test.ts</c>). Only the verbs <c>it</c> is here: the port has no
/// <c>defineEngine</c> or <c>createEngineSession</c>, and the rest of the file tests them.
/// </summary>
public sealed class CreateEngineSessionTests
{
    private static string[] Verbs(EngineCapabilities capabilities, bool canDismissKeyboard = false, params string[] pointerKinds) =>
        [.. ActContext.VerbsOf(capabilities, EngineActionKinds.Locator, pointerKinds, canDismissKeyboard).Select(verb => verb.ToString()).Order(StringComparer.Ordinal)];

    private static string[] VerbsOfKinds(params string[] kinds) =>
        [.. ActContext.VerbsOf(EngineCapabilities.None, kinds, []).Where(verb => verb != ExecutorVerb.Navigate).Select(verb => verb.ToString()).Order(StringComparer.Ordinal)];

    // Partial of upstream's "derives the grammar verbs from the declared action kinds and hooks": the port's engines declare capabilities and
    // an action-kind list, not hooks, and it has no `open` verb to derive. This checks the lines of the action kinds that unlock tap,
    // doubleTap, type, typeSecret, press, select, and check (and the port's clear), the keyboard, the pointer kinds, and the resize; the dismissal
    // is declared by IEngineSession.CanDismissKeyboard where upstream defines keyboard.dismiss, and the resize by EngineCapabilities.Resize where
    // upstream defines session.resize. Not ported: the lines for longPress, secondaryTap, hover, and dragTo beside the others, the scroll, scrollUntil,
    // scrollTo, and upload verbs that upstream derives from swipe, scrollIntoView, and setInputFiles (the port derives them from
    // EngineCapabilities), the perform hook, the empty engine, and the session.open and session.back lines (#1437).
    [Fact]
    public void Partial_Derives_the_keyboard_and_pointer_verbs_from_the_declared_hooks()
    {
        string[] node = ["Check", "Clear", "DoubleTap", "Drag", "Hover", "LongPress", "Navigate", "Press", "SecondaryTap", "Select", "Tap", "Type", "TypeSecret"];
        static string[] With(string[] verbs, params string[] more) => [.. verbs.Concat(more).Order(StringComparer.Ordinal)];

        // Each action kind unlocks its verb and no other; check sets a state, so it needs both directions; fill unlocks the plain and the secret fill.
        Assert.Equal(["Tap"], VerbsOfKinds("tap"));
        Assert.Equal(["DoubleTap", "LongPress", "SecondaryTap"], VerbsOfKinds("doubleTap", "longPress", "secondaryTap"));
        Assert.Equal(["Type", "TypeSecret"], VerbsOfKinds("fill"));
        Assert.Equal(["Press"], VerbsOfKinds("press"));
        Assert.Equal(["Select"], VerbsOfKinds("selectOption"));
        Assert.Equal(["Check"], VerbsOfKinds("check", "uncheck"));
        Assert.Empty(VerbsOfKinds("check"));
        Assert.Empty(VerbsOfKinds("uncheck"));
        Assert.Equal(["Clear"], VerbsOfKinds("check", "clear", "focus"));
        Assert.Equal(["Drag", "Hover"], VerbsOfKinds("hover", "dragTo", "scrollIntoView", "setInputFiles"));
        Assert.Empty(VerbsOfKinds());

        // The point verbs need their own pointer kind; other pointer kinds alone offer nothing.
        Assert.Equal(With(node, "HoverAt", "TapAt"), Verbs(EngineCapabilities.None, false, "tap", "hover"));
        Assert.Equal(With(node, "HoverAt"), Verbs(EngineCapabilities.None, false, "hover"));
        Assert.Equal(With(node, "DragAt"), Verbs(EngineCapabilities.None, false, "dragTo"));
        Assert.Equal(node, Verbs(EngineCapabilities.None, false, "longPress", "swipeTo"));

        // A keyboard adds typeText and pressKey, and dismissKeyboard only when it can be dismissed.
        Assert.Equal(With(node, "PressKey", "TypeText"), Verbs(EngineCapabilities.Keyboard));
        Assert.Equal(With(node, "DismissKeyboard", "PressKey", "TypeText"), Verbs(EngineCapabilities.Keyboard, true));

        // A dismissal without a keyboard is no verb: upstream's dismiss is a hook of the keyboard.
        Assert.Equal(node, Verbs(EngineCapabilities.None, true));

        // A resize hook adds the resize verb and nothing else.
        Assert.Equal(With(node, "Resize"), Verbs(EngineCapabilities.Resize));
    }
}
