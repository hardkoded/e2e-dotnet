// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class DeviceFixtureUnstableRawTests
{
    private const string BootedId = "5A1C0DE5-0000-4000-8000-000000000001";

    private static JsonObject Booted() => new() { ["platform"] = "ios", ["id"] = BootedId, ["device"] = "iPhone 17 Pro", ["booted"] = true };

    [Fact]
    public async Task Names_the_session_the_device_agent_device_booted_and_hands_out_the_session_client()
    {
        var h = Harness.Create(options => options with { Device = "iPhone 17 Pro" });
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => Booted());
        var session = await OpenAttempt(h);
        var raw = session.Device.UnstableRaw;
        Assert.Equal(MobilePlatform.Ios, raw.Platform);
        Assert.Equal("e2e-ios-simulator-0", raw.Session);
        Assert.Equal(BootedId, raw.DeviceId);
        Assert.Equal("iPhone 17 Pro", raw.DeviceName);
        Assert.Equal(new DeviceSelection(MobilePlatform.Ios, Device: "iPhone 17 Pro"), raw.Selection);
        Assert.Same(h.Fake, raw.Client);

        // Not ported: upstream also asserts the fixture never declares `unstable_raw` to the harness recorder as a step. The port has no fixture
        // step recorder to assert on.
        var before = h.Fake.Calls.Count;
        await raw.Client.CallAsync("apps.list", raw.Selection.ToArgs(), CancellationToken.None);
        JsonAssert.Calls(h.Fake.Calls, before, ("apps.list", """{"platform":"ios","device":"iPhone 17 Pro"}"""));
    }

    [Fact]
    public async Task Follows_the_session_option_and_selects_a_configured_UDID_by_id()
    {
        var h = Harness.Create(options => options with { Device = BootedId, Session = "host" });
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => Booted());
        var session = await OpenAttempt(h);
        var raw = session.Device.UnstableRaw;
        Assert.Equal("host-0", raw.Session);
        Assert.Equal(new DeviceSelection(MobilePlatform.Ios, Udid: BootedId), raw.Selection);
    }

    [Fact]
    public async Task Is_INVALID_STATE_once_the_worker_disposed_the_session()
    {
        var h = Harness.Create();
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => Booted());
        var session = await OpenAttempt(h);
        var device = session.Device;
        var handle = device.UnstableRaw;
        await session.CloseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal("INVALID_STATE", Assert.Throws<EngineException>(() => device.UnstableRaw).Code);
        Assert.Equal("INVALID_STATE", Assert.Throws<EngineException>(() => handle.Client).Code);
    }
}
