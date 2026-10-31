// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.ReportJson;
using E2E.Tests.ScriptedScreen;

namespace E2E.Tests.ScriptedActions;

/// <summary>
/// Node actions and the app hooks through a session over the scripted reference screen: every action reaches
/// the engine with the node and payload the test named, and <c>app.open</c>, <c>back</c>, <c>restart</c>, and
/// <c>clearState</c> reach the session hooks. Upstream runs each case in the real runner and reads the engine
/// calls and the report steps. The port has no runner, so each case runs in the test body, reads what the engine recorded, and
/// reads the steps from <c>report.json</c>. Not ported, because the port has no such feature: the object
/// forms of <c>selectOption</c> (<c>label</c>, <c>value</c>, <c>index</c>); the first test is partial for that reason. It also does not run
/// <c>setInputFiles</c>, whose files the scripted engine does not read. It is partial too
/// because <c>click</c> and <c>dblclick</c> are the port's <c>tap</c> and <c>doubleTap</c> steps, so their api names are not checked.
/// </summary>
public sealed class ScriptedEngineActionsAndAppHooksTests
{
    private const string BaseUrl = "http://127.0.0.1:4599";

    [Fact]
    public async Task Partial_Dispatches_every_action_kind_with_the_expected_ref_and_payload()
    {
        using var report = new ReportDirectory();
        using var scene = ReferenceScene.Create(open: false);
        await using var session = await StartAsync(new ScriptedEngine(scene), report);
        var screen = session.Screen;
        await session.App.OpenAsync("/");
        var submit = screen.GetByRole("button", "Submit");
        await submit.TapAsync();
        await submit.ClickAsync();
        await submit.DoubleTapAsync();
        await submit.DblClickAsync();
        await submit.SecondaryTapAsync();
        await submit.LongPressAsync();
        await submit.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(800) });
        await screen.GetByLabel("Email").FillAsync("ada@example.test");
        await Expect.That(screen.GetByLabel("Email")).ToHaveValueAsync("ada@example.test");
        await screen.GetByLabel("Email").ClearAsync();
        await Expect.That(screen.GetByLabel("Email")).ToHaveValueAsync("");
        await screen.GetByLabel("City").PressSequentiallyAsync("Wa");
        await Expect.That(screen.GetByLabel("City")).ToHaveValueAsync("Wa");
        await Expect.That(screen.GetByLabel("City")).ToBeFocusedAsync();
        await screen.GetByLabel("City").PressSequentiallyAsync("rs", new PressSequentiallyOptions { Delay = TimeSpan.FromMilliseconds(10) });
        await Expect.That(screen.GetByLabel("City")).ToHaveValueAsync("Wars");
        await screen.GetByLabel("City").PressAsync("Backspace");
        await Expect.That(screen.GetByLabel("City")).ToHaveValueAsync("War");
        await screen.GetByLabel("City").PressAsync("Shift+Tab");
        await screen.GetByLabel("City").PressAsync("$");
        await Expect.That(screen.GetByLabel("City")).ToHaveValueAsync("War$");
        await screen.GetByRole("checkbox", "Notifications").CheckAsync();
        await Expect.That(screen.GetByRole("checkbox", "Notifications")).ToBeCheckedAsync();
        await screen.GetByRole("checkbox", "Notifications").UncheckAsync();
        await Expect.That(screen.GetByRole("checkbox", "Notifications")).Not.ToBeCheckedAsync(timeout: TimeSpan.FromMilliseconds(500));
        await screen.GetByLabel("Plan").SelectOptionAsync("Pro");
        await Expect.That(screen.GetByLabel("Plan")).ToHaveValueAsync("pro");
        await screen.GetByLabel("Plan").SelectOptionAsync("team");
        await Expect.That(screen.GetByLabel("Plan")).ToHaveValueAsync("team");
        await screen.GetByLabel("Focus target").FocusAsync();
        await Expect.That(screen.GetByLabel("Focus target")).ToBeFocusedAsync();
        await Expect.That(screen.GetByLabel("City")).Not.ToBeFocusedAsync(timeout: TimeSpan.FromMilliseconds(500));
        await submit.HoverAsync();
        await screen.GetByTestId("drag").DragToAsync(screen.GetByTestId("drop"));
        await submit.ScrollIntoViewAsync();
        await screen.GetByRole("list", "Feed").SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Down, Momentum = Momentum.Slow });
        await screen.GetByRole("list", "Feed").SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Left });
        await screen.GetByRole("button", "Increment").TapAsync();
        await screen.GetByRole("button", "Increment").TapAsync();
        await Expect.That(screen.GetByRole("status", "Count")).ToHaveTextAsync("2");
        await screen.GetByRole("button", "Bold").TapAsync();
        await Expect.That(screen.GetByRole("button", new RoleOptions { Name = "Bold", Pressed = true })).ToBeVisibleAsync();

        var dragged = Assert.Single(scene.Performs, performed => performed.Action is LocatorAction.DragTo);
        var dropNode = ((LocatorAction.DragTo)dragged.Action).Target;
        Assert.Equal("drop", dropNode.Ref);
        Assert.Equal(
            new (string, LocatorAction)[]
            {
                ("submit", new LocatorAction.Tap()),
                ("submit", new LocatorAction.Tap()),
                ("submit", new LocatorAction.DoubleTap()),
                ("submit", new LocatorAction.DoubleTap()),
                ("submit", new LocatorAction.SecondaryTap()),
                ("submit", new LocatorAction.LongPress()),
                ("submit", new LocatorAction.LongPress(TimeSpan.FromMilliseconds(800))),
                ("email", new LocatorAction.Fill("ada@example.test", Sensitive: false)),
                ("email", new LocatorAction.Clear()),
                ("city", new LocatorAction.PressSequentially("Wa")),
                ("city", new LocatorAction.PressSequentially("rs", TimeSpan.FromMilliseconds(10))),
                ("city", new LocatorAction.Press("Backspace")),
                ("city", new LocatorAction.Press("Shift+Tab")),
                ("city", new LocatorAction.Press("$")),
                ("notify", new LocatorAction.Check()),
                ("notify", new LocatorAction.Uncheck()),
                ("plan", new LocatorAction.Select("Pro")),
                ("plan", new LocatorAction.Select("team")),
                ("focus-target", new LocatorAction.Focus()),
                ("submit", new LocatorAction.Hover()),
                ("drag", new LocatorAction.DragTo(dropNode)),
                ("submit", new LocatorAction.ScrollIntoView()),
                ("feed", new LocatorAction.Swipe(ScrollDirection.Down, Momentum.Slow)),
                ("feed", new LocatorAction.Swipe(ScrollDirection.Left)),
                ("increment", new LocatorAction.Tap()),
                ("increment", new LocatorAction.Tap()),
                ("bold", new LocatorAction.Tap()),
            },
            scene.Performs);
        session.Complete();
        await session.DisposeAsync();

        var steps = report.LastAttempt()["steps"]!.AsArray();
        var apis = steps.Select(each => (string)each!["api"]!).ToList();
        Assert.All(
            new[] { "locator.tap", "locator.doubleTap", "locator.secondaryTap", "locator.longPress", "locator.hover", "locator.dragTo", "locator.swipe", "locator.fill", "locator.clear", "locator.pressSequentially", "locator.press", "locator.check", "locator.uncheck", "locator.selectOption", "locator.focus", "locator.scrollIntoView" },
            api => Assert.Contains(api, apis));
        Assert.Equal("getByLabel(\"Email\")", (string)report.Step("locator.fill")["label"]!);
        Assert.Equal("getByLabel(\"City\")", (string)report.Step("locator.pressSequentially")["label"]!);
    }

    [Fact]
    public async Task Reaches_the_session_hooks_in_order_for_app_open_back_restart_and_clearState()
    {
        using var scene = ReferenceScene.Create(open: false);
        var engine = new ScriptedEngine(scene);
        using var report = new ReportDirectory();
        await using var session = await StartAsync(engine, report);
        await session.App.OpenAsync("/");
        await session.App.OpenAsync("/settings");
        await session.App.BackAsync();
        await session.App.RestartAsync();
        await session.App.ClearStateAsync();
        await Expect.That(session.Screen.GetByRole("button", "Submit")).ToBeVisibleAsync();
        Assert.Equal(
            ["open " + BaseUrl + "/", "open " + BaseUrl + "/settings", "back", "restart", "open " + BaseUrl + "/", "reset", "open " + BaseUrl + "/"],
            engine.SessionCalls);
        session.Complete();
        await session.DisposeAsync();

        Assert.Equal(["app.open", "app.open", "app.back", "app.restart", "app.clearState"], report.LastAttempt()["steps"]!.AsArray().Select(each => (string)each!["api"]!).Where(api => api.StartsWith("app.", StringComparison.Ordinal)));
    }

    private static Task<E2ESession> StartAsync(ScriptedEngine engine, ReportDirectory report)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            ResultsDirectory = report.Results,
            Engine = engine,
            BaseUrl = BaseUrl,
            TestTitle = "scripted actions",
            CacheEnabled = false,
        });
    }
}
