// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Engine.Protocol;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe("permissions 'all'")</c> of <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>).
/// Not ported: <c>carries allPermissions into prepare on an engine that declares grantsAllPermissions</c> (the port's app has no
/// <c>permissions: 'all'</c> and <c>EngineAppInfo.AllPermissions</c>, and its config holds no <see cref="EngineHandle"/> target to resolve).
/// </summary>
public sealed class PermissionsAllTests
{
    [Fact]
    public async Task Refuses_a_server_that_does_not_declare_the_grantsAllPermissions_its_manifest_promised()
    {
        var local = E2E.Engine.EngineManifest.DefineEngine(Toy());
        var remote = ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = ProtocolManifest.DescribeEngine(local) with { GrantsAllPermissions = true },
                Transport = new LoopbackTransport(local),
            }
        );

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Init!(InitInfo()));

        Assert.Contains("grantsAllPermissions is false, declared true", refused.Message, StringComparison.Ordinal);
    }
}
