// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.Limrun.Tests.Driver;

/// <summary>
/// The driver against a scripted agent-device client. Ports <c>packages/limrun/tests/unit/driver.test.ts</c>. Upstream's first test no
/// longer names the lease session in the binding's client, so the port asserts the session the driver's clients are created with.
/// </summary>
public sealed class LimrunDriverTests
{
    [Theory]
    [InlineData(MobilePlatform.Ios)]
    [InlineData(MobilePlatform.Android)]
    public async Task Uses_the_requested_app_when_installation_returns_no_identifier(MobilePlatform platform)
    {
        var client = new FakeDeviceClient();
        client.Respond(DeviceMethods.LeasesAllocate, _ => new JsonObject
        {
            ["retainOnClose"] = true,
            ["tenantId"] = "e2e",
            ["runId"] = "run",
            ["leaseId"] = "lease",
            ["backend"] = "ios-instance",
        });
        var sessions = new List<string>();
        client.Respond(DeviceMethods.AppsInstall, _ => new JsonObject());

        var binding = await LimrunDriver.AttachAsync(
            new LimrunDriverRequest("/daemon", "run", 0, platform, "com.example.app", "/build"),
            (named, _) =>
            {
                sessions.Add(named);
                return client;
            },
            CancellationToken.None);

        Assert.Equal("com.example.app", binding.InstalledApp);
        Assert.Equal($"limrun:{platform.WireName()}:lease", binding.DeviceId);
        Assert.NotEmpty(sessions);
        Assert.All(sessions, named => Assert.Matches(@"^e2e-lease-[0-9a-f]{8}\z", named));
        Assert.Single(sessions.Distinct());
        JsonAssert.Args(
            $$"""{"tenant":"e2e","runId":"run","clientId":"worker-0","leaseBackend":"{{platform.WireName()}}-instance","leaseProvider":"limrun","retainOnClose":true}""",
            client.LastArgs(DeviceMethods.LeasesAllocate));
        JsonAssert.Args(
            $$"""{"appPath":"/build","{{(platform == MobilePlatform.Ios ? "udid" : "serial")}}":"limrun:{{platform.WireName()}}:lease","app":"com.example.app","platform":"{{platform.WireName()}}"}""",
            client.LastArgs(DeviceMethods.AppsInstall));
    }

    [Fact]
    public async Task Names_a_session_for_its_own_clients_and_keeps_it_out_of_the_client_configuration_the_engine_receives()
    {
        var client = new FakeDeviceClient();
        client.Respond(DeviceMethods.LeasesAllocate, _ => new JsonObject
        {
            ["retainOnClose"] = true,
            ["tenantId"] = "e2e",
            ["runId"] = "run",
            ["leaseId"] = "lease",
            ["backend"] = "ios-instance",
        });
        client.Respond(DeviceMethods.AppsInstall, _ => new JsonObject { ["appId"] = "com.example.app" });
        var created = new List<(string Session, JsonObject? Client)>();

        var binding = await LimrunDriver.AttachAsync(
            new LimrunDriverRequest("/daemon", "run", 0, MobilePlatform.Ios, null, "/build"),
            (session, connection) =>
            {
                created.Add((session, connection!.Client));
                return client;
            },
            CancellationToken.None);

        // Exact: the engine rejects a lease whose client names `session` or the daemon connection.
        JsonAssert.Args(
            """{"stateDir":"/daemon","tenant":"e2e","runId":"run","leaseId":"lease","leaseBackend":"ios-instance","leaseProvider":"limrun"}""",
            binding.Client);
        Assert.Equal("limrun:ios:lease", binding.DeviceId);
        Assert.Equal("com.example.app", binding.InstalledApp);
        Assert.Collection(
            created,
            allocator =>
            {
                Assert.Equal("e2e-lease-06e36932", allocator.Session);
                JsonAssert.Args("""{"stateDir":"/daemon"}""", allocator.Client);
            },
            worker =>
            {
                Assert.Equal("e2e-lease-06e36932", worker.Session);
                JsonAssert.Args(
                    """{"stateDir":"/daemon","tenant":"e2e","runId":"run","leaseId":"lease","leaseBackend":"ios-instance","leaseProvider":"limrun"}""",
                    worker.Client);
            });
    }

    [Fact]
    public async Task Scopes_the_worker_client_to_the_lease_the_daemon_allocated()
    {
        var client = new FakeDeviceClient();
        client.Respond(DeviceMethods.LeasesAllocate, _ => new JsonObject
        {
            ["retainOnClose"] = true,
            ["tenantId"] = "e2e",
            ["runId"] = "run",
            ["leaseId"] = "lease",
            ["backend"] = "ios-instance",
            ["deviceKey"] = "key",
            ["clientId"] = "worker-3",
        });

        var binding = await LimrunDriver.AttachAsync(
            new LimrunDriverRequest("/daemon", "run", 3, MobilePlatform.Ios, null, null),
            (_, _) => client,
            CancellationToken.None);

        JsonAssert.Args(
            """{"stateDir":"/daemon","tenant":"e2e","runId":"run","leaseId":"lease","leaseBackend":"ios-instance","leaseProvider":"limrun","deviceKey":"key","clientId":"worker-3"}""",
            binding.Client);
    }
}
