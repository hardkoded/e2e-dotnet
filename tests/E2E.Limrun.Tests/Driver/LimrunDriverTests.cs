// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.Limrun.Tests.Driver;

/// <summary>
/// The driver against a scripted agent-device client. Ports <c>packages/limrun/tests/unit/driver.test.ts</c>. The first test is partial:
/// upstream asserts that the binding's <c>client</c> carries the lease session, and the port keeps <c>session</c> out of a binding's client
/// (the engine reserves it), so the test asserts the session the driver's clients are created with.
/// </summary>
public sealed class LimrunDriverTests
{
    [Theory]
    [InlineData(MobilePlatform.Ios)]
    [InlineData(MobilePlatform.Android)]
    public async Task Partial_Uses_the_requested_app_when_installation_returns_no_identifier(MobilePlatform platform)
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
        Assert.All(sessions, named => Assert.Matches("^e2e-lease-[0-9a-f]{8}$", named));
        Assert.Single(sessions.Distinct());
        JsonAssert.Args(
            $$"""{"tenant":"e2e","runId":"run","clientId":"worker-0","leaseBackend":"{{platform.WireName()}}-instance","leaseProvider":"limrun","retainOnClose":true}""",
            client.LastArgs(DeviceMethods.LeasesAllocate));
        JsonAssert.Args(
            $$"""{"appPath":"/build","{{(platform == MobilePlatform.Ios ? "udid" : "serial")}}":"limrun:{{platform.WireName()}}:lease","app":"com.example.app","platform":"{{platform.WireName()}}"}""",
            client.LastArgs(DeviceMethods.AppsInstall));
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
