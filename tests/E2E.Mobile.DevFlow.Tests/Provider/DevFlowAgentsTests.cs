// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.DevFlow.Tests.Provider;

public sealed class DevFlowAgentsTests
{
    private static readonly JsonArray Brokered = (JsonArray)JsonNode.Parse(
        """
        [
          { "id": "7ff0e6fd13d9", "project": "/work/MobileApp/MobileApp.csproj", "tfm": "net10.0-ios", "platform": "iOS", "appName": "MobileApp", "port": 10223 },
          { "id": "a3c9e1f20b44", "project": "/work/MobileApp/MobileApp.csproj", "tfm": "net10.0-android", "platform": "Android", "appName": "MobileApp", "port": 10224 }
        ]
        """)!;

    private static DeviceRequest Request(MobilePlatform platform = MobilePlatform.Ios, string? app = FakeAgent.PackageId, List<string>? log = null) => new()
    {
        Platform = platform,
        RunId = "run1",
        TargetName = "mobileapp",
        Slot = 0,
        Slots = 1,
        AgentDeviceVersion = "0.21.29",
        App = app,
        ProjectRoot = "/work",
        Env = new Dictionary<string, string?>(),
        Log = line => log?.Add(line),
    };

    private static (DevFlowAgents Provider, FakeAgent Agent) Over(DevFlowAgentsOptions? options = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var agent = new FakeAgent();
        var http = agent.Http;
        var settings = options ?? new DevFlowAgentsOptions();
        return (new DevFlowAgents(settings, http, new AgentDiscovery(http, settings) { Delay = delay ?? (static (_, _) => Task.CompletedTask) }), agent);
    }

    [Fact]
    public async Task Without_a_broker_the_agent_is_on_the_default_port_and_the_lease_is_claimed()
    {
        var log = new List<string>();
        var (provider, agent) = Over();

        var lease = await provider.AcquireAsync(Request(log: log), CancellationToken.None);

        Assert.Equal(["GET /api/v1/agent/status"], agent.Calls.Where(call => call.Uri.Port == 9223).Select(call => call.Route));
        Assert.Equal("localhost:9223", lease.Id);
        Assert.Equal("MobileApp", lease.Device);
        Json.AssertEqual("""{"host":"localhost","port":9223,"leaseId":"e2e-run1-0","label":"e2e mobileapp slot 1 of 1"}""", lease.Client);
        Assert.True(DeviceBindings.IsClientConfig(lease.Client));
        Json.AssertEqual("""{"action":"claim","leaseId":"e2e-run1-0","holderKind":"e2e","label":"e2e mobileapp slot 1 of 1"}""", Assert.Single(agent.LeaseCalls).Body);
        Assert.Contains(log, line => line.Contains("attached to MobileApp (ios) at http://localhost:9223/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_broker_names_the_agent_of_the_platform()
    {
        var (provider, agent) = Over();
        agent.ScriptJson("GET", "/api/agents", Brokered, port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status("android"), port: 10224);

        var ios = await provider.AcquireAsync(Request(), CancellationToken.None);
        var android = await provider.AcquireAsync(Request(MobilePlatform.Android), CancellationToken.None);

        Assert.Equal(10223, ios.Client!["port"]!.GetValue<int>());
        Assert.Equal(10224, android.Client!["port"]!.GetValue<int>());
        Assert.Equal("GET /api/agents", agent.Calls[0].Route);
    }

    [Fact]
    public async Task Several_matching_agents_are_a_choice_the_options_make()
    {
        var two = new JsonArray(Brokered[0]!.DeepClone(), Brokered[0]!.DeepClone());
        two[1]!["appName"] = "OtherApp";
        two[1]!["port"] = 10230;
        var (provider, agent) = Over();
        agent.ScriptJson("GET", "/api/agents", two, port: DevFlowAgents.DefaultBrokerPort);

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => provider.AcquireAsync(Request(), CancellationToken.None));

        Assert.Contains("2 DevFlow agents match", error.Message, StringComparison.Ordinal);
        Assert.Contains("DevFlowAgentsOptions.AppName or Port", error.Message, StringComparison.Ordinal);

        var (named, namedAgent) = Over(new DevFlowAgentsOptions { AppName = "OtherApp" });
        namedAgent.ScriptJson("GET", "/api/agents", two, port: DevFlowAgents.DefaultBrokerPort);
        var lease = await named.AcquireAsync(Request(app: null), CancellationToken.None);
        Assert.Equal(10230, lease.Client!["port"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_configured_port_wins_over_the_broker()
    {
        var (provider, agent) = Over(new DevFlowAgentsOptions { Port = 9300 });
        agent.ScriptJson("GET", "/api/agents", Brokered, port: DevFlowAgents.DefaultBrokerPort);

        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);

        Assert.Equal(9300, lease.Client!["port"]!.GetValue<int>());
        Assert.DoesNotContain(agent.Calls, call => call.Uri.Port == DevFlowAgents.DefaultBrokerPort);
    }

    [Fact]
    public async Task Waits_for_the_agent_to_come_up()
    {
        var (provider, agent) = Over();
        var refusals = 3;
        agent.Script("GET", "/api/v1/agent/status", _ => refusals-- > 0 ? throw new HttpRequestException("Connection refused") : FakeAgent.Json(FakeAgent.Status()));

        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);

        Assert.Equal(4, agent.Sent("GET /api/v1/agent/status").Count());
        Assert.Equal(9223, lease.Client!["port"]!.GetValue<int>());
    }

    [Fact]
    public async Task An_agent_that_never_answers_fails_the_run_as_a_missing_session()
    {
        // The deadline runs out while the wait between two tries sleeps, after the first try was refused. A real clock would
        // race that first try: a deadline that ran out before it would report a hang instead of a missing app.
        var (provider, agent) = Over(delay: static (_, _) => Task.FromCanceled(new CancellationToken(canceled: true)));
        agent.ScriptRefused("GET", "/api/v1/agent/status");

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => provider.AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("SESSION_NOT_FOUND", error.Code);
        Assert.Contains("waited", error.Message, StringComparison.Ordinal);
        Assert.Single(agent.Sent("GET /api/v1/agent/status"));
    }

    [Fact]
    public async Task An_agent_of_another_platform_or_app_fails_the_run()
    {
        var (provider, _) = Over();

        var platform = await Assert.ThrowsAsync<AgentDeviceError>(() => provider.AcquireAsync(Request(MobilePlatform.Android), CancellationToken.None));
        var app = await Assert.ThrowsAsync<AgentDeviceError>(() => provider.AcquireAsync(Request(app: "com.example.other"), CancellationToken.None));

        Assert.Contains("runs a ios app", platform.Message, StringComparison.Ordinal);
        Assert.Contains("not com.example.other", app.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lease_another_host_holds_fails_the_run_with_the_holder()
    {
        var (provider, agent) = Over();
        agent.ScriptLeaseHeldBy("inspector", "VS Code");

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => provider.AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("mutation lease: inspector (VS Code)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task More_than_one_slot_is_a_configuration_error_since_one_agent_is_one_app()
    {
        var (provider, agent) = Over();

        var error = await Assert.ThrowsAsync<ConfigurationException>(() => provider.AcquireAsync(Request() with { Slots = 2 }, CancellationToken.None));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public async Task Release_gives_the_lease_back_and_tolerates_an_app_that_is_gone()
    {
        var log = new List<string>();
        var (provider, agent) = Over();
        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);
        agent.Calls.Clear();
        var context = new DeviceReleaseContext { RunId = "run1", TargetName = "mobileapp", Env = new Dictionary<string, string?>(), Log = log.Add };

        await provider.ReleaseAsync(lease, context, CancellationToken.None);
        agent.ScriptRefused("POST", "/api/v1/agent/lease");
        await provider.ReleaseAsync(lease, context, CancellationToken.None);

        Json.AssertEqual("""{"action":"release","leaseId":"e2e-run1-0"}""", agent.LeaseCalls.Last(call => call.Body?["action"]?.GetValue<string>() == "release").Body);
        Assert.Contains(log, line => line.Contains("not released", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_worker_with_a_binding_drives_the_agent_prepare_found_under_its_lease()
    {
        var (provider, agent) = Over();
        var client = provider.CreateClient("e2e-mobileapp-0", new SlotBinding { Client = (JsonObject)JsonNode.Parse("""{"host":"localhost","port":10223,"leaseId":"e2e-run1-0","label":"e2e mobileapp slot 1 of 1"}""")! });
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status(), port: 10223);

        await client.CallAsync(DeviceMethods.DevicesBoot, (JsonObject)JsonNode.Parse("""{"platform":"ios"}""")!, CancellationToken.None);

        Assert.All(agent.Calls.Concat(agent.LeaseCalls), call => Assert.Equal(10223, call.Uri.Port));
        Assert.Equal("e2e-run1-0", agent.Calls[0].Header("X-DevFlow-Lease"));
        Assert.Equal("e2e-run1-0", Assert.Single(agent.LeaseCalls).Body!["leaseId"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_worker_without_a_binding_finds_the_agent_on_its_first_call()
    {
        var (provider, agent) = Over();
        agent.ScriptJson("GET", "/api/agents", new JsonArray(Brokered[0]!.DeepClone()), port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status(), port: 10223);
        var client = provider.CreateClient("e2e-mobileapp-0", null);

        await client.CallAsync(DeviceMethods.DevicesBoot, (JsonObject)JsonNode.Parse("""{"platform":"ios"}""")!, CancellationToken.None);

        Assert.Equal(["GET /api/agents", "GET /api/v1/agent/status"], agent.Calls.Select(call => call.Route));
        Assert.Equal(10223, agent.Calls[1].Uri.Port);
        Assert.Equal("e2e-e2e-mobileapp-0", agent.Calls[1].Header("X-DevFlow-Lease"));
        Assert.Equal(10223, Assert.Single(agent.LeaseCalls).Uri.Port);
    }

    [Fact]
    public async Task A_worker_without_a_binding_picks_the_agent_of_the_platform_its_boot_names()
    {
        var (provider, agent) = Over();
        agent.ScriptJson("GET", "/api/agents", Brokered, port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status("android"), port: 10224);
        var client = provider.CreateClient("e2e-mobileapp-0", null);

        await client.CallAsync(DeviceMethods.DevicesBoot, (JsonObject)JsonNode.Parse("""{"platform":"android"}""")!, CancellationToken.None);

        Assert.Equal(10224, agent.Calls.Last().Uri.Port);
    }

    [Fact]
    public async Task A_worker_without_a_binding_or_a_broker_tries_the_default_port()
    {
        var (provider, agent) = Over();
        var client = provider.CreateClient("e2e-mobileapp-0", null);

        await client.CallAsync(DeviceMethods.DevicesBoot, (JsonObject)JsonNode.Parse("""{"platform":"ios"}""")!, CancellationToken.None);

        Assert.Equal(9223, agent.Calls.Last().Uri.Port);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void An_impossible_port_is_a_configuration_error(int port)
    {
        var error = Assert.Throws<ConfigurationException>(() => new DevFlowAgents(new DevFlowAgentsOptions { Port = port }));

        Assert.Equal("INVALID_CONFIG", error.Code);
    }

    [Fact]
    public void The_engine_takes_the_provider_as_a_client_source()
    {
        var engine = new MobileEngine(new MobileOptions { Platform = MobilePlatform.Ios, Provider = new DevFlowAgents(), App = new MobileApp { BundleId = FakeAgent.PackageId } });

        Assert.Equal("devflow", engine.Options.Provider!.Name);
        Assert.IsAssignableFrom<IDeviceClientSource>(engine.Options.Provider);
    }
}
