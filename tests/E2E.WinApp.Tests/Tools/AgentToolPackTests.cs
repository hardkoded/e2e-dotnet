// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Tools;

/// <summary>.NET-only: upstream has no Windows engine. The agent tools the engine contributes.</summary>
public sealed class AgentToolPackTests
{
    [Fact]
    public async Task Offers_open_app_and_alert_on_the_windows_platform_as_mutating_tools()
    {
        await using var harness = new Harness();
        var tools = WinAppTools.Create(harness.Engine);
        Assert.Equal(["alert", "open_app"], tools.Keys.Order());
        foreach (var tool in tools.Values)
        {
            Assert.True(tool.Mutates);
            Assert.Equal(["windows"], tool.Platforms);
        }
    }

    [Fact]
    public void Needs_at_least_one_engine()
    {
        Assert.Throws<ArgumentException>(() => WinAppTools.Create());
    }

    [Fact]
    public async Task Open_app_opens_the_named_app_in_the_running_session()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Attachable["Calculator"] = new LaunchedApp(900, 0x2000, "Calculator", Owned: false);
        var tools = WinAppTools.Create(harness.Engine);
        var result = await tools["open_app"].ExecuteAsync(new JsonObject { ["app"] = "Calculator" }, CancellationToken.None);
        Assert.Equal("Opened Calculator.", result);
        Assert.Contains("attach Calculator", harness.Driver.Calls);
        var relaunched = await tools["open_app"].ExecuteAsync(new JsonObject { ["app"] = "Desk", ["relaunch"] = true }, CancellationToken.None);
        Assert.Equal("Opened Desk.", relaunched);
        Assert.Single(harness.Driver.Closed);
        Assert.Same(session, Assert.Single(harness.Engine.ActiveSessions));
    }

    [Fact]
    public async Task Alert_answers_the_open_dialog()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(RawElements.Window("W", RawElements.Dialog("dlg", "Confirm", RawElements.Button("btn-ok", "OK", "PrimaryButton"))));
        await session.ObserveAsync(CancellationToken.None);
        var tools = WinAppTools.Create(harness.Engine);
        Assert.Equal("Alert accepted.", await tools["alert"].ExecuteAsync(new JsonObject { ["action"] = "accept" }, CancellationToken.None));
        Assert.Contains("invoke btn-ok Invoke", harness.Driver.Calls);
        var error = await Assert.ThrowsAsync<TestException>(() => tools["alert"].ExecuteAsync(new JsonObject { ["action"] = "ignore" }, CancellationToken.None));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    [Fact]
    public async Task A_tool_outside_an_attempt_is_invalid_state()
    {
        await using var harness = new Harness();
        var tools = WinAppTools.Create(harness.Engine);
        var error = await Assert.ThrowsAsync<EngineException>(() => tools["open_app"].ExecuteAsync(new JsonObject { ["app"] = "Desk" }, CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        Assert.Contains("no Windows app attempt is running", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_argument_is_an_invalid_argument()
    {
        await using var harness = new Harness();
        await harness.OpenAsync();
        var tools = WinAppTools.Create(harness.Engine);
        var error = await Assert.ThrowsAsync<TestException>(() => tools["open_app"].ExecuteAsync(new JsonObject(), CancellationToken.None));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }
}
