// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.BrowserStack;

/// <summary>A session the recording covers, as the lease named it (upstream's <c>SessionRef</c>).</summary>
internal sealed record SessionRef(string Build, string SessionName);

/// <summary>A session the lookup found: its <c>hashed_id</c>, when it started, and its video, if the API says (upstream's <c>FoundSession</c>).</summary>
/// <param name="Id">The session's <c>hashed_id</c>.</param>
/// <param name="StartedAt">When the session started; null when it has no start time or one that does not parse.</param>
/// <param name="VideoUrl">Null until BrowserStack has a video for the session.</param>
internal sealed record FoundSession(string Id, DateTimeOffset? StartedAt, string? VideoUrl);

/// <summary>
/// BrowserStack's App Automate REST API over <see cref="HttpClient"/> (upstream's <c>sessions.ts</c>): uploads a build from a URL, and
/// finds a session by its build and name, with its start time and the URL of its video.
/// </summary>
internal static class BrowserStackSessions
{
    /// <summary>The App Automate API's base.</summary>
    private const string DefaultApiEndpoint = "https://api-cloud.browserstack.com/app-automate";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>BrowserStack downloads the build before it answers an upload from a URL.</summary>
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(300);

    /// <summary>Builds or sessions the list asks for at once.</summary>
    private const int PageSize = 50;

    /// <summary>Pages a lookup reads before it gives up: both lists are newest first, so the run is near the top.</summary>
    private const int MaxPages = 10;

    /// <summary>
    /// The API at <c>BROWSERSTACK_API_ENDPOINT</c> from the run's environment, else BrowserStack's own. Throws, without repeating the
    /// value, when the override is not an http(s) URL or carries a username or password, which a failed request would otherwise put in
    /// its error message (upstream <c>browserstackApiEndpoint</c>).
    /// </summary>
    public static string ApiEndpoint(IReadOnlyDictionary<string, string?> env)
    {
        var value = env.GetValueOrDefault("BROWSERSTACK_API_ENDPOINT")?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return DefaultApiEndpoint;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ConfigurationException("INVALID_CONFIG", "BROWSERSTACK_API_ENDPOINT is not an http(s) URL");
        }

        if (url.Query.Length > 1 || url.Fragment.Length > 1)
        {
            throw new ConfigurationException("INVALID_CONFIG", "BROWSERSTACK_API_ENDPOINT must not carry a query or fragment");
        }

        if (url.UserInfo.Length > 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", "BROWSERSTACK_API_ENDPOINT must not carry a username or password; set BROWSERSTACK_USERNAME and BROWSERSTACK_ACCESS_KEY instead");
        }

        return value.TrimEnd('/');
    }

    /// <summary>
    /// The newest session named <c>SessionName</c> in the newest build named <c>Build</c>, reading each list a page at a time. Every
    /// request is bounded by a timeout and the token, and errors name the build and the session, never the credentials or a URL.
    /// </summary>
    public static async Task<FoundSession> FindSessionAsync(HttpClient http, string endpoint, BrowserStackCredentials credentials, SessionRef session, CancellationToken cancellationToken)
    {
        var auth = BasicAuth(credentials);
        var what = $"BrowserStack session lookup in build {Quote(session.Build)}";

        // Builds of different projects may share a name, as targets of one run do by default.
        foreach (var buildId in await FindBuildsAsync(http, endpoint, auth, session.Build, cancellationToken).ConfigureAwait(false))
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var rows = await GetRowsAsync(http, ListUrl($"{endpoint}/builds/{Uri.EscapeDataString(buildId)}/sessions.json", page), auth, what, cancellationToken).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    var found = Record(Record(row)?["automation_session"]);
                    if (found is not null && Text(found["name"]) == session.SessionName && Text(found["hashed_id"]) is { Length: > 0 } id)
                    {
                        var video = Text(found["video_url"]);
                        return new FoundSession(id, UtcTimestamp(found["created_at"]), video is not null && IsHttpUrl(video) ? video : null);
                    }
                }

                if (rows.Count < PageSize)
                {
                    break;
                }
            }
        }

        throw new InvalidOperationException($"BrowserStack has no session named {Quote(session.SessionName)} in build {Quote(session.Build)}");
    }

    /// <summary>
    /// Has BrowserStack fetch the build at <paramref name="url"/> and returns its <c>bs://</c> app id: the session's <c>app</c>
    /// capability takes only an uploaded app, never a URL (upstream <c>uploadAppFromUrl</c>).
    /// </summary>
    public static async Task<string> UploadAppFromUrlAsync(HttpClient http, string endpoint, BrowserStackCredentials credentials, string url, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent { { new StringContent(url), "url" } };

        // The API may echo the URL, which can carry a signed token: the request hides it in every error.
        var body = Record(await RequestAsync(http, new Uri($"{endpoint}/upload"), BasicAuth(credentials), "BrowserStack app upload from a URL", UploadTimeout, form, url, cancellationToken).ConfigureAwait(false));
        return Text(body?["app_url"]) is { } appUrl && appUrl.StartsWith("bs://", StringComparison.Ordinal)
            ? appUrl
            : throw new InvalidOperationException("BrowserStack app upload from a URL failed: no bs:// app id in the response");
    }

    /// <summary><paramref name="message"/> with <paramref name="url"/>, raw or encoded, and its query string replaced, so a signed token never reaches a log (upstream <c>redact</c>).</summary>
    internal static string Redact(string message, string url)
    {
        var query = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Query.TrimStart('?') : "";
        return new[] { url, EncodeUriComponent(url), query, EncodeUriComponent(query) }
            .Where(secret => secret.Length > 0)
            .Aggregate(message, (text, secret) => text.Replace(secret, "<app URL>", StringComparison.Ordinal));
    }

    /// <summary>JavaScript's <c>encodeURIComponent</c>: it leaves <c>!'()*</c> alone, which <c>Uri.EscapeDataString</c> escapes.</summary>
    private static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value).Replace("%21", "!", StringComparison.Ordinal).Replace("%27", "'", StringComparison.Ordinal)
            .Replace("%28", "(", StringComparison.Ordinal).Replace("%29", ")", StringComparison.Ordinal).Replace("%2A", "*", StringComparison.Ordinal);

    /// <summary>The <c>hashed_id</c> of every build named <paramref name="build"/>, newest first.</summary>
    private static async Task<List<string>> FindBuildsAsync(HttpClient http, string endpoint, string auth, string build, CancellationToken cancellationToken)
    {
        var what = $"BrowserStack build lookup for {Quote(build)}";
        var ids = new List<string>();
        for (var page = 0; page < MaxPages; page++)
        {
            var rows = await GetRowsAsync(http, ListUrl($"{endpoint}/builds.json", page), auth, what, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var found = Record(Record(row)?["automation_build"]);
                if (found is not null && Text(found["name"]) == build && Text(found["hashed_id"]) is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }

            if (rows.Count < PageSize)
            {
                break;
            }
        }

        return ids.Count > 0 ? ids : throw new InvalidOperationException($"BrowserStack has no build named {Quote(build)}");
    }

    private static Uri ListUrl(string baseUrl, int page) =>
        new($"{baseUrl}?limit={PageSize.ToString(CultureInfo.InvariantCulture)}&offset={(page * PageSize).ToString(CultureInfo.InvariantCulture)}");

    /// <summary>A <c>created_at</c> as a UTC time. The API sends one with a zone; a time without one is read as UTC.</summary>
    private static DateTimeOffset? UtcTimestamp(JsonNode? value)
    {
        var text = Text(value)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) ? time : null;
    }

    private static string BasicAuth(BrowserStackCredentials credentials) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.AccessKey}"));

    /// <summary>One authenticated GET answered with a JSON array; anything else throws, as <paramref name="what"/>, with the HTTP status and the API's message.</summary>
    private static async Task<JsonArray> GetRowsAsync(HttpClient http, Uri url, string auth, string what, CancellationToken cancellationToken) =>
        await RequestAsync(http, url, auth, what, RequestTimeout, null, null, cancellationToken).ConfigureAwait(false) as JsonArray
            ?? throw new InvalidOperationException($"{what} failed: no list in the response");

    /// <summary>
    /// One authenticated request, a POST when it carries a form, answered with JSON; anything else throws, as <paramref name="what"/>,
    /// with the HTTP status and the API's message, <paramref name="hide"/> replaced in both. A caller's cancel is an
    /// <see cref="OperationCanceledException"/>, and a request that outlives <paramref name="timeout"/> a <see cref="TimeoutException"/>.
    /// </summary>
    private static async Task<JsonNode?> RequestAsync(HttpClient http, Uri url, string auth, string what, TimeSpan timeout, HttpContent? form, string? hide, CancellationToken cancellationToken)
    {
        string Clean(string text) => hide is null ? text : Redact(text, hide);

        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(timeout);
        HttpStatusCode status;
        string text;
        try
        {
            using var message = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, url) { Content = form };
            message.Headers.TryAddWithoutValidation("Authorization", auth);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await http.SendAsync(message, bound.Token).ConfigureAwait(false);
            status = response.StatusCode;
            text = await response.Content.ReadAsStringAsync(bound.Token).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is OperationCanceledException or HttpRequestException or IOException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException($"{what} cancelled", cause, cancellationToken);
            }

            if (bound.IsCancellationRequested)
            {
                throw new TimeoutException($"{what} got no answer within {timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s");
            }

            // The error may carry the hidden URL in its own cause or properties, so only its redacted message is kept.
            throw new InvalidOperationException($"{what} failed: {Clean(cause.Message)}", hide is null ? cause : new InvalidOperationException(Clean(cause.Message)));
        }

        JsonNode? body;
        try
        {
            body = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{what} failed: HTTP {((int)status).ToString(CultureInfo.InvariantCulture)}, not JSON");
        }

        if (status is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            var record = Record(body);
            var message = Text(record?["message"]) ?? Text(record?["error"]);
            var detail = message is { Length: > 0 } ? $" ({Truncate(Clean(message), 200)})" : "";
            throw new InvalidOperationException($"{what} failed: HTTP {((int)status).ToString(CultureInfo.InvariantCulture)}{detail}");
        }

        return body;
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The text as JSON writes it, quotes included, as upstream names a build or session.</summary>
    private static string Quote(string text) => JsonSerializer.Serialize(text, QuoteOptions);

    private static JsonObject? Record(JsonNode? node) => node as JsonObject;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);
}
