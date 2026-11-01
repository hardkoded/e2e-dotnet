// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>
/// Finds the agent of the app under test. DevFlow's broker, when one runs, assigns each agent a port and lists them at
/// <c>http://localhost:19223/api/agents</c>; without a broker the agent listens on the port its <c>.mauidevflow</c>
/// config names, else 9223. The broker is never started from here: it belongs to the <c>maui</c> CLI.
/// </summary>
internal sealed class AgentDiscovery(HttpClient http, DevFlowAgentsOptions options)
{
    /// <summary>How long one broker query may take: it is on the loopback and answers from memory.</summary>
    internal static readonly TimeSpan BrokerTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How long between two tries while waiting for the agent to come up.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>The clock the wait counts on. Tests replace it with one that spends no time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = static (span, token) => Task.Delay(span, token);

    /// <summary>
    /// The agent's base address: the configured port, else the one agent of the platform (and app name, when set) the
    /// broker lists, else the default port. Several matching agents are a choice the options must make.
    /// </summary>
    public async Task<Uri> ResolveAsync(MobilePlatform? platform, CancellationToken cancellationToken)
    {
        if (options.Port is { } port)
        {
            return Endpoint(port);
        }

        if (options.UseBroker && await ListAgentsAsync(cancellationToken).ConfigureAwait(false) is { } agents)
        {
            var matching = agents.Where(agent => Matches(agent, platform)).ToList();
            if (matching.Count == 1)
            {
                return Endpoint(matching[0].Port);
            }

            if (matching.Count > 1)
            {
                var names = string.Join(", ", matching.Select(agent => $"{agent.AppName} ({agent.Platform}, port {agent.Port})"));
                throw new DevFlowAgentException(null, "ambiguous", $"{matching.Count} DevFlow agents match: {names}; set DevFlowAgentsOptions.AppName or Port to pick one");
            }
        }

        return Endpoint(DevFlowAgents.DefaultAgentPort);
    }

    /// <summary>How long one status probe may take while waiting: a port that refuses answers at once, one that hangs must not eat the whole wait.</summary>
    internal static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Polls the agent's status until it answers or <paramref name="timeout"/> runs out. With <paramref name="rediscover"/>,
    /// the client's endpoint is resolved again after a failed try, since an app that was just launched registers with
    /// the broker a moment later and may get another port, and a broker that still lists the process that was ended
    /// beside the new one is a passing state, not a failure. <paramref name="onResolved"/> runs once per distinct
    /// endpoint, to forward its port on Android. A try the agent accepts but never answers is bounded on its own. The
    /// last failure is the one reported.
    /// </summary>
    public async Task<JsonNode?> WaitForStatusAsync(DevFlowAgentClient client, TimeSpan timeout, CancellationToken cancellationToken, Func<Uri, CancellationToken, Task>? onResolved = null, bool rediscover = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        DevFlowAgentException? last = null;
        Uri? announced = null;
        while (true)
        {
            try
            {
                var endpoint = await client.EndpointAsync(deadline.Token).ConfigureAwait(false);
                if (onResolved is not null && endpoint != announced)
                {
                    await onResolved(endpoint, deadline.Token).ConfigureAwait(false);
                    announced = endpoint;
                }

                using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                probe.CancelAfter(ProbeTimeout);
                try
                {
                    return await client.GetAsync("api/v1/agent/status", probe.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested)
                {
                    throw new DevFlowAgentException(null, "timeout", $"the DevFlow agent at {endpoint} accepted the connection but did not answer within {ProbeTimeout.TotalSeconds:0} seconds");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Out of time. A port that refused every connection is an app that is not running; one that accepted and never answered is a hang.
                throw last is { Code: "unreachable" or "ambiguous" }
                    ? new DevFlowAgentException(null, last.Code, $"{last.Message} (waited {timeout.TotalSeconds:0} seconds)")
                    : new DevFlowAgentException(null, "timeout", $"the DevFlow agent did not answer within {timeout.TotalSeconds:0} seconds; is the app running with the agent registered?");
            }
            catch (DevFlowAgentException failure) when (failure.Status is null && (rediscover || failure.Code != "ambiguous"))
            {
                last = failure;
                if (rediscover)
                {
                    client.Reset();
                }

                try
                {
                    await Delay(PollInterval, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new DevFlowAgentException(null, failure.Code, $"{failure.Message} (waited {timeout.TotalSeconds:0} seconds)");
                }
            }
        }
    }

    private Uri Endpoint(int port) => new($"http://{options.Host}:{port}/");

    private bool Matches(BrokerAgent agent, MobilePlatform? platform) =>
        (platform is null || string.Equals(agent.Platform, platform.Value.WireName(), StringComparison.OrdinalIgnoreCase))
        && (options.AppName is null || string.Equals(agent.AppName, options.AppName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The broker's agent list, or null when no broker answers: the agent then listens on its own port.</summary>
    private async Task<List<BrokerAgent>?> ListAgentsAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(BrokerTimeout);
        try
        {
            using var response = await http.GetAsync(new Uri($"http://{options.Host}:{options.BrokerPort}/api/agents"), limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
            if (JsonNode.Parse(text) is not JsonArray list)
            {
                return null;
            }

            return
            [
                .. list.OfType<JsonObject>()
                    .Select(agent => new BrokerAgent(Text(agent["appName"]) ?? "", Text(agent["platform"]) ?? "", (int)(Number(agent["port"]) ?? 0)))
                    .Where(agent => agent.Port > 0),
            ];
        }
        catch (Exception cause) when (cause is HttpRequestException or JsonException || (cause is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;

    /// <summary>One registration the broker lists.</summary>
    private sealed record BrokerAgent(string AppName, string Platform, int Port);
}
