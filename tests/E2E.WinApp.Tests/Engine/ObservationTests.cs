// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Engine;

/// <summary>.NET-only: upstream has no Windows engine. What one observation reports and how its refs bind.</summary>
public sealed class ObservationTests
{
    [Fact]
    public async Task Reports_the_app_and_window_title_as_the_route_under_one_screen_root()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal("Desk / Settings", observation.Route);
        Assert.Equal("Desk / Settings", session.Route);
        var root = Assert.Single(observation.Roots);
        Assert.Equal(UiaProjection.RootId, root.Ref);
        Assert.Equal(new BoundingBox(0, 0, 800, 600), root.Rect);
        var window = Assert.Single(root.Children);
        Assert.Equal("window", window.Role);
        Assert.Equal("Settings", window.Name);
        Assert.False(observation.Truncated);
        Assert.Null(observation.KeyboardVisible);
    }

    [Fact]
    public async Task Lists_every_visible_window_of_the_app_after_the_main_one_in_the_same_space()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Script(session.App!.ProcessId, 0x2000, "", RawElements.Element("popup", "Window", null, null, new Rect(300, 250, 200, 100), RawElements.ListItem("opt-1", "Large") with { Bounds = new Rect(300, 250, 200, 24) }) with { ClassName = "Xaml_WindowedPopupClass" }, new Rect(300, 250, 200, 100));
        var observation = await session.ObserveAsync(CancellationToken.None);
        var roots = observation.Roots[0].Children;
        Assert.Equal(2, roots.Count);
        Assert.Equal("window", roots[0].Role);
        Assert.Equal("dialog", roots[1].Role);
        Assert.Equal(new BoundingBox(200, 200, 200, 24), Assert.Single(roots[1].Children).Rect);
        Assert.Equal("Desk / Settings", observation.Route);
    }

    [Fact]
    public async Task Scales_boxes_by_the_window_monitor_dpi()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Script(session.App!.ProcessId, FakeWindowDriver.MainHandle, "Settings", RawElements.Settings(), new Rect(100, 50, 1600, 1200), scale: 2);
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal(new BoundingBox(0, 0, 800, 600), observation.Roots[0].Rect);
        Assert.Equal(new BoundingBox(200, 275, 40, 15), Harness.ByTestId(observation, "save-button").Rect);
    }

    [Fact]
    public async Task Binds_each_ref_and_keeps_an_older_one_actionable_until_its_element_is_gone()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var first = await session.ObserveAsync(CancellationToken.None);
        var save = Harness.ByTestId(first, "save-button");
        var second = await session.ObserveAsync(CancellationToken.None);
        Assert.NotEqual(save.Ref, Harness.ByTestId(second, "save-button").Ref);
        Assert.Equal("btn-save", session.Resolve(save).Handle);
        await session.PerformAsync(save, new LocatorAction.Tap(), CancellationToken.None);
        Assert.Contains("invoke btn-save Auto", harness.Driver.Calls);

        harness.Driver.Vanished.Add("btn-save");
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(save, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NodeStale, error.Code);
        Assert.True(error.Retryable);
    }

    [Fact]
    public async Task A_ref_the_session_never_minted_is_stale()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        await session.ObserveAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(new SemanticNode { Ref = "n999" }, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NodeStale, error.Code);
        Assert.Contains("n999 is not part of the newest observation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_the_largest_window_when_the_first_one_closed()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var pid = session.App!.ProcessId;
        harness.Driver.Windows[pid].Clear();
        harness.Driver.Script(pid, 0x3000, "Main", RawElements.Window("Main"), new Rect(0, 0, 1000, 700));
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal("Desk / Main", observation.Route);
        Assert.Equal(0x3000, session.MainWindow);
    }

    [Fact]
    public async Task An_app_with_no_window_left_asks_for_an_open()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Windows[session.App!.ProcessId].Clear();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        Assert.Contains("call app.open()", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Marks_the_observation_truncated_when_the_node_budget_runs_out()
    {
        await using var harness = new Harness();
        var items = Enumerable.Range(1, ObservationLimits.Nodes + 5).Select(index => RawElements.ListItem($"i{index}", $"Item {index}")).ToArray();
        var session = await harness.OpenAsync(RawElements.Window("Big", RawElements.List("l", "Items", items)));
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.True(observation.Truncated);
        Assert.Equal(ObservationLimits.Nodes, Harness.Walk(observation.Roots).Count() - 1);
    }

    [Fact]
    public async Task A_driver_failure_on_the_observation_is_an_engine_failure()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Faults["inspect"] = new WindowDriverException(DriverFault.Other, "COM call failed");
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.EngineFailure, error.Code);
        Assert.Contains("observe failed: COM call failed", error.Message, StringComparison.Ordinal);
    }
}
