// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Helpers;

namespace E2E.Tests.Engine;

/// <summary>
/// Upstream's <c>describe('createEngineSession')</c> (<c>engine.test.ts</c>): the engine-to-session adapter over the engine-SPI <see cref="EngineManifest"/>
/// handle. The port's own verb logic (<c>ActContext</c>, for the IEngine path the agent runs on) is covered by the partial test at the top.
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

    private static EngineSpec Toy() => new()
    {
        Name = "toy",
        Version = "1.0.0",
        SpiVersion = 1,
        Observe = (_, _) => Task.FromResult(Snapshot.Of([Snapshot.Node("n1", "Save"), Snapshot.Node("n2", "Cancel")])),
    };

    private static ITargetSession Session(EngineSpec spec) =>
        EngineSessions.CreateEngineSession(new EngineSessionOptions { Engine = EngineManifest.DefineEngine(spec), TargetName = "toy-target" });

    [Fact]
    public async Task Mints_revisions_and_stamps_them_onto_every_ref()
    {
        var session = Session(Toy());

        var first = await session.ObserveAsync(Snapshot.Operation);
        var second = await session.ObserveAsync(Snapshot.Operation);

        Assert.NotEqual(first.Revision, second.Revision);
        Assert.Equal("semantic", second.Kind);
        Assert.Equal(second.Revision, second.Tree!.Children![0].Ref.Revision);
        Assert.Equal(new ObservationRedaction(0, 0), second.Redaction);
    }

    [Fact]
    public async Task Routes_every_node_action_through_perform_and_fails_loud_on_undeclared_members()
    {
        var calls = new List<string>();
        var session = Session(Toy() with
        {
            Actions = EngineActionKinds.Locator,
            Perform = (reference, action, _) =>
            {
                calls.Add($"{EngineActionKinds.Of(action)}:{reference.Id}");
                return Task.CompletedTask;
            },
        });

        await session.PerformAsync(new NodeRef("n1", "b1"), new LocatorAction.Tap(), Snapshot.Operation);
        await session.PerformAsync(new NodeRef("n1", "b1"), new LocatorAction.Press("Enter"), Snapshot.Operation);

        Assert.Equal(["tap:n1", "press:n1"], calls);
        Assert.True(session.Actions.SetEquals(EngineActionKinds.Locator));
        Assert.Contains("locators", (await Assert.ThrowsAsync<ConfigurationException>(() => session.LocateAsync(new LocatorExpression(), Snapshot.Operation))).Message, StringComparison.Ordinal);
        Assert.Contains("screenshots", (await Assert.ThrowsAsync<ConfigurationException>(() => session.Artifacts.ScreenshotAsync(null, Snapshot.Operation))).Message, StringComparison.Ordinal);
        Assert.Contains("navigation", (await Assert.ThrowsAsync<ConfigurationException>(() => session.App.OpenAsync("https://example.test/", Snapshot.Operation))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_an_undeclared_action_kind_before_it_reaches_the_engine()
    {
        var calls = new List<string>();
        var session = Session(Toy() with
        {
            Actions = ["tap"],
            Perform = (reference, action, _) =>
            {
                calls.Add($"{EngineActionKinds.Of(action)}:{reference.Id}");
                return Task.CompletedTask;
            },
        });

        Assert.Equal(["tap"], session.Actions.Order(StringComparer.Ordinal));
        var check = await Assert.ThrowsAsync<ConfigurationException>(() => session.PerformAsync(new NodeRef("n1", "b1"), new LocatorAction.Check(), Snapshot.Operation));
        Assert.Equal("UNSUPPORTED_CAPABILITY", check.Code);
        Assert.Contains("no engine capability for the \"check\" action: engine toy does not implement it", check.Message, StringComparison.Ordinal);
        var swipe = await Assert.ThrowsAsync<ConfigurationException>(() => session.SwipeAsync(ScrollDirection.Down, null, Snapshot.Operation));
        Assert.Contains("no engine capability for swipe gestures", swipe.Message, StringComparison.Ordinal);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Routes_a_point_action_through_performAt_only_for_a_declared_pointer_kind()
    {
        var performed = new List<string>();
        var session = Session(Toy() with
        {
            PerformAt = (point, action, _) =>
            {
                performed.Add($"{action.GetType().Name}@{point.X},{point.Y}");
                return Task.CompletedTask;
            },
            PointerActions = ["tap", "longPress"],
        });

        await session.PerformAtAsync(new Point(3, 4), new PointerAction.Tap(), Snapshot.Operation);
        await session.PerformAtAsync(new Point(5, 6), new PointerAction.LongPress(TimeSpan.FromMilliseconds(800)), Snapshot.Operation);
        var secondary = await Assert.ThrowsAsync<ConfigurationException>(() => session.PerformAtAsync(new Point(1, 1), new PointerAction.SecondaryTap(), Snapshot.Operation));
        Assert.Contains("the \"secondaryTap\" action at a point", secondary.Message, StringComparison.Ordinal);

        Assert.Equal(["Tap@3,4", "LongPress@5,6"], performed);
        var none = Session(Toy());
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => none.PerformAtAsync(new Point(1, 1), new PointerAction.Tap(), Snapshot.Operation));
        Assert.Contains("point", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swipes_the_viewport_as_perform_root_swipe_observing_first_when_nothing_named_the_root_yet()
    {
        var performed = new List<(NodeRef Reference, LocatorAction Action)>();
        var observes = 0;
        var session = Session(Toy() with
        {
            Observe = (_, _) =>
            {
                observes++;
                return Task.FromResult(new EngineSnapshot(
                    new EngineNode(new NodeRef("screen", ""), Role: "root", Children: [Snapshot.Node("n1", "Save")]),
                    Snapshot.Viewport));
            },
            Actions = ["swipe"],
            Perform = (reference, action, _) =>
            {
                performed.Add((reference, action));
                return Task.CompletedTask;
            },
        });

        await session.SwipeAsync(ScrollDirection.Down, null, Snapshot.Operation);
        Assert.Equal(1, observes);
        Assert.Equal((new NodeRef("screen", "b1"), new LocatorAction.Swipe(ScrollDirection.Down, null)), performed[0]);

        // A later observation names the root again; the swipe reuses it without observing.
        await session.ObserveAsync(Snapshot.Operation);
        await session.SwipeAsync(ScrollDirection.Left, Momentum.Fast, Snapshot.Operation);
        Assert.Equal(2, observes);
        Assert.Equal((new NodeRef("screen", "b2"), new LocatorAction.Swipe(ScrollDirection.Left, Momentum.Fast)), performed[1]);
    }

    [Fact]
    public async Task Carries_the_engine_location_and_viewport_onto_the_observation()
    {
        var session = Session(Toy() with { Observe = (_, _) => Task.FromResult(Snapshot.Of([], location: "app://device/Home")) });

        var observation = await session.ObserveAsync(Snapshot.Operation);

        Assert.Equal("app://device/Home", observation.Location);
        Assert.Equal(Snapshot.Viewport, observation.Viewport);
    }

    [Fact]
    public void Derives_the_grammar_verbs_from_the_declared_action_kinds_and_hooks()
    {
        string[] VerbsOf(EngineSpec spec) => [.. Session(spec).Verbs.Order(StringComparer.Ordinal)];
        Task Done() => Task.CompletedTask;
        var keyboard = new EngineKeyboardSpec { Type = (_, _, _) => Done(), Press = (_, _) => Done() };

        Assert.Empty(VerbsOf(Toy()));
        Assert.Equal(
            ["check", "doubleTap", "drag", "hover", "longPress", "press", "scroll", "scrollTo", "scrollUntil", "secondaryTap", "select", "tap", "type", "typeSecret", "upload"],
            VerbsOf(Toy() with { Actions = EngineActionKinds.Locator, Perform = (_, _, _) => Done() }));
        Assert.Equal(["tap"], VerbsOf(Toy() with { Actions = ["tap"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["doubleTap", "longPress", "secondaryTap"], VerbsOf(Toy() with { Actions = ["doubleTap", "longPress", "secondaryTap"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["type", "typeSecret"], VerbsOf(Toy() with { Actions = ["fill"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["press"], VerbsOf(Toy() with { Actions = ["press"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["select"], VerbsOf(Toy() with { Actions = ["selectOption"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["scroll", "scrollUntil"], VerbsOf(Toy() with { Actions = ["swipe"], Perform = (_, _, _) => Done() }));
        Assert.Equal(["drag", "hover", "scrollTo", "upload"], VerbsOf(Toy() with { Actions = ["hover", "dragTo", "scrollIntoView", "setInputFiles"], Perform = (_, _, _) => Done() }));
        // `check` sets a state, so it needs both directions; `clear` and `focus` unlock nothing for the agent.
        Assert.Equal(["check"], VerbsOf(Toy() with { Actions = ["check", "uncheck"], Perform = (_, _, _) => Done() }));
        Assert.Empty(VerbsOf(Toy() with { Actions = ["check", "clear", "focus"], Perform = (_, _, _) => Done() }));
        // The point verbs need their own pointer kind; other pointer kinds alone offer nothing.
        Assert.Equal(["hoverAt", "tapAt"], VerbsOf(Toy() with { PerformAt = (_, _, _) => Done(), PointerActions = ["tap", "hover"] }));
        Assert.Equal(["hoverAt"], VerbsOf(Toy() with { PerformAt = (_, _, _) => Done(), PointerActions = ["hover"] }));
        Assert.Equal(["dragAt"], VerbsOf(Toy() with { PerformAt = (_, _, _) => Done(), PointerActions = ["dragTo"] }));
        Assert.Empty(VerbsOf(Toy() with { PerformAt = (_, _, _) => Done(), PointerActions = ["longPress", "swipeTo"] }));
        Assert.Equal(["pressKey", "typeText"], VerbsOf(Toy() with { Keyboard = keyboard }));
        Assert.Equal(["dismissKeyboard", "pressKey", "typeText"], VerbsOf(Toy() with { Keyboard = keyboard with { Dismiss = _ => Done() } }));
        Assert.Equal(["navigate"], VerbsOf(Toy() with { Session = new EngineSessionSpec { Open = (_, _) => Done() } }));
        Assert.Equal(["back"], VerbsOf(Toy() with { Session = new EngineSessionSpec { Back = _ => Done(), Restart = _ => Done() } }));
        Assert.Empty(VerbsOf(Toy() with { Session = new EngineSessionSpec { Restart = _ => Done() } }));
        Assert.Equal(["resize"], VerbsOf(Toy() with { Session = new EngineSessionSpec { Resize = (_, _) => Done() } }));
    }

    [Fact]
    public async Task Rejects_a_superseded_located_ref_but_lets_observation_refs_through_to_the_engine()
    {
        var performed = new List<string>();
        var session = Session(Toy() with
        {
            Locate = (_, _) => Task.FromResult<IReadOnlyList<EngineNode>>([Snapshot.Node("n1", "Save")]),
            Actions = ["tap"],
            Perform = (reference, _, _) =>
            {
                performed.Add(reference.Revision);
                return Task.CompletedTask;
            },
        });

        var first = (await session.LocateAsync(new LocatorExpression(), Snapshot.Operation))[0];
        await session.LocateAsync(new LocatorExpression(), Snapshot.Operation);
        var stale = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(first, new LocatorAction.Tap(), Snapshot.Operation));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);

        // The same id from an observation is the engine's to check, not the adapter's.
        var observed = await session.ObserveAsync(Snapshot.Operation);
        await session.PerformAsync(new NodeRef("n1", observed.Revision), new LocatorAction.Tap(), Snapshot.Operation);
        Assert.Equal([observed.Revision], performed);
    }

    [Fact]
    public async Task Ends_the_attempt_exactly_once_handing_endAttempt_the_cleanup_budget()
    {
        var contexts = new List<EngineCleanupContext>();
        var session = Session(Toy() with { EndAttempt = context => { contexts.Add(context); return Task.CompletedTask; } });
        var operation = Snapshot.Operation with { Timeout = TimeSpan.FromSeconds(7) };

        await session.CloseAsync(operation);
        await session.CloseAsync(operation);

        Assert.Single(contexts);
        Assert.Equal(TimeSpan.FromSeconds(7), contexts[0].Timeout);
        Assert.Equal(operation.Signal, contexts[0].Signal);
    }

    [Fact]
    public async Task Reports_missing_observation_as_an_unsupported_capability()
    {
        var session = EngineSessions.CreateEngineSession(new EngineSessionOptions { Engine = null, TargetName = "bare" });

        var error = await Assert.ThrowsAsync<ConfigurationException>(() => session.ObserveAsync(Snapshot.Operation));
        Assert.Contains("no engine capability for observation", error.Message, StringComparison.Ordinal);
        Assert.Empty(session.Verbs);
    }
}
