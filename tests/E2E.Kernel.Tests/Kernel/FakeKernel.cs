// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Kernel.Tests.Kernel;

/// <summary>One request the fake Kernel saw.</summary>
internal sealed record KernelCall(string Route, string Id, Uri Uri, string? Authorization, JsonNode? Body);

/// <summary>
/// A fake Kernel REST API behind an <see cref="HttpClient"/>, the stand-in for upstream's mocked <c>@onkernel/sdk</c>: it records
/// every request and answers what a test scripts, else a working browser. A route is <c>create</c>, <c>delete</c>, <c>list</c>,
/// <c>startReplay</c>, <c>stopReplay</c>, <c>downloadReplay</c>, or <c>readFile</c>.
/// </summary>
internal sealed class FakeKernel : HttpMessageHandler
{
    private int _counter;

    public List<KernelCall> Calls { get; } = [];

    /// <summary>The session ids <c>list</c> answers with.</summary>
    public List<string> Active { get; } = [];

    /// <summary>Answers a request to <c>create</c>; the default creates the next browser.</summary>
    public Func<HttpResponseMessage>? Create { get; set; }

    /// <summary>Answers a request to delete this id; the default deletes it.</summary>
    public Func<string, HttpResponseMessage>? Delete { get; set; }

    /// <summary>Answers a request to <c>list</c> with the request's URI; the default lists <see cref="Active"/>.</summary>
    public Func<Uri, HttpResponseMessage>? List { get; set; }

    /// <summary>How many downloads of a replay fail before one succeeds.</summary>
    public int DownloadFailures { get; set; }

    public HttpClient Client => new(this);

    public IEnumerable<KernelCall> Of(string route) => Calls.Where(call => call.Route == route);

    public IReadOnlyList<string> Deleted => [.. Of("delete").Select(call => call.Id)];

    public static HttpResponseMessage Json(JsonNode body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Text(HttpStatusCode status, string text) => new(status) { Content = new StringContent(text, Encoding.UTF8, "text/plain") };

    public HttpResponseMessage NextBrowser()
    {
        var n = Interlocked.Increment(ref _counter);
        return Json(new JsonObject { ["session_id"] = $"b{n}", ["cdp_ws_url"] = $"wss://kernel/b{n}", ["browser_live_view_url"] = $"https://view/b{n}" }, HttpStatusCode.Created);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var (route, id) = (request.Method.Method, segments.Length, segments.ElementAtOrDefault(2)) switch
        {
            ("POST", 1, _) => ("create", ""),
            ("GET", 1, _) => ("list", ""),
            ("DELETE", 2, _) => ("delete", segments[1]),
            ("POST", 3, "replays") => ("startReplay", segments[1]),
            ("POST", 5, _) => ("stopReplay", segments[1]),
            ("GET", 4, "replays") => ("downloadReplay", segments[1]),
            ("GET", 4, "fs") => ("readFile", segments[1]),
            _ => throw new InvalidOperationException($"unexpected request {request.Method} {request.RequestUri}"),
        };
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        lock (Calls)
        {
            Calls.Add(new KernelCall(route, id, request.RequestUri, request.Headers.Authorization?.ToString(), body));
        }

        return route switch
        {
            "create" => Create?.Invoke() ?? NextBrowser(),
            "delete" => Delete?.Invoke(id) ?? new HttpResponseMessage(HttpStatusCode.NoContent),
            "list" when List is not null => List(request.RequestUri),
            "list" => Json(new JsonArray([.. Active.Select(active => (JsonNode)new JsonObject { ["session_id"] = active })])),
            "startReplay" => Json(new JsonObject { ["replay_id"] = "r1" }),
            "stopReplay" => new HttpResponseMessage(HttpStatusCode.OK),
            "downloadReplay" when DownloadFailures > 0 => Fail(),
            "downloadReplay" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("mp4 bytes", Encoding.UTF8, "video/mp4") },
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("file bytes") },
        };
    }

    private HttpResponseMessage Fail()
    {
        DownloadFailures--;
        return Text(HttpStatusCode.ServiceUnavailable, "replay still processing");
    }
}
