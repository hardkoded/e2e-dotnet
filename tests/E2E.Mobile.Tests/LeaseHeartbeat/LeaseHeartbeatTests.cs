// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Mobile.Lease;

namespace E2E.Mobile.Tests.LeaseHeartbeat;

/// <summary>Ports <c>packages/mobile/tests/unit/lease-heartbeat.test.ts</c> on a clock the test moves.</summary>
public sealed class LeaseHeartbeatTests
{
    /// <summary>A daemon that answers every lease request, remembering the JSON-RPC requests it got.</summary>
    private sealed class FakeDaemon : HttpMessageHandler
    {
        public List<JsonObject> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            lock (Requests)
            {
                Requests.Add(body);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"result":{"ok":true,"data":{"lease":{"leaseId":"lease-2"}}}}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    private static JsonObject Scope() => new()
    {
        ["stateDir"] = "/state",
        ["tenant"] = "browserstack",
        ["runId"] = "run",
        ["leaseId"] = "lease-2",
        ["leaseBackend"] = "android-instance",
        ["leaseProvider"] = "browserstack",
    };

    [Fact]
    public async Task Renews_the_lease_on_the_leases_own_daemon_until_stopped()
    {
        var daemon = new FakeDaemon();
        var clock = new ManualDelay();
        var created = new List<DeviceClientConfig>();

        // The real client over a fake daemon, so the request that reaches it is the one a real daemon would get.
        var stop = await Mobile.Lease.LeaseHeartbeat.StartAsync(
            Scope(),
            (session, connection) =>
            {
                created.Add(DeviceClientConfig.For(session, connection));
                return new AgentDeviceRpcClient(session, connection!.Client, _ => Task.FromResult(new DaemonEndpoint(new Uri("http://127.0.0.1:1/"), "token")), new HttpClient(daemon));
            },
            _ => { },
            clock.Delay,
            CancellationToken.None);

        var config = Assert.Single(created);
        Assert.Matches("^e2e-lease-[0-9a-f]{8}$", config.Session);
        Assert.Equal("", config.DaemonBaseUrl);
        Assert.Equal("", config.DaemonAuthToken);
        Assert.Equal("auto", config.DaemonTransport);
        await clock.AdvanceAsync(TimeSpan.FromSeconds(140));
        Assert.Equal(8, daemon.Requests.Count);
        Assert.All(daemon.Requests, request => Assert.Equal("agent_device.lease.heartbeat", request["method"]!.GetValue<string>()));
        JsonAssert.Args($$"""{"session":"{{config.Session}}","tenantId":"browserstack","runId":"run","leaseProvider":"browserstack","leaseId":"lease-2","ttlMs":60000}""", daemon.Requests[^1]["params"]);
        await stop();
        await clock.AdvanceAsync(TimeSpan.FromSeconds(140));
        Assert.Equal(8, daemon.Requests.Count);
    }

    [Fact]
    public async Task Logs_a_failed_renewal_and_retries_surviving_a_closed_log_and_cancels_an_in_flight_beat_on_stop()
    {
        var logged = 0;
        var messages = new List<string>();
        var clock = new ManualDelay();
        var client = new FakeDeviceClient();
        var beats = 0;
        var hung = new TaskCompletionSource<CancellationToken>();
        client.RespondAsync(DeviceMethods.LeasesHeartbeat, async (_, token) =>
        {
            switch (Interlocked.Increment(ref beats))
            {
                case 1:
                    return new JsonObject();
                case 2:
                    throw new InvalidOperationException("transport unavailable");
                case 3:
                    throw new InvalidOperationException("daemon gone");
                default:
                    hung.TrySetResult(token);
                    await Task.Delay(Timeout.Infinite, token);
                    return null;
            }
        });
        var stop = await Mobile.Lease.LeaseHeartbeat.StartAsync(
            Scope(),
            (_, _) => client,
            line =>
            {
                messages.Add(line);
                if (++logged > 1)
                {
                    throw new InvalidOperationException("reporter closed");
                }
            },
            clock.Delay,
            CancellationToken.None);

        await clock.AdvanceAsync(TimeSpan.FromSeconds(40));
        Assert.Equal("lease lease-2: renewal failed: transport unavailable", messages[0]);
        Assert.Equal(2, messages.Count);

        await clock.AdvanceAsync(TimeSpan.FromSeconds(20), expectNext: false);
        var inFlight = await hung.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await stop();

        Assert.True(inFlight.IsCancellationRequested);
        Assert.Equal(2, messages.Count);
        await clock.AdvanceAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(4, beats);
    }
}
