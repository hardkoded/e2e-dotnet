// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.Expo;

/// <summary>What the provider asks Expo for: one agent-device session on one platform.</summary>
internal sealed record EasSessionParams
{
    public required string AppId { get; init; }

    public required MobilePlatform Platform { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public string? BuildId { get; init; }

    public string? ApplicationArchiveUrl { get; init; }

    /// <summary>An iOS device name or UDID, or an Android AVD hardware profile.</summary>
    public string? DeviceIdentifier { get; init; }

    public int? MaxIdleTimeMinutes { get; init; }

    public int? MaxRunTimeMinutes { get; init; }

    /// <summary>The agent-device version Expo starts the daemon at.</summary>
    public string? PackageVersion { get; init; }
}

/// <summary>Where a session is: queued until the account has a free simulator, starting while it boots, ready once its daemon is reachable.</summary>
internal enum EasPhase
{
    Queued,
    Starting,
    Ready,
    Ended,
}

internal sealed record EasSessionState(
    EasPhase Phase,
    string? DaemonUrl = null,
    string? DaemonToken = null,
    string? OpenPreviewUrl = null,
    string? EndedStatus = null);

internal sealed record EasCreatedSession(string Id, string Url);

/// <summary>An error Expo answered with, its <c>errorCode</c> kept.</summary>
internal sealed class EasException(string message, string? code) : Exception($"Expo: {message}")
{
    public string? Code { get; } = code;
}

/// <summary>
/// Expo Simulators' sessions through Expo's GraphQL API over <see cref="HttpClient"/>, the API <c>eas simulator:*</c> uses,
/// so the integration installs no SDK.
/// </summary>
internal sealed class EasSessions(ExpoCredentials credentials, HttpClient http)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    private static readonly Uri GraphqlUrl = new("https://api.expo.dev/graphql");

    private const string Create = """
        mutation ($input: CreateDeviceRunSessionInput!) {
          deviceRunSession { createDeviceRunSession(deviceRunSessionInput: $input) { id app { slug ownerAccount { name } } } }
        }
        """;

    private const string State = """
        query ($id: ID!) {
          deviceRunSessions { byId(deviceRunSessionId: $id) {
            status
            turtleJobRun { status }
            remoteConfig { ... on AgentDeviceRunSessionRemoteConfig { agentDeviceRemoteSessionUrl agentDeviceRemoteSessionToken webPreviewUrl webPreviewToken } }
          } }
        }
        """;

    private const string Stop = """
        mutation ($id: ID!) {
          deviceRunSession { ensureDeviceRunSessionStopped(deviceRunSessionId: $id) { turtleJobRun { id status } } }
        }
        """;

    private const string CancelJob = """
        mutation ($id: ID!) {
          jobRun { cancelJobRun(jobRunId: $id) { id } }
        }
        """;

    /// <summary>Job run states before the job picks the session up: waiting for the account's concurrency.</summary>
    private static readonly HashSet<string> QueuedJob = ["NEW", "IN_QUEUE"];

    /// <summary>Job run states after which the session never becomes ready.</summary>
    private static readonly HashSet<string> EndedJob = ["ERRORED", "CANCELED", "FINISHED", "PENDING_CANCEL"];

    public async Task<EasCreatedSession> CreateAsync(EasSessionParams session, CancellationToken cancellationToken)
    {
        var input = new JsonObject
        {
            ["appId"] = session.AppId,
            ["name"] = session.Name,
            ["tags"] = new JsonArray([.. session.Tags.Select(tag => (JsonNode?)JsonValue.Create(tag))]),
            ["platform"] = session.Platform.WireName().ToUpperInvariant(),
            ["type"] = "AGENT_DEVICE",
        };
        Set(input, "buildId", session.BuildId);
        Set(input, "applicationArchiveUrl", session.ApplicationArchiveUrl);
        Set(input, "maxIdleTimeMinutes", session.MaxIdleTimeMinutes);
        Set(input, "maxRunTimeMinutes", session.MaxRunTimeMinutes);
        Set(input, "packageVersion", session.PackageVersion);
        if (session.DeviceIdentifier is not null)
        {
            input[session.Platform.WireName()] = new JsonObject { ["deviceIdentifier"] = session.DeviceIdentifier };
        }

        var data = await GraphqlAsync(Create, new JsonObject { ["input"] = input }, cancellationToken).ConfigureAwait(false);
        var created = Field(Field(data, "deviceRunSession"), "createDeviceRunSession");
        var app = Field(created, "app");
        if (Text(Field(created, "id")) is not { } id || Text(Field(app, "slug")) is not { } slug || Text(Field(Field(app, "ownerAccount"), "name")) is not { } owner)
        {
            throw Unexpected("createDeviceRunSession");
        }

        return new EasCreatedSession(id, $"https://expo.dev/accounts/{Uri.EscapeDataString(owner)}/projects/{Uri.EscapeDataString(slug)}/simulator-sessions/{id}");
    }

    public async Task<EasSessionState> StateAsync(string id, CancellationToken cancellationToken)
    {
        var data = await GraphqlAsync(State, new JsonObject { ["id"] = id }, cancellationToken).ConfigureAwait(false);
        var session = Field(Field(data, "deviceRunSessions"), "byId");
        var status = Text(Field(session, "status"));
        var jobRun = Field(session, "turtleJobRun");
        var job = Text(Field(jobRun, "status"));
        var config = Field(session, "remoteConfig");
        var daemonUrl = Field(config, "agentDeviceRemoteSessionUrl");
        var daemonToken = Field(config, "agentDeviceRemoteSessionToken");
        var previewUrl = Field(config, "webPreviewUrl");
        var previewToken = Field(config, "webPreviewToken");
        if (status is null
            || (jobRun is not null && job is null)
            || (config is not null && config is not JsonObject)
            || !new[] { daemonUrl, daemonToken, previewUrl, previewToken }.All(value => value is null || Text(value) is not null))
        {
            throw Unexpected("deviceRunSessions.byId");
        }

        if (status is "STOPPED" or "ERRORED")
        {
            return new EasSessionState(EasPhase.Ended, EndedStatus: status.ToLowerInvariant());
        }

        // A job that ended keeps the daemon address it had, which no longer answers.
        if (job is not null && EndedJob.Contains(job))
        {
            return new EasSessionState(EasPhase.Ended, EndedStatus: $"job {job.ToLowerInvariant()}");
        }

        if (Text(daemonUrl) is { } url && Text(daemonToken) is { } token)
        {
            var preview = previewToken is null ? Text(previewUrl) : null;
            return new EasSessionState(EasPhase.Ready, url, token, preview);
        }

        return new EasSessionState(job is null || QueuedJob.Contains(job) ? EasPhase.Queued : EasPhase.Starting);
    }

    /// <summary>
    /// Stops the session, and cancels its job when it is still queued: a stopped session's queued job still starts later
    /// and holds one of the account's concurrent sessions while it does. Stopping one already stopped, or one Expo no
    /// longer knows, succeeds.
    /// </summary>
    public async Task StopAsync(string id, CancellationToken cancellationToken)
    {
        JsonNode data;
        try
        {
            data = await GraphqlAsync(Stop, new JsonObject { ["id"] = id }, cancellationToken).ConfigureAwait(false);
        }
        catch (EasException cause) when (cause.Code == "NOT_FOUND_ERROR")
        {
            return;
        }

        if (Field(Field(data, "deviceRunSession"), "ensureDeviceRunSessionStopped") is not JsonObject stopped)
        {
            throw Unexpected("ensureDeviceRunSessionStopped");
        }

        if (!stopped.TryGetPropertyValue("turtleJobRun", out var job))
        {
            throw Unexpected("ensureDeviceRunSessionStopped");
        }

        if (job is null)
        {
            return;
        }

        if (Text(Field(job, "id")) is not { } jobId || Text(Field(job, "status")) is not { } jobStatus)
        {
            throw Unexpected("ensureDeviceRunSessionStopped");
        }

        if (QueuedJob.Contains(jobStatus))
        {
            await GraphqlAsync(CancelJob, new JsonObject { ["id"] = jobId }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><paramref name="key"/> of a JSON object; <see langword="null"/> for a missing key or a value that is not an object.</summary>
    private static JsonNode? Field(JsonNode? value, string key) => value is JsonObject node ? node[key] : null;

    private static string? Text(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The error for an answer without the fields an operation reads; it names no value, since the answer can hold a daemon token.</summary>
    private static InvalidOperationException Unexpected(string operation) => new($"Expo: unexpected response to {operation}");

    private static void Set(JsonObject target, string name, string? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }

    private static void Set(JsonObject target, string name, int? value)
    {
        if (value is { } number)
        {
            target[name] = number;
        }
    }

    /// <summary>Runs one operation; a GraphQL error throws with Expo's message and error code.</summary>
    private async Task<JsonNode> GraphqlAsync(string query, JsonObject variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphqlUrl)
        {
            Content = new StringContent(new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        var (name, value) = credentials.Header;
        request.Headers.TryAddWithoutValidation(name, value);

        // A connection that stalls must not hang a poll, or the startup limits never run.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(RequestTimeout);
        using var response = await http.SendAsync(request, limit.Token).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        JsonNode? body;
        try
        {
            body = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            var excerpt = Redact(text.Trim());
            throw new InvalidOperationException($"Expo: HTTP {status}, {(excerpt.Length > 200 ? excerpt[..200] : excerpt)}");
        }

        if (Field(body, "errors") is JsonArray { Count: > 0 } errors)
        {
            var error = errors[0];
            throw new EasException(Redact(Text(Field(error, "message")) ?? $"HTTP {status}"), Text(Field(Field(error, "extensions"), "errorCode")));
        }

        return !response.IsSuccessStatusCode || Field(body, "data") is not { } data ? throw new InvalidOperationException($"Expo: HTTP {status}, no data") : data;
    }

    /// <summary>The text with the credential this client sends hidden: Expo's error text can echo a rejected token.</summary>
    private string Redact(string text) =>
        (credentials.AccessToken ?? credentials.SessionSecret) is { Length: > 0 } secret ? text.Replace(secret, "*****", StringComparison.Ordinal) : text;
}
