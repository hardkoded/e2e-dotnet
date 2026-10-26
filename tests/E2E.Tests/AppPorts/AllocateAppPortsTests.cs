// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Config;

namespace E2E.Tests.AppPorts;

/// <summary>
/// <c>describe('allocateAppPorts')</c> of <c>app-ports.test.ts</c>. The port runs one target, so there is no pair of targets to hand
/// distinct ports to, and <c>AppPorts.AllocateTargetPorts</c> is the only allocator it has.
/// </summary>
public sealed class AllocateAppPortsTests
{
    /// <summary>Partial: one target, so it checks that a port is reserved for a free-port URL and that fixed and absent URLs are left alone, not that two targets get distinct ports.</summary>
    [Fact]
    public void Partial_Reserves_a_free_port_per_target_and_leaves_the_rest_alone()
    {
        var root = Path.GetTempPath();
        var free = ResolveConfigTests.App("""{ "url": "http://127.0.0.1:0", "command": { "executable": "pnpm", "env": { "PORT": "{port}" } } }""");
        var a = E2E.AppPorts.AllocateTargetPorts("web", free.Target.App, root);
        var port = new Uri(a.Base!.Href).Port;
        Assert.True(port > 0);
        Assert.Equal("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), a.Base.Origin);
        Assert.Equal(port.ToString(System.Globalization.CultureInfo.InvariantCulture), a.Command!.Env!["PORT"]);
        Assert.Equal(port, a.PortRequest!.Port);

        var fixedApp = E2E.AppPorts.AllocateTargetPorts("web", ResolveConfigTests.App("""{ "url": "http://127.0.0.1:3000" }""").Target.App, root);
        Assert.Equal("http://127.0.0.1:3000", fixedApp.Base!.Origin);
        Assert.Null(fixedApp.PortRequest);
        Assert.Null(E2E.AppPorts.AllocateTargetPorts("web", ResolveConfigTests.App("{}").Target.App, root).Base);
    }
}
