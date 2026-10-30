// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Mobile.Tests.RpcClient;

/// <summary>
/// The JSON-RPC request <see cref="AgentDeviceRpcClient"/> sends a daemon. The .NET port has no upstream test for it:
/// upstream hands the lease to agent-device's own client, which builds the same <c>meta</c>.
/// </summary>
public sealed class AgentDeviceRpcClientTests
{
    private sealed class FakeDaemon : HttpMessageHandler
    {
        public List<JsonObject> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"result":{"ok":true,"data":{}}}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    // .NET-only: the translator is the port's stand-in for agent-device's client, and apps.list is the call device.unstable_raw's reference
    // example makes. Without a translation the production client refuses it before any request goes out.
    [Fact]
    public async Task Sends_apps_list_as_the_apps_command_with_the_device_selection()
    {
        var daemon = new FakeDaemon();
        using var http = new HttpClient(daemon);
        var client = new AgentDeviceRpcClient("e2e-ios-0", new JsonObject(), _ => Task.FromResult(new DaemonEndpoint(new Uri("http://127.0.0.1:1/"), null)), http);

        await client.CallAsync(DeviceMethods.AppsList, new JsonObject { ["platform"] = "ios", ["device"] = "iPhone 17 Pro" }, CancellationToken.None);

        var request = Assert.Single(daemon.Requests);
        Assert.Equal("agent_device.command", request["method"]!.GetValue<string>());
        var command = request["params"]!.AsObject();
        Assert.Equal("apps", command["command"]!.GetValue<string>());
        Assert.Empty(command["positionals"]!.AsArray());
        Assert.Equal("ios", command["flags"]!["platform"]!.GetValue<string>());
        Assert.Equal("iPhone 17 Pro", command["flags"]!["device"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sends_the_whole_lease_scope_in_the_meta_of_a_command_bound_to_a_lease()
    {
        // The settings a Limrun lease hands the client: the daemon reads the backend and provider of the scope from meta only.
        var settings = JsonNode.Parse("""
            {"stateDir":"/state","tenant":"e2e","runId":"run-1","leaseId":"lease-1","leaseBackend":"ios-instance","leaseProvider":"limrun","deviceKey":"key-1","clientId":"worker-0"}
            """)!.AsObject();
        var daemon = new FakeDaemon();
        var client = new AgentDeviceRpcClient("e2e-ios-0", settings, _ => Task.FromResult(new DaemonEndpoint(new Uri("http://127.0.0.1:1/"), null)), new HttpClient(daemon));

        await client.CallAsync(DeviceMethods.DevicesBoot, new JsonObject { ["platform"] = "ios" }, CancellationToken.None);
        await client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), CancellationToken.None);

        Assert.Equal(2, daemon.Requests.Count);
        foreach (var request in daemon.Requests)
        {
            var meta = request["params"]!["meta"]!.AsObject();
            meta.Remove("requestId");
            JsonAssert.Args(
                """{"tenantId":"e2e","runId":"run-1","leaseId":"lease-1","leaseBackend":"ios-instance","leaseProvider":"limrun","deviceKey":"key-1","clientId":"worker-0"}""",
                meta);
        }
    }
}
