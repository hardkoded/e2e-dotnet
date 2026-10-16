// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Client;

public sealed class CaptureTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-testmu-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Writes_the_screenshot_and_reports_its_size_against_the_window()
    {
        var (client, hub, _) = Appium.Over();
        var path = Path.Combine(_directory, "shot.png");

        var result = await client.CallAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(path)!.ToJsonString()}},"normalizeStatusBar":true}""");

        Assert.Equal(["GET screenshot", "GET window/rect"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal(Convert.FromBase64String(FakeAppium.PngBase64(780, 1688)), await File.ReadAllBytesAsync(path));
        Json.AssertEqual($$"""{"path":{{JsonValue.Create(path)!.ToJsonString()}},"logicalWidth":390,"logicalHeight":844,"width":780,"height":1688,"pixelDensity":2}""", result);
    }

    [Fact]
    public async Task A_screenshot_needs_a_path()
    {
        var (client, hub, _) = Appium.Over();

        Assert.Equal("INVALID_ARGS", (await client.FailureAsync(DeviceMethods.CaptureScreenshot, "{}")).Code);
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task A_screenshot_that_is_not_base64_fails()
    {
        var (client, hub, _) = Appium.Over();
        hub.ScriptValue("GET", "screenshot", "not base64!");

        var error = await client.FailureAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(Path.Combine(_directory, "x.png"))!.ToJsonString()}}}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
    }

    [Fact]
    public async Task A_window_with_no_size_fails_a_screenshot()
    {
        var (client, hub, _) = Appium.Over();
        hub.ScriptValue("GET", "window/rect", new JsonObject());

        var error = await client.FailureAsync(DeviceMethods.CaptureScreenshot, $$"""{"path":{{JsonValue.Create(Path.Combine(_directory, "x.png"))!.ToJsonString()}}}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
    }

    [Fact]
    public async Task A_snapshot_is_the_converted_page_source()
    {
        var (client, hub, converter) = Appium.Over();
        hub.ScriptValue("GET", "source", "<XCUIElementTypeApplication/>");

        var snapshot = await client.CallAsync(DeviceMethods.CaptureSnapshot, """{"interactiveOnly":false}""");

        Assert.Equal(["<XCUIElementTypeApplication/>"], converter.Sources);
        Assert.True(JsonNode.DeepEquals(Appium.Screen(), snapshot));
    }

    [Fact]
    public async Task A_snapshot_without_a_converter_is_not_implemented_and_never_reads_the_page()
    {
        var hub = new FakeAppium();
        var client = new AppiumDeviceClient(hub.Driver(), MobilePlatform.Ios);

        var error = await client.FailureAsync(DeviceMethods.CaptureSnapshot);

        Assert.Equal("NOT_IMPLEMENTED", error.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", Appium.EngineCode(error));
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task A_ref_of_an_older_snapshot_is_stale_once_a_newer_one_drops_it()
    {
        var (client, hub, converter) = await Appium.ReadAsync();
        converter.Snapshot = (JsonObject)JsonNode.Parse("""{"nodes":[{"ref":"e1","rect":{"x":0,"y":0,"width":10,"height":10}}]}""")!;
        await client.CallAsync(DeviceMethods.CaptureSnapshot);
        hub.Calls.Clear();

        var error = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@e3"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Equal("ref_not_found", error.Details!["reason"]);
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task A_ref_before_any_snapshot_is_stale_and_so_is_one_with_no_bounds()
    {
        var (client, _, _) = Appium.Over(screen: (JsonObject)JsonNode.Parse("""{"nodes":[{"ref":"e1"}]}""")!);
        Assert.Equal("ref_not_found", (await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@e1"}""")).Details!["reason"]);

        await client.CallAsync(DeviceMethods.CaptureSnapshot);
        Assert.Equal("ref_not_found", (await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@e1"}""")).Details!["reason"]);
    }
}
