// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Engine.Protocol;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe("permissions 'all'")</c> of <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>).
/// </summary>
public sealed class PermissionsAllTests
{
    [Fact]
    public async Task Carries_allPermissions_into_prepare_on_an_engine_that_declares_grantsAllPermissions()
    {
        var seen = new List<EngineAppDeclaration?>();
        var (_, remote) = Of(
            Toy(spec =>
                spec with
                {
                    GrantsAllPermissions = true,
                    Prepare = info =>
                    {
                        seen.Add(
                            new EngineAppDeclaration
                            {
                                BundleId = info.App!.BundleId,
                                AllPermissions = info.App.AllPermissions,
                            }
                        );
                        return Task.FromResult<EnginePrepareResult?>(null);
                    },
                }
            ),
            options => options with { ValidateApp = (app, _) => seen.Add(app) }
        );
        var declared = new TargetConfig
        {
            Name = "phone",
            Platform = "toy",
            App = new AppConfig { BundleId = "dev.shop.app", AllPermissions = true },
            EngineHandle = remote,
        };

        var target = ResolvedConfig.ResolveTarget(declared);
        await target.EngineHandle!.Prepare!(
            PrepareInfo("phone") with
            {
                App = TargetApps.EngineAppInfoOf(
                    TargetApps.ResolveTargetApp("phone", declared.App, Directory.GetCurrentDirectory())
                ),
            }
        );

        Assert.Equal(2, seen.Count);
        Assert.All(seen, app =>
        {
            Assert.Equal("dev.shop.app", app!.BundleId);
            Assert.True(app.AllPermissions);
        });
    }

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
