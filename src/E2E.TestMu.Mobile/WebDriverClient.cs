// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.TestMu.Mobile;

/// <summary>The account a hub call is made for. Its <see cref="ToString"/> never prints the key.</summary>
internal sealed record HubCredentials(string User, string AccessKey)
{
    public override string ToString() => $"HubCredentials {{ User = {User}, AccessKey = ***** }}";
}

/// <summary>The hub answered with a WebDriver error, an HTTP error, or nothing at all. The key is never in the message.</summary>
internal sealed class WebDriverException(HttpStatusCode? status, string error, string message) : Exception(message)
{
    /// <summary>The HTTP status, or null when the hub never answered.</summary>
    public HttpStatusCode? Status { get; } = status;

    /// <summary>The W3C error code (<c>no such element</c>), or <c>unknown error</c>, <c>timeout</c> for a failure with none.</summary>
    public string Error { get; } = error;
}

/// <summary>
/// A W3C WebDriver session over HTTP: the part of the protocol the Appium client uses. The hub is Appium's, behind
/// TestMu's grid (<c>https://mobile-hub.lambdatest.com/wd/hub</c>). A command answers the response's <c>value</c> or
/// throws <see cref="WebDriverException"/>. Nothing retries: a retried tap could land twice.
/// </summary>
internal sealed class WebDriverClient
{
    /// <summary>How long one command may take. A page source on a cloud device can take seconds.</summary>
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly Uri _hub;
    private readonly HubCredentials? _credentials;

    internal WebDriverClient(HttpClient http, Uri hub, string sessionId, HubCredentials? credentials)
    {
        _http = http;
        _hub = WithTrailingSlash(hub);
        _credentials = credentials;
        SessionId = sessionId;
    }

    public string SessionId { get; }

    /// <summary>
    /// Starts a session. <paramref name="http"/> must have an infinite <see cref="HttpClient.Timeout"/>: the limits here
    /// are per call, and the default 100 seconds would end a queued create early. The call blocks while TestMu queues the request for a free device, so <paramref name="timeout"/>
    /// must cover the queue wait. <paramref name="capabilities"/> is the whole <c>capabilities</c> object.
    /// </summary>
    public static async Task<WebDriverClient> CreateSessionAsync(HttpClient http, Uri hub, HubCredentials credentials, JsonObject capabilities, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var url = new Uri(WithTrailingSlash(hub), "session");
        var reply = await SendAsync(http, HttpMethod.Post, url, credentials, new JsonObject { ["capabilities"] = capabilities.DeepClone() }, timeout, cancellationToken).ConfigureAwait(false);
        var id = Text(reply?["sessionId"]) ?? throw new WebDriverException(null, "session not created", "the hub answered without a session id");
        return new WebDriverClient(http, hub, id, credentials);
    }

    public Task<JsonNode?> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(_http, HttpMethod.Get, Url(path), _credentials, null, CommandTimeout, cancellationToken);

    public Task<JsonNode?> PostAsync(string path, JsonNode? body, CancellationToken cancellationToken) =>
        SendAsync(_http, HttpMethod.Post, Url(path), _credentials, body ?? new JsonObject(), CommandTimeout, cancellationToken);

    /// <summary>Runs a script (<c>execute/sync</c>), which is how Appium takes its <c>mobile: ...</c> commands.</summary>
    public Task<JsonNode?> ExecuteAsync(string script, JsonObject? arguments, CancellationToken cancellationToken) =>
        PostAsync("execute/sync", new JsonObject { ["script"] = script, ["args"] = arguments is null ? new JsonArray() : new JsonArray(arguments.DeepClone()) }, cancellationToken);

    /// <summary>
    /// Ends the session. A session the hub no longer knows (idle timeout, already deleted) is ended already, so that is
    /// not a failure.
    /// </summary>
    public async Task DeleteSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(_http, HttpMethod.Delete, Url(""), _credentials, null, CommandTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (WebDriverException failure) when (failure.Status == HttpStatusCode.NotFound || failure.Error == "invalid session id")
        {
            // Gone already.
        }
    }

    private Uri Url(string path) => new(_hub, path.Length == 0 ? $"session/{Uri.EscapeDataString(SessionId)}" : $"session/{Uri.EscapeDataString(SessionId)}/{path}");

    private static Uri WithTrailingSlash(Uri hub) => hub.AbsoluteUri.EndsWith('/') ? hub : new Uri(hub.AbsoluteUri + "/");

    private static async Task<JsonNode?> SendAsync(HttpClient http, HttpMethod method, Uri url, HubCredentials? credentials, JsonNode? body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, url);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (credentials is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.User}:{credentials.AccessKey}")));
        }

        if (body is not null)
        {
            message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        string text;
        HttpStatusCode status;
        try
        {
            using var response = await http.SendAsync(message, limit.Token).ConfigureAwait(false);
            status = response.StatusCode;
            text = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WebDriverException(null, "timeout", $"the hub did not answer {method} {url.AbsolutePath} within {timeout.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException cause)
        {
            throw new WebDriverException(null, "unknown error", $"the hub could not be reached: {Redact(cause.Message, credentials)}");
        }

        var root = Parse(text);
        var value = root?["value"];
        if ((int)status >= 400)
        {
            // A bare 404 on a session URL is a hub that no longer has the session, not an unknown command.
            var code = Text(value?["error"]) ?? (status == HttpStatusCode.NotFound ? "invalid session id" : "unknown error");
            var detail = Text(value?["message"]) ?? Text(root?["message"]) ?? Short(text);
            throw new WebDriverException(status, code, Redact($"HTTP {(int)status}: {detail}", credentials));
        }

        // W3C answers {"value": ...}; session creation {"value": {"sessionId": ...}}. The older protocol put sessionId beside a null value.
        return value is null && root?["sessionId"] is { } legacy ? new JsonObject { ["sessionId"] = legacy.DeepClone() } : value;
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return text.Length == 0 ? null : JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Short(string text)
    {
        var line = text.Trim();
        return line.Length > 200 ? line[..200] + "..." : line;
    }

    private static string Redact(string text, HubCredentials? credentials) =>
        credentials is { AccessKey.Length: > 0 } ? text.Replace(credentials.AccessKey, "*****", StringComparison.Ordinal) : text;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
