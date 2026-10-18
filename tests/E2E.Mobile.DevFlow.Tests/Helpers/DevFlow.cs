// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.DevFlow.Tests;

internal static class DevFlow
{
    public static readonly LeaseIdentity Lease = new("lease-1", "e2e", "e2e mobileapp slot 1 of 1");

    /// <summary>A client over the fake agent, holding the lease identity the provider would have given it.</summary>
    public static (DevFlowDeviceClient Client, FakeAgent Agent) Over(MobilePlatform? platform = MobilePlatform.Ios, LeaseIdentity? lease = null)
    {
        var agent = new FakeAgent();
        return (new DevFlowDeviceClient(new DevFlowAgentClient(agent.Http, FakeAgent.Agent, lease ?? Lease), platform) { Delay = static (_, _) => Task.CompletedTask }, agent);
    }

    /// <summary>A client that has read the screen, so its refs are live.</summary>
    public static async Task<(DevFlowDeviceClient Client, FakeAgent Agent)> ReadAsync(MobilePlatform? platform = MobilePlatform.Ios)
    {
        var over = Over(platform);
        await over.Client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), CancellationToken.None);
        over.Agent.Calls.Clear();
        return over;
    }

    public static Task<JsonNode?> CallAsync(this DevFlowDeviceClient client, string method, string? options = null) =>
        client.CallAsync(method, options is null ? null : (JsonObject)JsonNode.Parse(options)!, CancellationToken.None);

    /// <summary>The error a call fails with, as the engine reads it.</summary>
    public static async Task<AgentDeviceError> FailureAsync(this DevFlowDeviceClient client, string method, string? options = null) =>
        await Assert.ThrowsAsync<AgentDeviceError>(() => client.CallAsync(method, options));

    /// <summary>What the engine makes of an error: the code a test sees.</summary>
    public static string EngineCode(AgentDeviceError error) => Assert.IsType<EngineException>(DeviceErrors.TranslateError(error, "operation")).Code;

    /// <summary>What the engine makes of an error from an action on a node, where a stale ref is retried.</summary>
    public static string ActionCode(AgentDeviceError error) => Assert.IsType<EngineException>(DeviceErrors.StaleOr(error, "operation")).Code;

    /// <summary>The nodes of a snapshot by ref.</summary>
    public static Dictionary<string, JsonObject> Nodes(JsonNode? snapshot) =>
        snapshot!["nodes"]!.AsArray().OfType<JsonObject>().ToDictionary(node => node["ref"]!.GetValue<string>(), StringComparer.Ordinal);
}
