// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Engine.Protocol;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe('launch arguments')</c> of <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>).
/// Not ported: <c>hands an engine that predates the object form a list it can spread, and refuses an object at config load</c> and the config
/// half of <c>carries the object form to an engine that declares launchArgumentValues</c> (the port's <c>app.launchArguments</c> is a list
/// only, and its config types a target's engine as <see cref="IEngine"/>, which holds no <see cref="EngineHandle"/>);
/// <c>refuses a fileApps flag that is not a boolean</c> and <c>refuses a launchArgumentValues flag that is not a boolean</c> (the flags are
/// typed <c>bool?</c>, so a string does not compile).
/// </summary>
public sealed class LaunchArgumentsTests
{
    [Fact]
    public void Carries_fileApps_in_the_manifest_so_a_served_engine_opens_a_file_app_and_one_that_does_not_declare_it_is_refused()
    {
        var (local, remote) = Of(Toy(toy => toy with { FileApps = true }));
        Assert.True(ProtocolManifest.DescribeEngine(local).FileApps);
        Assert.True(remote.FileApps);

        var plain = Of(Toy());

        Assert.Null(ProtocolManifest.DescribeEngine(plain.Local).FileApps);
        Assert.NotEqual(true, plain.Remote.FileApps);
    }

    [Fact]
    public async Task Refuses_a_server_that_does_not_declare_the_fileApps_its_manifest_promised()
    {
        var local = E2E.Engine.EngineManifest.DefineEngine(Toy());
        var remote = ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = ProtocolManifest.DescribeEngine(local) with { FileApps = true },
                Transport = new LoopbackTransport(local),
            }
        );

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Init!(InitInfo()));

        Assert.Contains("fileApps is false, declared true", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream resolves a config target, sends its object-form <c>launchArguments</c> through <c>prepare</c>, and checks the
    /// engine reads them. The port's app has the list form only, so the port checks the flag the manifest carries.
    /// </summary>
    [Fact]
    public void Partial_Carries_the_object_form_to_an_engine_that_declares_launchArgumentValues()
    {
        var (local, remote) = Of(Toy(toy => toy with { LaunchArgumentValues = true }));

        Assert.True(ProtocolManifest.DescribeEngine(local).LaunchArgumentValues);
        Assert.True(remote.LaunchArgumentValues);
    }
}
