// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>
/// The identity this client holds the agent's mutation lease under. DevFlow lets one host at a time change the app (the
/// Inspector, the MCP server, the CLI, or a test), and names the holder to the others.
/// </summary>
internal sealed record LeaseIdentity(string LeaseId, string HolderKind = "e2e", string? Label = null);

/// <summary>The agent answered with an error, could not be reached, or did not answer in time.</summary>
internal sealed class DevFlowAgentException(HttpStatusCode? status, string? code, string message, JsonObject? details = null) : Exception(message)
{
    /// <summary>The HTTP status, or null when the agent never answered.</summary>
    public HttpStatusCode? Status { get; } = status;

    /// <summary>
    /// The machine-readable code: the spec's <c>errorCode</c>, the agent's <c>reason</c>, or for a failure with no
    /// answer <c>timeout</c> or <c>unreachable</c>. Null when the agent gave none.
    /// </summary>
    public string? Code { get; } = code;

    /// <summary>The <c>details</c> the agent attached: the lease holder, the capture epochs. Null without any.</summary>
    public JsonObject? Details { get; } = details;
}

/// <summary>
/// The DevFlow agent's HTTP API (<c>/api/v1/*</c>), as far as the device client uses it. A call answers the JSON the
/// agent returned, or throws <see cref="DevFlowAgentException"/>. Nothing retries: a retried tap could land twice.
/// <para>
/// The agent's endpoint may be resolved on the first call, since a worker that starts without <c>prepare</c> has to find
/// the agent itself. The agent's mutation lease lapses ten seconds after the holder was last heard from, so, as the
/// official client does, every request that changes the app claims the lease again first: a claim the holder already
/// has is a heartbeat, and one another host holds is refused before the action is sent.
/// </para>
/// </summary>
internal sealed class DevFlowAgentClient
{
    /// <summary>How long one command may take. A tree of a large screen on a slow emulator can take seconds.</summary>
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    private const string LeasePath = "api/v1/agent/lease";

    private readonly HttpClient _http;
    private readonly Func<MobilePlatform?, CancellationToken, Task<Uri>> _endpoint;
    private readonly LeaseIdentity? _lease;
    private readonly SemaphoreSlim _resolving = new(1, 1);
    private Uri? _resolved;

    public DevFlowAgentClient(HttpClient http, Uri endpoint, LeaseIdentity? lease)
        : this(http, (_, _) => Task.FromResult(endpoint), lease)
    {
        _resolved = endpoint;
    }

    public DevFlowAgentClient(HttpClient http, Func<MobilePlatform?, CancellationToken, Task<Uri>> endpoint, LeaseIdentity? lease)
    {
        _http = http;
        _endpoint = endpoint;
        _lease = lease;
    }

    public LeaseIdentity? Lease => _lease;

    /// <summary>The platform the agent is expected to run, for a resolution that has to pick among several agents. Null picks by app alone.</summary>
    public MobilePlatform? PlatformHint { get; set; }

    /// <summary>
    /// The agent's base address: <c>http://localhost:10223/</c>. Resolved on the first call when it was not known up
    /// front, and kept from then on; a resolution that fails is tried again on the next call.
    /// </summary>
    public async Task<Uri> EndpointAsync(CancellationToken cancellationToken)
    {
        if (_resolved is { } known)
        {
            return known;
        }

        await _resolving.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _resolved ??= await _endpoint(PlatformHint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _resolving.Release();
        }
    }

    public Task<JsonNode?> GetAsync(string path, CancellationToken cancellationToken) =>
        SendJsonAsync(HttpMethod.Get, path, null, cancellationToken);

    /// <summary>A request that changes the app. The lease is claimed first, when this client holds one.</summary>
    public async Task<JsonNode?> PostAsync(string path, JsonObject? body, CancellationToken cancellationToken)
    {
        await ClaimLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await SendJsonAsync(HttpMethod.Post, path, body ?? new JsonObject(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A request that changes the app. The lease is claimed first, when this client holds one.</summary>
    public async Task<JsonNode?> PutAsync(string path, JsonObject? body, CancellationToken cancellationToken)
    {
        await ClaimLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await SendJsonAsync(HttpMethod.Put, path, body ?? new JsonObject(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Claims the agent's mutation lease under this client's identity, or renews it. The agent answers 200 whatever
    /// happened and says in the body who holds the lease: a claim another host holds is refused here, naming the holder.
    /// Nothing is sent when this client has no identity.
    /// </summary>
    public async Task ClaimLeaseAsync(CancellationToken cancellationToken)
    {
        if (_lease is null)
        {
            return;
        }

        var body = new JsonObject { ["action"] = "claim", ["leaseId"] = _lease.LeaseId, ["holderKind"] = _lease.HolderKind, ["label"] = _lease.Label };
        var status = await SendJsonAsync(HttpMethod.Post, LeasePath, body, cancellationToken).ConfigureAwait(false) as JsonObject;
        if (status is null || Flag(status["youHold"]) != false)
        {
            return;
        }

        var details = new JsonObject { ["HolderKind"] = Text(status["holderKind"]), ["Label"] = Text(status["label"]) };
        throw new DevFlowAgentException(HttpStatusCode.Conflict, "lease", Text(status["error"]) ?? "another DevFlow host holds the app's mutation lease", details);
    }

    /// <summary>Gives the lease back, so another host can drive the app. Nothing is sent when this client has no identity.</summary>
    public Task ReleaseLeaseAsync(CancellationToken cancellationToken) =>
        _lease is null
            ? Task.CompletedTask
            : SendJsonAsync(HttpMethod.Post, LeasePath, new JsonObject { ["action"] = "release", ["leaseId"] = _lease.LeaseId }, cancellationToken);

    /// <summary>A PNG the agent returns, with the capture epoch its headers name.</summary>
    public async Task<(byte[] Bytes, long? CaptureEpoch)> GetPngAsync(string path, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(CommandTimeout);
        using var response = await SendAsync(HttpMethod.Get, path, null, "image/png", limit.Token, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode >= 400)
        {
            var text = await ReadTextAsync(response, limit.Token, cancellationToken).ConfigureAwait(false);
            throw Failure(response.StatusCode, Parse(text), text);
        }

        byte[] bytes;
        try
        {
            bytes = await response.Content.ReadAsByteArrayAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DevFlowAgentException(null, "timeout", $"the DevFlow agent did not finish sending the screenshot within {CommandTimeout.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException cause)
        {
            throw new DevFlowAgentException(null, "unreachable", $"the DevFlow agent closed the connection: {cause.Message}");
        }

        long? epoch = response.Headers.TryGetValues("X-DevFlow-Capture-Epoch", out var values) && long.TryParse(values.FirstOrDefault(), out var parsed) ? parsed : null;
        return (bytes, epoch);
    }

    private async Task<JsonNode?> SendJsonAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(CommandTimeout);
        using var response = await SendAsync(method, path, body, "application/json", limit.Token, cancellationToken).ConfigureAwait(false);
        var text = await ReadTextAsync(response, limit.Token, cancellationToken).ConfigureAwait(false);
        var root = Parse(text);
        if ((int)response.StatusCode >= 400)
        {
            throw Failure(response.StatusCode, root, text);
        }

        // The MAUI agent answers a refused action as 200 with success: false in some paths; the spec allows it too.
        if (root is JsonObject answer && Flag(answer["success"]) == false)
        {
            throw Failure(response.StatusCode, root, text);
        }

        return root;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonNode? body, string accept, CancellationToken limit, CancellationToken cancellationToken)
    {
        var endpoint = await EndpointAsync(limit).ConfigureAwait(false);
        using var message = new HttpRequestMessage(method, new Uri(endpoint, path));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (_lease is not null)
        {
            message.Headers.TryAddWithoutValidation("X-DevFlow-Lease", _lease.LeaseId);
            message.Headers.TryAddWithoutValidation("X-DevFlow-Holder", _lease.HolderKind);
            if (_lease.Label is not null)
            {
                message.Headers.TryAddWithoutValidation("X-DevFlow-Label", _lease.Label);
            }
        }

        if (body is not null)
        {
            message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        try
        {
            return await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, limit).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DevFlowAgentException(null, "timeout", $"the DevFlow agent did not answer {method} {path} within {CommandTimeout.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException cause)
        {
            throw new DevFlowAgentException(null, "unreachable", $"the DevFlow agent at {endpoint} could not be reached ({cause.Message}); is the app running with the agent registered?");
        }
    }

    private static async Task<string> ReadTextAsync(HttpResponseMessage response, CancellationToken limit, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(limit).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DevFlowAgentException(null, "timeout", $"the DevFlow agent did not finish answering within {CommandTimeout.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException cause)
        {
            throw new DevFlowAgentException(null, "unreachable", $"the DevFlow agent closed the connection: {cause.Message}");
        }
    }

    /// <summary>
    /// The error an answer carries. The spec writes RFC 7807 (<c>errorCode</c>, <c>detail</c>); the MAUI agent writes
    /// <c>{ success: false, error, reason?, details? }</c>. Both are read.
    /// </summary>
    private static DevFlowAgentException Failure(HttpStatusCode status, JsonNode? root, string text)
    {
        var body = root as JsonObject;
        var code = Text(body?["errorCode"]) ?? Text(body?["reason"]);
        var detail = Text(body?["error"]) ?? Text(body?["detail"]) ?? Text(body?["title"]) ?? Short(text);
        return new DevFlowAgentException(status, code, $"HTTP {(int)status}: {detail}", body?["details"] as JsonObject);
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return text.Length == 0 ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Short(string text)
    {
        var line = text.Trim();
        return line.Length == 0 ? "no body" : line.Length > 200 ? line[..200] + "..." : line;
    }

    private static bool? Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
