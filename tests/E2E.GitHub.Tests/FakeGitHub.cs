// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;

namespace E2E.GitHub.Tests;

/// <summary>One request the fake saw.</summary>
internal sealed record Call(string Url, string Method, IReadOnlyDictionary<string, string> Headers, JsonElement? Body)
{
    public string Path => new Uri(Url).AbsolutePath;

    /// <summary>Upstream's <c>${call.method} ${new URL(call.url).pathname}</c>.</summary>
    public string Route => $"{Method} {Path}";

    /// <summary>The <c>body</c> field of the JSON body the call sent.</summary>
    public string BodyText => Body?.GetProperty("body").GetString() ?? "";
}

/// <summary>
/// Upstream's <c>fake-github.ts</c>: a GitHub REST API that records calls and answers from a script, and the Actions environment a job sees. A
/// request is answered by the script's <c>METHOD /path?query</c>, then <c>METHOD /path</c>, then the defaults, then <c>METHOD *</c>.
/// </summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    /// <summary>What GitHub answers <c>GET /user</c> with for an installation token such as <c>GITHUB_TOKEN</c>, unless the script names that route.</summary>
    private static readonly Dictionary<string, Func<Call, HttpResponseMessage>> Defaults = new()
    {
        ["GET /user"] = _ => Json(HttpStatusCode.Forbidden, new { message = "Resource not accessible by integration" }),
    };

    private readonly IReadOnlyDictionary<string, Func<Call, HttpResponseMessage>> _script;

    public FakeGitHub(IReadOnlyDictionary<string, Func<Call, HttpResponseMessage>> script) => _script = script;

    public List<Call> Calls { get; } = [];

    public HttpClient Client => new(this, disposeHandler: false);

    public static HttpResponseMessage Json(HttpStatusCode status, object value) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Json(int status, object value) => Json((HttpStatusCode)status, value);

    public static HttpResponseMessage Text(int status, string text, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(text) };
        foreach (var (name, value) in headers)
        {
            response.Headers.Add(name, value);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(header => header.Key.ToLowerInvariant(), header => string.Join(", ", header.Value));
        var text = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var call = new Call(request.RequestUri!.ToString(), request.Method.Method, headers, text is null ? null : JsonDocument.Parse(text).RootElement.Clone());
        Calls.Add(call);
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.RequestUri;
        var answer = Find($"{call.Method} {uri.AbsolutePath}{uri.Query}")
            ?? Find($"{call.Method} {uri.AbsolutePath}")
            ?? (Defaults.TryGetValue($"{call.Method} {uri.AbsolutePath}", out var fallback) ? fallback : null)
            ?? Find($"{call.Method} *")
            ?? throw new InvalidOperationException($"unexpected request {call.Method} {uri.AbsolutePath}{uri.Query}");
        return answer(call);
    }

    private Func<Call, HttpResponseMessage>? Find(string key) => _script.TryGetValue(key, out var answer) ? answer : null;

    /// <summary>What GitHub Actions sets for a <c>pull_request</c> job, event payload at <c>/event.json</c>.</summary>
    public static Dictionary<string, string> ActionsEnv() => new()
    {
        ["GITHUB_ACTIONS"] = "true",
        ["GITHUB_REPOSITORY"] = "octo/app",
        ["GITHUB_SHA"] = "merge-sha",
        ["GITHUB_RUN_ID"] = "99",
        ["GITHUB_EVENT_NAME"] = "pull_request",
        ["GITHUB_EVENT_PATH"] = "/event.json",
        ["GITHUB_WORKFLOW"] = "e2e",
        ["GITHUB_JOB"] = "test",
        ["GITHUB_STEP_SUMMARY"] = "/summary.md",
    };

    /// <summary>Event payloads by path, the way <c>ReadFile</c> hands them to <c>DetectActionsAsync</c>.</summary>
    private static readonly Dictionary<string, string> Events = new()
    {
        ["/event.json"] = """{"pull_request":{"number":41,"head":{"sha":"head-sha"}}}""",
        ["/event/comment.json"] = """{"issue":{"number":7,"pull_request":{"url":"x"}}}""",
        ["/event/issue.json"] = """{"issue":{"number":8}}""",
        ["/event/push.json"] = """{"ref":"refs/heads/main"}""",
        ["/event/broken.json"] = "{not json",
    };

    public static Task<string> ReadEvent(string file, CancellationToken cancellationToken) =>
        Events.TryGetValue(file, out var text) ? Task.FromResult(text) : throw new FileNotFoundException($"ENOENT {file}");

    /// <summary>An environment reader over a dictionary; unset names read as null.</summary>
    public static Func<string, string?> Env(IReadOnlyDictionary<string, string> values) => name => values.GetValueOrDefault(name);
}
