// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow.Tests.Client;

public sealed class CaptureTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-devflow-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Writes_the_native_screenshot_and_reports_its_size_against_the_window()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        var path = Path.Combine(_directory, "shot.png");

        var result = await client.CallAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(path)!.ToJsonString()}},"normalizeStatusBar":true}""");

        var call = Assert.Single(agent.Calls);
        Assert.Equal("GET /api/v1/ui/screenshot", call.Route);
        Assert.Equal("native", call.Query("scale"));
        Assert.Equal(FakeAgent.Png(780, 1688), await File.ReadAllBytesAsync(path));
        Json.AssertEqual($$"""{"path":{{JsonValue.Create(path)!.ToJsonString()}},"width":780,"height":1688,"logicalWidth":390,"logicalHeight":844,"pixelDensity":2}""", result);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Reads_the_tree_first_when_the_window_size_is_not_known_yet()
    {
        var (client, agent) = DevFlow.Over();

        await client.CallAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(Path.Combine(_directory, "x.png"))!.ToJsonString()}}}""");

        Assert.Equal(["GET /api/v1/agent/status", "GET /api/v1/ui/tree", "GET /api/v1/ui/screenshot"], agent.Calls.Select(call => call.Route));
    }

    [Fact]
    public async Task A_screenshot_needs_a_path()
    {
        var (client, agent) = DevFlow.Over();

        Assert.Equal("INVALID_ARGS", (await client.FailureAsync(DeviceMethods.CaptureScreenshot, "{}")).Code);
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public async Task A_screenshot_that_is_not_a_png_fails()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.Script("GET", "/api/v1/ui/screenshot", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });

        var error = await client.FailureAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(Path.Combine(_directory, "x.png"))!.ToJsonString()}}}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task A_screenshot_the_agent_cannot_take_is_an_unsupported_capability()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure("GET", "/api/v1/ui/screenshot", HttpStatusCode.NotImplemented, "Not supported by this backend.", "not_supported");

        var error = await client.FailureAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(Path.Combine(_directory, "x.png"))!.ToJsonString()}}}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
    }
}
