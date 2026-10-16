// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests;

/// <summary>A converter that answers a fixed snapshot, whatever the page source says.</summary>
internal sealed class StubConverter(JsonObject snapshot) : IPageSourceConverter
{
    public List<string> Sources { get; } = [];

    public JsonObject Snapshot { get; set; } = snapshot;

    public JsonObject Convert(string pageSource, MobilePlatform platform)
    {
        Sources.Add(pageSource);
        return (JsonObject)Snapshot.DeepClone();
    }
}

internal static class Appium
{
    /// <summary>An iOS screen: a navigation bar with a back button, a button at (125, 220), and a text field at (120, 320).</summary>
    public static JsonObject Screen() => (JsonObject)JsonNode.Parse(
        """
        {
          "nodes": [
            { "ref": "e1", "index": 0, "type": "NavigationBar", "kind": "navigation-bar", "rect": { "x": 0, "y": 44, "width": 390, "height": 44 } },
            { "ref": "e2", "index": 1, "parentIndex": 0, "type": "Button", "kind": "button", "label": "Back", "rect": { "x": 10, "y": 50, "width": 60, "height": 40 } },
            { "ref": "e3", "index": 2, "type": "Button", "kind": "button", "label": "Save", "rect": { "x": 100, "y": 200, "width": 50, "height": 40 } },
            { "ref": "e4", "index": 3, "type": "TextField", "kind": "text-field", "rect": { "x": 20, "y": 300, "width": 200, "height": 40 } }
          ]
        }
        """)!;

    public static (AppiumDeviceClient Client, FakeAppium Hub, StubConverter Converter) Over(MobilePlatform platform = MobilePlatform.Ios, JsonObject? screen = null)
    {
        var hub = new FakeAppium();
        var converter = new StubConverter(screen ?? Screen());
        return (new AppiumDeviceClient(hub.Driver(), platform, converter), hub, converter);
    }

    /// <summary>A client that has read the screen, so its refs are live.</summary>
    public static async Task<(AppiumDeviceClient Client, FakeAppium Hub, StubConverter Converter)> ReadAsync(MobilePlatform platform = MobilePlatform.Ios, JsonObject? screen = null)
    {
        var over = Over(platform, screen);
        await over.Client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), CancellationToken.None);
        over.Hub.Calls.Clear();
        return over;
    }

    public static Task<JsonNode?> CallAsync(this AppiumDeviceClient client, string method, string? options = null) =>
        client.CallAsync(method, options is null ? null : (JsonObject)JsonNode.Parse(options)!, CancellationToken.None);

    /// <summary>The error a call fails with, as the engine reads it.</summary>
    public static async Task<AgentDeviceError> FailureAsync(this AppiumDeviceClient client, string method, string? options = null) =>
        await Assert.ThrowsAsync<AgentDeviceError>(() => client.CallAsync(method, options));

    /// <summary>What the engine makes of an error: the code a test sees.</summary>
    public static string EngineCode(AgentDeviceError error) => Assert.IsType<EngineException>(DeviceErrors.TranslateError(error, "operation")).Code;
}
