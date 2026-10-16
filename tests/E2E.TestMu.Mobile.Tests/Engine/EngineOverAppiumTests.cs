// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Engine;

public sealed class EngineOverAppiumTests
{
    private const string Project = "/project";

    /// <summary>A provider that leases one session on the fake hub and makes the Appium client for it, as the real one will.</summary>
    private sealed class FakeTestMu(FakeAppium hub) : IDeviceProvider, IDeviceClientSource
    {
        public string Name => "testmu";

        public List<DeviceConnection?> Connections { get; } = [];

        public int Released { get; private set; }

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceLease { Id = FakeAppium.SessionId, Client = new JsonObject { ["testmu"] = new JsonObject { ["sessionId"] = FakeAppium.SessionId } } });

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
        {
            Released++;
            return hub.Driver().DeleteSessionAsync(cancellationToken);
        }

        public IDeviceClient CreateClient(string session, DeviceConnection? connection)
        {
            Connections.Add(connection);
            var id = connection?.Client?["testmu"]?["sessionId"]?.GetValue<string>() ?? throw new InvalidOperationException("no TestMu session in the binding");
            return new AppiumDeviceClient(new WebDriverClient(hub.Http, FakeAppium.Hub, id, null), MobilePlatform.Ios);
        }
    }

    private static MobileEngine EngineOver(FakeTestMu provider) =>
        new(new MobileOptions { Platform = MobilePlatform.Ios, TargetName = "ios", App = new MobileApp { BundleId = "com.example.app" }, Provider = provider });

    [Fact]
    public async Task Prepare_warms_the_leased_session_the_worker_drives_it_and_only_the_release_ends_it()
    {
        var hub = new FakeAppium();
        var provider = new FakeTestMu(hub);
        var engine = EngineOver(provider);
        var env = new Dictionary<string, string?>();

        var prepared = await engine.PrepareAsync(
            new MobilePrepareInfo { RunId = "run-1", TargetName = "ios", ProjectRoot = Project, Slots = 1, Env = env, App = engine.Options.App, Log = _ => { } },
            CancellationToken.None);

        // The warm-up boots the device and opens the app through the lease's own session.
        Assert.Equal(["GET window/rect", "POST execute/sync"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal("mobile: activateApp", hub.Calls[1].Script);
        Assert.Equal("sess-1", provider.Connections[0]!.Client!["testmu"]!["sessionId"]!.GetValue<string>());
        hub.Calls.Clear();

        // A worker is another engine over the same options: it finds the session through the binding the lease left.
        var worker = EngineOver(provider);
        await using (var session = await worker.StartAsync(
            new MobileStartInfo { RunId = "run-1", TargetName = "ios", WorkerSlot = 0, ProjectRoot = Project, Env = prepared.Env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value) },
            CancellationToken.None))
        {
            await session.Device.HomeAsync();
        }

        Assert.Equal("sess-1", provider.Connections[^1]!.Client!["testmu"]!["sessionId"]!.GetValue<string>());
        Assert.Equal(["GET window/rect", "POST execute/sync"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal("mobile: pressButton", hub.Calls[1].Script);

        // Closing the attempt's session did not delete the leased one.
        Assert.DoesNotContain(hub.Calls, call => call.Method == HttpMethod.Delete);

        await engine.FinishAsync(new MobileFinishInfo { RunId = "run-1", TargetName = "ios", Env = env, Log = _ => { } }, CancellationToken.None);

        Assert.Equal(1, provider.Released);
        Assert.Single(hub.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task A_refused_device_command_reaches_the_test_as_an_unsupported_capability()
    {
        var hub = new FakeAppium();
        var provider = new FakeTestMu(hub);
        var engine = EngineOver(provider);
        var env = new Dictionary<string, string?>();
        var prepared = await engine.PrepareAsync(
            new MobilePrepareInfo { RunId = "run-1", TargetName = "ios", ProjectRoot = Project, Slots = 1, Env = env, App = engine.Options.App, Log = _ => { } },
            CancellationToken.None);
        await using var session = await engine.StartAsync(
            new MobileStartInfo { RunId = "run-1", TargetName = "ios", WorkerSlot = 0, ProjectRoot = Project, Env = prepared.Env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value) },
            CancellationToken.None);

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.Device.SetAppearanceAsync(Appearance.Dark));

        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
    }
}
