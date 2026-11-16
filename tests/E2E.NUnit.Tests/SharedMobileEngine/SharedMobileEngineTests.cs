// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using E2E.Mobile;

namespace E2E.NUnit.Tests.SharedMobileEngine;

/// <summary>
/// .NET-only (upstream has no mobile adapter): the run fixture prepares a shared engine once per process, and that prepare leases the hosted
/// device. The host that makes the engine must not lease too: a second claim under another run id is refused by the DevFlow agent as another
/// host's lease, and a Limrun device would be leased twice.
/// </summary>
[NonParallelizable]
public sealed class SharedMobileEngineTests
{
    [Test]
    public async Task Makes_the_shared_engine_without_leasing_the_device()
    {
        using var agent = new TcpListener(IPAddress.Loopback, 0);
        agent.Start();
        var port = ((IPEndPoint)agent.LocalEndpoint).Port;
        var provider = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER");
        var devflowPort = Environment.GetEnvironmentVariable("E2E_DEVFLOW_PORT");
        Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", "devflow");
        Environment.SetEnvironmentVariable("E2E_DEVFLOW_PORT", port.ToString());
        try
        {
            // A host that prepares the engine waits for the agent here, up to the connect timeout, so the wait is bounded.
            await Task.Run(() => MobileAppHost.ShareEngine(hosted => new MobileOptions
            {
                Platform = MobilePlatform.Ios,
                Provider = hosted,
                TargetName = "mobileapp",
                App = new MobileApp { BundleId = "dev.example.app" },
            })).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(MobileAppHost.Engine, Is.Not.Null);
            Assert.That(MobileAppHost.AttachesToRunningApp, Is.True);
            Assert.That(agent.Pending(), Is.False, "the host reached the DevFlow agent");
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", provider);
            Environment.SetEnvironmentVariable("E2E_DEVFLOW_PORT", devflowPort);
        }
    }
}
