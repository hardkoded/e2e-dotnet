// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using E2E.Internal;

namespace E2E.GitHub;

/// <summary>What <see cref="Post.UpsertCommentAsync"/> needs (upstream's <c>PostParams</c>, <c>post.ts</c>).</summary>
/// <param name="Http">Sends the requests.</param>
/// <param name="ApiUrl"><c>https://api.github.com</c>, or an Enterprise Server API root.</param>
/// <param name="Token">The token the requests carry. It never reaches a message: see <c>Post.Redact</c>.</param>
/// <param name="Repository"><c>owner/repo</c>.</param>
/// <param name="PullRequest">The pull request's number.</param>
/// <param name="Marker">The hidden HTML comment that identifies the reporter's own comment.</param>
/// <param name="Body">The comment's text.</param>
internal sealed record PostParams(HttpClient Http, string ApiUrl, string Token, string Repository, int PullRequest, string Marker, string Body);

/// <summary>
/// One comment per pull request, kept current (upstream's <c>post.ts</c>): the reporter looks for the comment the token's own account wrote whose
/// first line is its marker and edits it, and creates one only when none exists. The REST calls are the ones GitHub documents: read the token's
/// account, list the issue's comments, update a comment, create a comment. Every request carries the reporter's cancellation token. Anything GitHub
/// answers outside the protocol is an error naming itself; the runner prints one line.
/// </summary>
internal static class Post
{
    private const int PerPage = 100;

    /// <summary>
    /// The list is followed until a short page says it is exhausted. The cap is a guard against a server that never sends one, not a limit any pull
    /// request reaches: ten thousand comments.
    /// </summary>
    private const int MaxPages = 100;

    /// <summary>.NET-only: upstream sends <c>@e2e-dev/github</c>, and this port names itself so its traffic is not counted as upstream's.</summary>
    internal const string UserAgent = "E2E.GitHub";

    /// <summary>What GitHub answers <c>GET /user</c> with for an installation token: <c>GITHUB_TOKEN</c> or a GitHub App's.</summary>
    private const string IntegrationRefusal = "Resource not accessible by integration";

    /// <summary>Whether a comment's author is the account the token posts as.</summary>
    private delegate bool Author(JsonElement user);

    /// <summary>
    /// Text that came from GitHub or from the machine, with the token taken out. .NET-only: upstream's messages never hold the token, and this
    /// keeps that true here whatever GitHub echoes back.
    /// </summary>
    private static string Redact(PostParams parameters, string text) => Redactor.For([("GITHUB_TOKEN", parameters.Token)]).Redact(text);

    /// <summary>Sends one GitHub REST call and hands back the response as it came.</summary>
    private static Task<HttpResponseMessage> SendAsync(PostParams parameters, HttpMethod method, string route, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, parameters.ApiUrl + route);
        request.Headers.Authorization = new("Bearer", parameters.Token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd(UserAgent);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return parameters.Http.SendAsync(request, cancellationToken);
    }

    /// <summary>The start of a response's body, for error messages.</summary>
    private static async Task<string> BodyStartAsync(PostParams parameters, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            text = "";
        }

        return Redact(parameters, text.Length > 200 ? text[..200] : text);
    }

    /// <summary>The body as JSON, or null when it is not.</summary>
    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The error for a response the protocol has no meaning for, with the start of its body.</summary>
    private static InvalidOperationException Unexpected(HttpResponseMessage response, HttpMethod method, string route, string detail) =>
        new($"GitHub responded {(int)response.StatusCode} to {method.Method} {route}{(detail.Length == 0 ? "" : ": " + detail)}");

    /// <summary>Whether GitHub throttled the request rather than refused it: a 429, or a 403 for a spent primary limit, a <c>Retry-After</c>, or a secondary limit.</summary>
    private static bool IsRateLimited(HttpResponseMessage response, string detail)
    {
        if ((int)response.StatusCode == 429)
        {
            return true;
        }

        return response.StatusCode == System.Net.HttpStatusCode.Forbidden
            && ((response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.Contains("0"))
                || response.Headers.Contains("retry-after")
                || detail.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The error the runner prints for a response that is not a success.</summary>
    private static async Task<Exception> FailureAsync(PostParams parameters, HttpResponseMessage response, HttpMethod method, string route, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return new InvalidOperationException("GitHub rejected the token (GITHUB_TOKEN or GH_TOKEN)");
        }

        var detail = await BodyStartAsync(parameters, response, cancellationToken).ConfigureAwait(false);
        if (IsRateLimited(response, detail))
        {
            return new InvalidOperationException($"GitHub rate limited {method.Method} {route}; wait for the rate-limit reset or Retry-After before rerunning the job");
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
        {
            return new InvalidOperationException(
                $"the token cannot comment on {parameters.Repository}#{parameters.PullRequest}: a pull request from a fork runs with a read-only token, and the job needs `permissions: pull-requests: write`");
        }

        return Unexpected(response, method, route, detail);
    }

    /// <summary>One GitHub REST call; anything but success is an error the runner can print.</summary>
    private static async Task<JsonElement?> RequestAsync(PostParams parameters, HttpMethod method, string route, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(parameters, method, route, body, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        }

        throw await FailureAsync(parameters, response, method, route, cancellationToken).ConfigureAwait(false);
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// The account the token posts as. A personal token names its user at <c>GET /user</c>. An installation token cannot read it, and posts as its
    /// app's bot, <c>github-actions[bot]</c> or <c>&lt;app&gt;[bot]</c>, so any bot author matches.
    /// </summary>
    private static async Task<Author> TokenAuthorAsync(PostParams parameters, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(parameters, HttpMethod.Get, "/user", null, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var user = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            var login = user is { } u ? StringProperty(u, "login") : null;
            if (login is null)
            {
                throw new InvalidOperationException("GitHub answered GET /user without a login");
            }

            return author => string.Equals(StringProperty(author, "login"), login, StringComparison.OrdinalIgnoreCase);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var refusal = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (refusal is { } r && StringProperty(r, "message") == IntegrationRefusal)
            {
                return author => StringProperty(author, "type") == "Bot";
            }
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw await FailureAsync(parameters, response, HttpMethod.Get, "/user", cancellationToken).ConfigureAwait(false);
        }

        throw Unexpected(response, HttpMethod.Get, "/user", await BodyStartAsync(parameters, response, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Whether a comment body is the reporter's own for this marker: its first line is the marker, exactly. The reporter has always written it there,
    /// so a marker quoted further down, as in a reply, is not a match, and neither is a first line that only starts or ends with it.
    /// </summary>
    private static bool CarriesMarker(string body, string marker)
    {
        var end = body.IndexOf('\n', StringComparison.Ordinal);
        var firstLine = end == -1 ? body : body[..end];
        return (firstLine.EndsWith('\r') ? firstLine[..^1] : firstLine) == marker;
    }

    /// <summary>The id of the comment the author wrote carrying the marker, or null when no page has one.</summary>
    private static async Task<long?> FindCommentAsync(PostParams parameters, string issue, Author author, CancellationToken cancellationToken)
    {
        for (var page = 1; page <= MaxPages; page++)
        {
            var comments = await RequestAsync(parameters, HttpMethod.Get, $"{issue}/comments?per_page={PerPage}&page={page}", null, cancellationToken).ConfigureAwait(false);
            if (comments is not { ValueKind: JsonValueKind.Array } list)
            {
                throw new InvalidOperationException("GitHub answered the comment list with a body that is not a list");
            }

            foreach (var comment in list.EnumerateArray())
            {
                if (comment.ValueKind == JsonValueKind.Object
                    && comment.TryGetProperty("user", out var user)
                    && user.ValueKind == JsonValueKind.Object
                    && author(user)
                    && StringProperty(comment, "body") is { } body
                    && CarriesMarker(body, parameters.Marker)
                    && comment.TryGetProperty("id", out var id)
                    && id.ValueKind == JsonValueKind.Number
                    && id.TryGetInt64(out var number))
                {
                    return number;
                }
            }

            if (list.GetArrayLength() < PerPage)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Creates or updates the reporter's comment and returns its URL (upstream's <c>upsertComment</c>).</summary>
    public static async Task<string> UpsertCommentAsync(PostParams parameters, CancellationToken cancellationToken)
    {
        var issue = $"/repos/{parameters.Repository}/issues/{parameters.PullRequest}";
        var existing = await FindCommentAsync(parameters, issue, await TokenAuthorAsync(parameters, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var body = new { body = parameters.Body };
        var created = existing is null
            ? await RequestAsync(parameters, HttpMethod.Post, $"{issue}/comments", body, cancellationToken).ConfigureAwait(false)
            : await RequestAsync(parameters, HttpMethod.Patch, $"/repos/{parameters.Repository}/issues/comments/{existing}", body, cancellationToken).ConfigureAwait(false);
        return created is { } c && StringProperty(c, "html_url") is { } url
            ? url
            : throw new InvalidOperationException("GitHub answered the comment without its html_url");
    }
}
