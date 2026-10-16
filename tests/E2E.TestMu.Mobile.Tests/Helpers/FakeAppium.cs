// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.TestMu.Mobile.Tests;

/// <summary>One request the fake hub saw. <see cref="Command"/> is the path under the session: <c>actions</c>, <c>element/el-1/value</c>, or empty for the session itself.</summary>
internal sealed record AppiumCall(HttpMethod Method, string Path, JsonNode? Body, string? Authorization)
{
    public string Command => Path.Contains("/session/sess-1/", StringComparison.Ordinal)
        ? Path[(Path.IndexOf("/session/sess-1/", StringComparison.Ordinal) + "/session/sess-1/".Length)..]
        : "";

    /// <summary>The script of an <c>execute/sync</c> call, else null.</summary>
    public string? Script => Command == "execute/sync" ? Body?["script"]?.GetValue<string>() : null;

    /// <summary>The first argument object of an <c>execute/sync</c> call.</summary>
    public JsonNode? Arguments => Body?["args"]?[0];
}

/// <summary>
/// A fake Appium hub behind an <see cref="HttpClient"/>: it records every request and answers a plausible device (a
/// 390 x 844 window, a 2x screenshot, a focused field), else what a test scripts for a command.
/// </summary>
internal sealed class FakeAppium : HttpMessageHandler
{
    public const string SessionId = "sess-1";

    public const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

    public static readonly Uri Hub = new("https://hub.example/wd/hub");

    private readonly List<(string Method, string Command, Func<AppiumCall, HttpResponseMessage> Respond)> _scripted = [];

    public List<AppiumCall> Calls { get; } = [];

    public HttpClient Http => new(this);

    /// <summary>A hub-side client of the fake session, as a worker builds it.</summary>
    public WebDriverClient Driver(HubCredentials? credentials = null) => new(Http, Hub, SessionId, credentials);

    /// <summary>Answers <paramref name="command"/> of this method with this response from now on.</summary>
    public void Script(string method, string command, Func<AppiumCall, HttpResponseMessage> respond) => _scripted.Insert(0, (method, command, respond));

    public void ScriptValue(string method, string command, JsonNode? value) => Script(method, command, _ => Value(value));

    public static HttpResponseMessage Value(JsonNode? value) =>
        new(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["value"] = value?.DeepClone() }.ToJsonString(), Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Failure(HttpStatusCode status, string error, string message) =>
        new(status) { Content = new StringContent(new JsonObject { ["value"] = new JsonObject { ["error"] = error, ["message"] = message } }.ToJsonString(), Encoding.UTF8, "application/json") };

    /// <summary>A PNG header for a screenshot of this size: all the client reads.</summary>
    public static string PngBase64(int width, int height)
    {
        byte[] header = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', .. Big(width), .. Big(height)];
        return Convert.ToBase64String(header);
    }

    public IEnumerable<AppiumCall> Sent(string command) => Calls.Where(call => call.Command == command);

    public IEnumerable<AppiumCall> Scripts() => Calls.Where(call => call.Script is not null);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var text = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var call = new AppiumCall(request.Method, request.RequestUri!.AbsolutePath, text is null ? null : JsonNode.Parse(text), request.Headers.Authorization?.ToString());
        Calls.Add(call);
        foreach (var (method, command, respond) in _scripted)
        {
            if (method == call.Method.Method && (command == call.Command || (command.Length == 0 && call.Command.Length == 0)))
            {
                return respond(call);
            }
        }

        return Default(call);
    }

    private static HttpResponseMessage Default(AppiumCall call)
    {
        if (call.Method == HttpMethod.Post && call.Path.EndsWith("/wd/hub/session", StringComparison.Ordinal))
        {
            return Value(new JsonObject { ["sessionId"] = SessionId, ["capabilities"] = new JsonObject() });
        }

        return (call.Method.Method, call.Command) switch
        {
            ("GET", "window/rect") => Value(new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = 390, ["height"] = 844 }),
            ("GET", "screenshot") => Value(PngBase64(780, 1688)),
            ("GET", "element/active") => Value(new JsonObject { [ElementKey] = "el-1" }),
            ("GET", "source") => Value("<AppiumAUT/>"),
            _ => Value(null),
        };
    }

    private static byte[] Big(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}

internal static class Json
{
    /// <summary>Whether a node equals the JSON written out, ignoring whitespace.</summary>
    public static void AssertEqual(string expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), $"expected {expected}{Environment.NewLine}actual   {actual?.ToJsonString()}");
}
