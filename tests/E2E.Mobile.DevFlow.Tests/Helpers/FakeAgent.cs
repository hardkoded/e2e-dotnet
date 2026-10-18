// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow.Tests;

/// <summary>One request the fake agent saw. <see cref="Route"/> is the method and the path under the host: <c>POST /api/v1/ui/actions/tap</c>.</summary>
internal sealed record AgentCall(HttpMethod Method, Uri Uri, JsonNode? Body, IReadOnlyDictionary<string, string> Headers)
{
    public string Route => $"{Method} {Uri.AbsolutePath}";

    public string? Header(string name) => Headers.GetValueOrDefault(name);

    /// <summary>One query value, or null.</summary>
    public string? Query(string name)
    {
        foreach (var pair in Uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0] == name)
            {
                return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }
        }

        return null;
    }
}

/// <summary>
/// A fake DevFlow agent (and broker) behind an <see cref="HttpClient"/>: it records every request and answers a
/// plausible MAUI app (an iOS app with a 390 x 844 window, the sample's greeting screen, a 2x screenshot), else what a
/// test scripts for a route. The broker on port 19223 answers nothing unless scripted, as when none runs.
/// </summary>
internal sealed class FakeAgent : HttpMessageHandler
{
    public const string PackageId = "dev.hardkoded.e2e.mobileapp";

    public const int CaptureEpoch = 7;

    public static readonly Uri Agent = new("http://localhost:9223/");

    private readonly List<(string Method, string PathPrefix, int? Port, Func<AgentCall, HttpResponseMessage> Respond)> _scripted = [];

    /// <summary>Every request but the lease control ones, which <see cref="LeaseCalls"/> keeps: a mutation claims the lease first, and that would clutter every list.</summary>
    public List<AgentCall> Calls { get; } = [];

    /// <summary>The <c>POST /api/v1/agent/lease</c> requests, in order.</summary>
    public List<AgentCall> LeaseCalls { get; } = [];

    public HttpClient Http => new(this) { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Answers requests of this method whose path starts with <paramref name="pathPrefix"/> (on <paramref name="port"/>, when given) from now on.</summary>
    public void Script(string method, string pathPrefix, Func<AgentCall, HttpResponseMessage> respond, int? port = null) => _scripted.Insert(0, (method, pathPrefix, port, respond));

    public void ScriptJson(string method, string pathPrefix, JsonNode? value, HttpStatusCode status = HttpStatusCode.OK, int? port = null) =>
        Script(method, pathPrefix, _ => Json(value, status), port);

    /// <summary>Answers as the MAUI agent refuses an action: <c>{ success: false, error, reason, details }</c>.</summary>
    public void ScriptFailure(string method, string pathPrefix, HttpStatusCode status, string error, string? reason = null, JsonObject? details = null) =>
        Script(method, pathPrefix, _ => Failure(status, error, reason, details));

    /// <summary>Answers a lease claim as the agent does when another host holds the lease: 200, and the holder in the body.</summary>
    public void ScriptLeaseHeldBy(string holderKind, string? label) =>
        Script("POST", "/api/v1/agent/lease", call => Json(LeaseStatus(call, youHold: false, holderKind, label)));

    /// <summary>Answers as a host with nothing listening: the connection is refused.</summary>
    public void ScriptRefused(string method, string pathPrefix, int? port = null) =>
        Script(method, pathPrefix, _ => throw new HttpRequestException("Connection refused"), port);

    public static HttpResponseMessage Json(JsonNode? value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(value?.ToJsonString() ?? "", Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Failure(HttpStatusCode status, string error, string? reason = null, JsonObject? details = null)
    {
        var body = new JsonObject { ["success"] = false, ["error"] = error };
        if (reason is not null)
        {
            body["reason"] = reason;
        }

        if (details is not null)
        {
            body["details"] = details;
        }

        return Json(body, status);
    }

    /// <summary>The agent's answer to a lease action: who holds the lease, whatever the caller asked.</summary>
    public static JsonObject LeaseStatus(AgentCall call, bool youHold, string? holderKind = "e2e", string? label = null) => new()
    {
        ["ok"] = true,
        ["allowed"] = youHold,
        ["youHold"] = youHold,
        ["heldByOther"] = !youHold,
        ["leaseId"] = youHold ? call.Body?["leaseId"]?.DeepClone() : null,
        ["holderKind"] = holderKind,
        ["label"] = label ?? (youHold ? call.Body?["label"]?.DeepClone() : null),
        ["expiresInMs"] = 10000,
    };

    /// <summary>A PNG header for a screenshot of this size: all the client reads.</summary>
    public static byte[] Png(int width, int height) =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', .. Big(width), .. Big(height)];

    /// <summary>The status the agent writes: the platform sits under <c>device</c>, not at the top as the spec draws it.</summary>
    public static JsonObject Status(string platform = "ios", string? packageId = PackageId, string name = "MobileApp") => new()
    {
        ["agent"] = new JsonObject { ["name"] = "Microsoft.Maui.DevFlow.Agent", ["version"] = "0.1.0", ["framework"] = "MAUI", ["frameworkId"] = "maui" },
        ["device"] = new JsonObject { ["platform"] = platform, ["deviceType"] = "simulator", ["idiom"] = "phone", ["displayDensity"] = 2, ["windowCount"] = 1, ["windowWidth"] = 390, ["windowHeight"] = 844 },
        ["app"] = new JsonObject { ["name"] = name, ["version"] = "1.0", ["packageId"] = packageId, ["processId"] = 4242 },
        ["running"] = true,
    };

    /// <summary>The hit test's answer: the element under the point, topmost first.</summary>
    public static JsonObject HitTest(AgentCall call) => new()
    {
        ["x"] = double.Parse(call.Query("x") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
        ["y"] = double.Parse(call.Query("y") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
        ["window"] = 0,
        ["captureEpoch"] = CaptureEpoch,
        ["elements"] = new JsonArray(
            new JsonObject { ["id"] = "btn_greet", ["type"] = "Button", ["text"] = "Greet", ["bounds"] = new JsonObject { ["x"] = 24, ["y"] = 261, ["width"] = 342, ["height"] = 44 } },
            new JsonObject { ["id"] = "stack_0", ["type"] = "VerticalStackLayout", ["bounds"] = new JsonObject { ["x"] = 0, ["y"] = 59, ["width"] = 390, ["height"] = 600 } }),
    };

    /// <summary>
    /// The sample's greeting screen: a window, a page, a scroll view, a stack with a heading, a subtitle, the name field
    /// (focused), the Greet button, a hidden error label, the counter, the Add one button, plus a tappable label, a
    /// switch that is on, and a password field with text in it.
    /// </summary>
    public static JsonArray Tree(bool nameFocused = true) => (JsonArray)JsonNode.Parse(
        $$"""
        [
          {
            "id": "win_0", "parentId": null, "type": "Window", "fullType": "Microsoft.Maui.Controls.Window", "framework": "maui", "captureEpoch": {{CaptureEpoch}},
            "text": "MobileApp", "role": "window", "traits": ["focusable"],
            "state": { "displayed": true, "enabled": true, "selected": false, "focused": true, "opacity": 1.0 },
            "bounds": { "x": 0, "y": 0, "width": 390, "height": 844, "coordinate": "screen" },
            "children": [
              {
                "id": "page_main", "parentId": "win_0", "type": "ContentPage", "fullType": "Microsoft.Maui.Controls.ContentPage", "framework": "maui", "captureEpoch": {{CaptureEpoch}},
                "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 },
                "bounds": { "x": 0, "y": 0, "width": 390, "height": 844 },
                "children": [
                  {
                    "id": "scroll_0", "parentId": "page_main", "type": "ScrollView", "fullType": "Microsoft.Maui.Controls.ScrollView", "framework": "maui", "captureEpoch": {{CaptureEpoch}},
                    "traits": ["scrollable"],
                    "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 },
                    "bounds": { "x": 0, "y": 59, "width": 390, "height": 751 },
                    "children": [
                      {
                        "id": "stack_0", "parentId": "scroll_0", "type": "VerticalStackLayout", "fullType": "Microsoft.Maui.Controls.VerticalStackLayout", "framework": "maui", "captureEpoch": {{CaptureEpoch}},
                        "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 },
                        "bounds": { "x": 0, "y": 59, "width": 390, "height": 600 },
                        "children": [
                          { "id": "lbl_title", "parentId": "stack_0", "type": "Label", "fullType": "Microsoft.Maui.Controls.Label", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "text": "Say hello", "role": "heading", "traits": ["header"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 107, "width": 342, "height": 40 } },
                          { "id": "lbl_sub", "parentId": "stack_0", "type": "Label", "fullType": "Microsoft.Maui.Controls.Label", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "text": "Type a name and the app greets you.",
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 163, "width": 342, "height": 22 } },
                          { "id": "entry_name", "parentId": "stack_0", "type": "Entry", "fullType": "Microsoft.Maui.Controls.Entry", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "name", "text": "", "role": "textbox", "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": {{(nameFocused ? "true" : "false")}}, "opacity": 1.0 }, "bounds": { "x": 24, "y": 201, "width": 342, "height": 44 },
                            "nativeView": { "type": "UIKit.UITextField", "properties": { "placeholder": "Ada Lovelace" } }, "frameworkProperties": { "IsReadOnly": "False" } },
                          { "id": "btn_greet", "parentId": "stack_0", "type": "Button", "fullType": "Microsoft.Maui.Controls.Button", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "greet", "text": "Greet", "role": "button", "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 261, "width": 342, "height": 44 } },
                          { "id": "lbl_error", "parentId": "stack_0", "type": "Label", "fullType": "Microsoft.Maui.Controls.Label", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "error", "text": "Enter a name first.",
                            "state": { "displayed": false, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 321, "width": 342, "height": 0 } },
                          { "id": "lbl_counter", "parentId": "stack_0", "type": "Label", "fullType": "Microsoft.Maui.Controls.Label", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "counter", "text": "Count: 0",
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 338, "width": 342, "height": 22 } },
                          { "id": "btn_increment", "parentId": "stack_0", "type": "Button", "fullType": "Microsoft.Maui.Controls.Button", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "increment", "text": "Add one", "role": "button", "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 376, "width": 342, "height": 44 } },
                          { "id": "lbl_link", "parentId": "stack_0", "type": "Label", "fullType": "Microsoft.Maui.Controls.Label", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "text": "Terms", "role": "link", "gestures": ["tap"], "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 436, "width": 60, "height": 22 } },
                          { "id": "sw_dark", "parentId": "stack_0", "type": "Switch", "fullType": "Microsoft.Maui.Controls.Switch", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "dark", "value": "True", "role": "switch", "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": false, "selected": true, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 474, "width": 51, "height": 31 } },
                          { "id": "entry_secret", "parentId": "stack_0", "type": "Entry", "fullType": "Microsoft.Maui.Controls.Entry", "framework": "maui", "captureEpoch": {{CaptureEpoch}}, "automationId": "secret", "text": "[REDACTED]", "role": "textbox", "traits": ["interactive", "focusable"],
                            "state": { "displayed": true, "enabled": true, "selected": false, "focused": false, "opacity": 1.0 }, "bounds": { "x": 24, "y": 521, "width": 342, "height": 44 } }
                        ]
                      }
                    ]
                  }
                ]
              }
            ]
          }
        ]
        """)!;

    public static JsonObject Envelope(JsonArray? elements = null, string revision = "r1") => new() { ["revision"] = revision, ["elements"] = elements ?? Tree() };

    public IEnumerable<AgentCall> Sent(string route) => Calls.Where(call => call.Route == route);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var text = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value), StringComparer.OrdinalIgnoreCase);
        var call = new AgentCall(request.Method, request.RequestUri!, text is null or "" ? null : JsonNode.Parse(text), headers);
        (call.Uri.AbsolutePath == "/api/v1/agent/lease" ? LeaseCalls : Calls).Add(call);
        foreach (var (method, prefix, port, respond) in _scripted)
        {
            if (method == call.Method.Method && call.Uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) && (port is null || port == call.Uri.Port))
            {
                return respond(call);
            }
        }

        return Default(call);
    }

    private static HttpResponseMessage Default(AgentCall call)
    {
        if (call.Uri.Port == DevFlowAgents.DefaultBrokerPort)
        {
            throw new HttpRequestException("Connection refused");
        }

        return (call.Method.Method, call.Uri.AbsolutePath) switch
        {
            ("GET", "/api/v1/agent/status") => Json(Status()),
            ("GET", "/api/v1/ui/tree") => Json(Envelope()),
            ("GET", "/api/v1/ui/screenshot") => PngResponse(Png(780, 1688)),
            ("GET", "/api/v1/ui/hit-test") => Json(HitTest(call)),
            ("POST", "/api/v1/agent/lease") => Json(LeaseStatus(call, youHold: true)),
            ("PUT", "/api/v1/device/app/theme") => Json(new JsonObject { ["theme"] = call.Body?["theme"]?.DeepClone(), ["success"] = true }),
            ("POST", var path) when path.StartsWith("/api/v1/ui/actions/", StringComparison.Ordinal) => Json(new JsonObject { ["success"] = true }),
            _ => Failure(HttpStatusCode.NotFound, "Not found"),
        };
    }

    private static HttpResponseMessage PngResponse(byte[] png)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        response.Headers.Add("X-DevFlow-Capture-Epoch", CaptureEpoch.ToString());
        return response;
    }

    private static byte[] Big(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}

internal static class Json
{
    /// <summary>Whether a node equals the JSON written out, ignoring whitespace.</summary>
    public static void AssertEqual(string expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), $"expected {expected}{Environment.NewLine}actual   {actual?.ToJsonString()}");
}
