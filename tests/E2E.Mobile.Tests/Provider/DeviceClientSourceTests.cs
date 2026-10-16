// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.Tests.Provider;

public sealed class DeviceClientSourceTests
{
    /// <summary>A provider that makes its own clients: it names no daemon, so the engine has nothing to connect to without it.</summary>
    private sealed class OwnClients : IDeviceProvider, IDeviceClientSource
    {
        public string Name => "own-clients";

        public FakeDeviceClient Fake { get; } = new();

        public List<(string Session, DeviceConnection? Connection)> Made { get; } = [];

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceLease { Id = $"lease-{request.Slot}", Client = new JsonObject { ["handle"] = $"device-{request.Slot}" } });

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IDeviceClient CreateClient(string session, DeviceConnection? connection)
        {
            Made.Add((session, connection));
            return Fake;
        }
    }

    private static MobileEngine EngineOver(OwnClients provider) =>
        new(new MobileOptions { Platform = MobilePlatform.Ios, TargetName = "ios", App = new MobileApp { BundleId = "com.example.app" }, Provider = provider });

    [Fact]
    public async Task A_provider_that_makes_clients_drives_prepare_and_a_worker_through_them()
    {
        var provider = new OwnClients();
        var engine = EngineOver(provider);
        var env = new Dictionary<string, string?>();

        var prepared = await engine.PrepareAsync(
            new MobilePrepareInfo { RunId = "run-1", TargetName = "ios", ProjectRoot = Harness.ProjectRoot, Slots = 1, Env = env, App = engine.Options.App, Log = _ => { } },
            CancellationToken.None);

        // The warm-up went through the provider's client, with the lease's client configuration in the binding.
        Assert.Equal(["devices.boot", "command.prepare", "apps.open"], provider.Fake.Methods());
        Assert.Equal("device-0", provider.Made[0].Connection!.Client!["handle"]!.GetValue<string>());
        Assert.Null(provider.Made[0].Connection!.Daemon);

        // A worker in another process builds its engine from the same options and finds the device through the binding.
        var worker = EngineOver(provider);
        await using var session = await worker.StartAsync(
            new MobileStartInfo { RunId = "run-1", TargetName = "ios", WorkerSlot = 0, ProjectRoot = Harness.ProjectRoot, Env = Harness.WorkerEnv(prepared) },
            CancellationToken.None);

        Assert.Equal("device-0", provider.Made[^1].Connection!.Client!["handle"]!.GetValue<string>());
        Assert.Equal("e2e-ios-0", provider.Made[^1].Session);
        Assert.Equal("devices.boot", provider.Fake.Methods()[^1]);
    }
}
