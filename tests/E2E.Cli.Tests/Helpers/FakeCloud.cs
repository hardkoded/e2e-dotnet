// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Mobile;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// A hosted device provider for the CLI tests: it counts its leases and what came back, and serves a scripted device with one button.
/// Live runs need Limrun credits or a DevFlow agent, so every test of a hosted session runs on this.
/// </summary>
internal sealed class FakeCloud : IDeviceProvider, IDeviceClientSource
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private int _acquired;
    private int _released;
    private int _opens;

    public string Name => "fake-cloud";

    public int Acquired => Volatile.Read(ref _acquired);

    public int Released => Volatile.Read(ref _released);

    /// <summary>Runs on the nth <c>apps.open</c> (1 is the warm-up in prepare, 2 the session's first launch); a throw fails it.</summary>
    public Func<int, CancellationToken, Task>? OnOpen { get; set; }

    public Func<Task>? OnAcquire { get; set; }

    public Func<Task>? OnRelease { get; set; }

    /// <summary>The engine of a session on this provider, over the device the target names.</summary>
    public MobileEngine Engine(TargetConfig target) => new(new MobileOptions
    {
        Platform = MobilePlatform.Ios,
        TargetName = target.Name,
        App = new MobileApp { BundleId = target.App.BundleId },
        Provider = this,
    });

    public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _acquired);
        return AcquiredAsync();

        async Task<DeviceLease> AcquiredAsync()
        {
            if (OnAcquire is { } wait)
            {
                await wait();
            }

            return new DeviceLease { Id = "lease-secret-id", Daemon = new DeviceDaemon("https://cloud.example", "daemon-secret-token"), Device = "cloud-iphone" };
        }
    }

    public async Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _released);
        if (OnRelease is { } wait)
        {
            await wait();
        }
    }

    public IDeviceClient CreateClient(string session, DeviceConnection? connection) => new Client(this);

    private sealed class Client(FakeCloud cloud) : IDeviceClient
    {
        public async Task<JsonNode?> CallAsync(string method, JsonObject? options, CancellationToken cancellationToken)
        {
            await Task.Yield();
            switch (method)
            {
                case DeviceMethods.AppsOpen:
                    if (cloud.OnOpen is { } open)
                    {
                        await open(Interlocked.Increment(ref cloud._opens), cancellationToken);
                    }

                    return new JsonObject { ["appName"] = "Greeter", ["appBundleId"] = "com.example.greeter" };
                case DeviceMethods.CaptureSnapshot:
                    RawNode[] nodes =
                    [
                        new() { Ref = "@e1", Index = 0, Depth = 0, Type = "application", Label = "Greeter", Rect = new Rect(0, 0, 390, 844) },
                        new() { Ref = "@e2", Index = 1, ParentIndex = 0, Depth = 1, Type = "button", Label = "Greet", Rect = new Rect(0, 100, 390, 44) },
                    ];
                    return new JsonObject { ["nodes"] = JsonSerializer.SerializeToNode(nodes, Web), ["appName"] = "Greeter", ["appBundleId"] = "com.example.greeter", ["truncated"] = false };
                case DeviceMethods.CaptureScreenshot:
                    return new JsonObject { ["logicalWidth"] = 390, ["logicalHeight"] = 844 };
                default:
                    return new JsonObject();
            }
        }
    }
}
