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

    // Partial of upstream's "derives the grammar verbs from the declared action kinds and hooks": the port's engines declare capabilities and
    // an action-kind list, not hooks, and it has no `open` verb to derive. This checks the lines of the keyboard, the pointer
    // kinds, and the resize; the dismissal is declared by IEngineSession.CanDismissKeyboard where upstream defines keyboard.dismiss, and the
    // resize by EngineCapabilities.Resize where upstream defines session.resize.
    [Fact]
    public void Partial_Derives_the_keyboard_and_pointer_verbs_from_the_declared_hooks()
    {
        string[] node = ["Check", "Clear", "DoubleTap", "Drag", "Hover", "LongPress", "Navigate", "Press", "SecondaryTap", "Select", "Tap", "Type", "TypeSecret"];
        static string[] With(string[] verbs, params string[] more) => [.. verbs.Concat(more).Order(StringComparer.Ordinal)];

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
