// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Kernel;

/// <summary>Kernel's browser profile to load into a new browser (<c>profile</c> of the create-browser body).</summary>
public sealed record KernelBrowserProfile
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public bool? SaveChanges { get; init; }
}

/// <summary>Kernel's screen size (<c>viewport</c> of the create-browser body).</summary>
public sealed record KernelBrowserViewport
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public int? RefreshRate { get; init; }
}

/// <summary>
/// Kernel's create-browser body, its tags read-only (upstream <c>KernelBrowserParams</c>). Upstream types it as the SDK's
/// <c>BrowserCreateParams</c>; the port has no SDK, so the fields the docs name are properties and any other field of the body
/// goes in <see cref="Extra"/>. Names are the body's fields in C# casing: <see cref="TimeoutSeconds"/> is <c>timeout_seconds</c>.
/// </summary>
public record KernelBrowserParams
{
    public bool? Stealth { get; init; }

    public bool? Headless { get; init; }

    public bool? Gpu { get; init; }

    public int? TimeoutSeconds { get; init; }

    public KernelBrowserProfile? Profile { get; init; }

    public string? ProxyId { get; init; }

    public KernelBrowserViewport? Viewport { get; init; }

    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Any other field of Kernel's create-browser body (<c>proxy</c>, <c>extensions</c>, <c>region</c>, ...), by its wire name. It wins over the properties above.</summary>
    public JsonObject? Extra { get; init; }
}

/// <summary>Kernel's start-replay body (upstream <c>KernelReplayParams</c>): <c>framerate</c>, <c>max_duration_in_seconds</c>, <c>record_audio</c>.</summary>
public sealed record KernelReplayParams
{
    public int? Framerate { get; init; }

    public int? MaxDurationInSeconds { get; init; }

    public bool? RecordAudio { get; init; }
}

/// <summary>A browser Kernel created.</summary>
internal sealed record KernelBrowser(string SessionId, string CdpWsUrl, string? LiveViewUrl);

/// <summary>Kernel answered with an error status. The message is Kernel's own, trimmed (<c>401 Invalid or disabled API key</c>).</summary>
internal sealed class KernelApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>The slice of Kernel the provider uses (upstream <c>KernelBrowsers</c>).</summary>
internal interface IKernelBrowsers
{
    Task<KernelBrowser> CreateAsync(KernelBrowserParams parameters, CancellationToken cancellationToken);

    /// <summary>Deletes the browser; one Kernel no longer knows counts as deleted. Resolves to whether Kernel still knew it.</summary>
    Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>The session ids of the active browsers carrying every one of <paramref name="tags"/>.</summary>
    Task<IReadOnlyList<string>> ListActiveAsync(IReadOnlyDictionary<string, string> tags, CancellationToken cancellationToken);

    /// <summary>Starts recording the browser's screen; resolves to the replay id.</summary>
    Task<string> StartReplayAsync(string sessionId, KernelReplayParams parameters, CancellationToken cancellationToken);

    Task StopReplayAsync(string sessionId, string replayId, CancellationToken cancellationToken);

    /// <summary>Downloads a stopped recording's MP4 to <paramref name="file"/>.</summary>
    Task DownloadReplayAsync(string sessionId, string replayId, string file, CancellationToken cancellationToken);

    /// <summary>The bytes of one file on the browser's own disk.</summary>
    Task<byte[]> ReadFileAsync(string sessionId, string file, CancellationToken cancellationToken);
}

/// <summary>
/// Kernel browsers for one API key (upstream <c>kernelBrowsers</c>), over Kernel's REST API with <see cref="HttpClient"/> in place
/// of <c>@onkernel/sdk</c>, the API key as a bearer token. The SDK retries a failed request twice; this client retries nothing,
/// since a retried create after an ambiguous answer could allocate a second browser. The endpoints and fields were read from the
/// SDK's source, not checked against a live Kernel.
/// </summary>
internal sealed class KernelClient(HttpClient http, string apiKey, Uri baseUrl) : IKernelBrowsers
{
    /// <summary>How long one request may take, as the SDK's default.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);

    public async Task<KernelBrowser> CreateAsync(KernelBrowserParams parameters, CancellationToken cancellationToken)
    {
        var reply = await SendJsonAsync(HttpMethod.Post, "/browsers", Body(parameters), cancellationToken).ConfigureAwait(false);
        return new KernelBrowser(
            Text(reply.Body?["session_id"]) ?? throw new KernelApiException(HttpStatusCode.OK, "Kernel created a browser without a session_id"),
            Text(reply.Body?["cdp_ws_url"]) ?? throw new KernelApiException(HttpStatusCode.OK, "Kernel created a browser without a cdp_ws_url"),
            Text(reply.Body?["browser_live_view_url"]));
    }

    public async Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await SendJsonAsync(HttpMethod.Delete, $"/browsers/{Uri.EscapeDataString(sessionId)}", null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (KernelApiException exception) when (exception.Status == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListActiveAsync(IReadOnlyDictionary<string, string> tags, CancellationToken cancellationToken)
    {
        var query = new StringBuilder("status=active");
        foreach (var (key, value) in tags)
        {
            query.Append("&tags%5B").Append(Uri.EscapeDataString(key)).Append("%5D=").Append(Uri.EscapeDataString(value));
        }

        var ids = new List<string>();
        var offset = 0;
        while (true)
        {
            var path = offset == 0 ? $"/browsers?{query}" : $"/browsers?{query}&offset={offset}";
            var reply = await SendJsonAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
            ids.AddRange(reply.Body is JsonArray items ? items.Select(item => Text(item?["session_id"])).OfType<string>() : []);

            // Kernel pages with X-Has-More and X-Next-Offset, the absolute start of the next page; 0 is its stop sentinel.
            var more = Header(reply.Headers, "X-Has-More");
            var next = int.TryParse(Header(reply.Headers, "X-Next-Offset"), out var parsed) ? parsed : (int?)null;
            if (more == "false" || (next is null && more != "true"))
            {
                return ids;
            }

            offset = next ?? throw new KernelApiException(HttpStatusCode.OK, "Kernel reported X-Has-More: true without an X-Next-Offset header; refusing to silently truncate the list");
            if (offset == 0)
            {
                return ids;
            }
        }
    }

    public async Task<string> StartReplayAsync(string sessionId, KernelReplayParams parameters, CancellationToken cancellationToken)
    {
        var body = new JsonObject();
        Set(body, "framerate", parameters.Framerate);
        Set(body, "max_duration_in_seconds", parameters.MaxDurationInSeconds);
        Set(body, "record_audio", parameters.RecordAudio);
        var reply = await SendJsonAsync(HttpMethod.Post, $"/browsers/{Uri.EscapeDataString(sessionId)}/replays", body, cancellationToken).ConfigureAwait(false);
        return Text(reply.Body?["replay_id"]) ?? throw new KernelApiException(HttpStatusCode.OK, "Kernel started a replay without a replay_id");
    }

    public async Task StopReplayAsync(string sessionId, string replayId, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, $"/browsers/{Uri.EscapeDataString(sessionId)}/replays/{Uri.EscapeDataString(replayId)}/stop", null, cancellationToken).ConfigureAwait(false);

    public async Task DownloadReplayAsync(string sessionId, string replayId, string file, CancellationToken cancellationToken)
    {
        using var reply = await SendAsync(HttpMethod.Get, $"/browsers/{Uri.EscapeDataString(sessionId)}/replays/{Uri.EscapeDataString(replayId)}", null, "video/mp4", cancellationToken).ConfigureAwait(false);
        await using var source = await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(file);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadFileAsync(string sessionId, string file, CancellationToken cancellationToken)
    {
        using var reply = await SendAsync(HttpMethod.Get, $"/browsers/{Uri.EscapeDataString(sessionId)}/fs/read_file?path={Uri.EscapeDataString(file)}", null, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        return await reply.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The create-browser body: the properties, then <see cref="KernelBrowserParams.Extra"/> over them.</summary>
    private static JsonObject Body(KernelBrowserParams parameters)
    {
        var body = new JsonObject();
        Set(body, "stealth", parameters.Stealth);
        Set(body, "headless", parameters.Headless);
        Set(body, "gpu", parameters.Gpu);
        Set(body, "timeout_seconds", parameters.TimeoutSeconds);
        Set(body, "proxy_id", parameters.ProxyId);
        if (parameters.Profile is { } profile)
        {
            var node = new JsonObject();
            Set(node, "id", profile.Id);
            Set(node, "name", profile.Name);
            Set(node, "save_changes", profile.SaveChanges);
            body["profile"] = node;
        }

        if (parameters.Viewport is { } viewport)
        {
            var node = new JsonObject { ["width"] = viewport.Width, ["height"] = viewport.Height };
            Set(node, "refresh_rate", viewport.RefreshRate);
            body["viewport"] = node;
        }

        if (parameters.Tags is { } tags)
        {
            body["tags"] = new JsonObject(tags.Select(tag => KeyValuePair.Create<string, JsonNode?>(tag.Key, tag.Value)));
        }

        foreach (var (key, value) in parameters.Extra ?? [])
        {
            body[key] = value?.DeepClone();
        }

        return body;
    }

    private static void Set(JsonObject body, string name, bool? value)
    {
        if (value is { } set)
        {
            body[name] = set;
        }
    }

    private static void Set(JsonObject body, string name, int? value)
    {
        if (value is { } set)
        {
            body[name] = set;
        }
    }

    private static void Set(JsonObject body, string name, string? value)
    {
        if (value is not null)
        {
            body[name] = value;
        }
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Header(HttpResponseHeaders headers, string name) => headers.TryGetValues(name, out var values) ? values.FirstOrDefault()?.Trim().ToLowerInvariant() : null;

    private async Task<(JsonNode? Body, HttpResponseHeaders Headers)> SendJsonAsync(HttpMethod method, string path, JsonObject? body, CancellationToken cancellationToken)
    {
        using var reply = await SendAsync(method, path, body, "application/json", cancellationToken).ConfigureAwait(false);

        // The body of an answer that arrived is read even when the caller has since cancelled: a created browser must be known to be deleted.
        using var timeout = new CancellationTokenSource(RequestTimeout);
        var text = await reply.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return (string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), reply.Headers);
    }

    /// <summary>Sends one request and returns its answer once the headers arrive, or throws <see cref="KernelApiException"/> for an error status.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonObject? body, string accept, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + path));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (body is not null)
        {
            message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        var reply = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (reply.IsSuccessStatusCode)
        {
            return reply;
        }

        using (reply)
        {
            using var read = new CancellationTokenSource(RequestTimeout);
            throw new KernelApiException(reply.StatusCode, ErrorMessage(reply.StatusCode, await reply.Content.ReadAsStringAsync(read.Token).ConfigureAwait(false)));
        }
    }

    /// <summary>
    /// The SDK's message for an error answer, trimmed: the status and the body's <c>message</c> (or the JSON body, or its text).
    /// Kernel's error bodies end in a newline, which would split the run's error line.
    /// </summary>
    private static string ErrorMessage(HttpStatusCode status, string text)
    {
        JsonNode? error = null;
        try
        {
            error = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            // Not JSON: the text is the message.
        }

        var message = (error is JsonObject json ? Text(json["message"]) ?? json.ToJsonString() : error?.ToJsonString() ?? text).Trim();
        return message.Length == 0 ? $"{(int)status} status code (no body)" : $"{(int)status} {message}";
    }
}
