// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Engine.Protocol;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe('launch arguments')</c> of <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>).
/// The <c>Partial_</c> test that carries the object form checks the declaration an <see cref="IEngine"/> reads, not one a client carries to a remote handle.
/// Not ported: <c>refuses a fileApps flag that is not a boolean</c>, whose flag is typed <c>bool?</c> so a string does not compile.
/// </summary>
public sealed class LaunchArgumentsTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("engine-manifest-v1.schema.json");

    private static TargetConfig Phone(AppConfig app) =>
        new()
        {
            Name = "phone",
            Platform = "ios",
            App = app,
        };

    private static IEngine Create(TargetConfig target, IEngine engine) =>
        TargetEngines.Create(target, headless: true, mobile: _ => engine);

    private sealed class ToyEngine(bool launchArgumentValues, List<EngineAppDeclaration> seen) : IEngine
    {
        public string Name => "toy";

        public string Platform => "ios";

        public string Version => "1.0.0";

        public EngineCapabilities Capabilities => EngineCapabilities.PinnedApp;

        public bool LaunchArgumentValues => launchArgumentValues;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        void IEngine.ValidateApp(EngineAppDeclaration app, EngineAppCheckInfo info) => seen.Add(app);
    }

    private static TargetConfig Declared(EngineHandle engine, AppConfig app) =>
        new()
        {
            Name = "phone",
            Platform = "toy",
            App = app,
            EngineHandle = engine,
        };

    [Fact]
    public async Task Hands_an_engine_that_predates_the_object_form_a_list_it_can_spread_and_refuses_an_object_at_config_load()
    {
        var spread = new List<string[]>();
        var legacy = E2E.Engine.EngineManifest.DefineEngine(
            Toy(spec =>
                spec with
                {
                    ValidateApp = (app, _) => spread.Add([.. app.LaunchArguments ?? []]),
                    Prepare = info =>
                    {
                        spread.Add([.. info.App!.LaunchArguments ?? []]);
                        return Task.FromResult<EnginePrepareResult?>(null);
                    },
                }
            )
        );
        var declared = Declared(
            legacy,
            new AppConfig { BundleId = "dev.shop.app", LaunchArguments = ["-AppleLocale", "fr_FR"] }
        );
        var target = ResolvedConfig.ResolveTarget(declared);
        await target.EngineHandle!.Prepare!(
            PrepareInfo("phone") with
            {
                App = TargetApps.EngineAppInfoOf(
                    TargetApps.ResolveTargetApp("phone", declared.App, Directory.GetCurrentDirectory())
                ),
            }
        );
        Assert.Equal(2, spread.Count);
        Assert.All(spread, arguments => Assert.Equal(["-AppleLocale", "fr_FR"], arguments));

        var refused = Assert.Throws<ConfigurationException>(() =>
            ResolvedConfig.ResolveTarget(
                Declared(
                    legacy,
                    new AppConfig
                    {
                        BundleId = "dev.shop.app",
                        LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>
                        {
                            ["AppleLocale"] = "fr_FR",
                            ["AppleLanguages"] = new[] { "fr" },
                        },
                    }
                )
            )
        );
        Assert.Equal("INVALID_CONFIG", refused.Code);
        Assert.Equal(
            "target \"phone\" app.launchArguments is an object, and engine \"toy\" takes only a list of strings: write the arguments as a list, or use an engine version that supports the object syntax",
            refused.Message
        );
        Assert.Equal(2, spread.Count);
    }

    /// <summary>Partial: upstream carries the declaration through <c>connectEngine</c> over a loopback transport to a remote handle; the port has no client, so it checks the manifest flag and the declaration the engine reads.</summary>
    [Fact]
    public void Partial_Carries_the_object_form_to_an_engine_that_declares_launchArgumentValues()
    {
        var handle = E2E.Engine.EngineManifest.DefineEngine(
            new EngineSpec
            {
                Name = "toy",
                Version = "1.0.0",
                SpiVersion = 1,
                LaunchArgumentValues = true,
                Observe = (_, _) => Task.FromResult<EngineSnapshot>(null!),
            }
        );
        Assert.True(ProtocolManifest.DescribeEngine(handle).LaunchArgumentValues);

        var seen = new List<EngineAppDeclaration>();
        var engine = new ToyEngine(true, seen);
        Create(
            Phone(
                new AppConfig
                {
                    BundleId = "dev.shop.app",
                    LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>
                    {
                        ["AppleLocale"] = "fr_FR",
                        ["AppleLanguages"] = new[] { "fr" },
                        ["e2e"] = true,
                        ["retries"] = 3,
                        ["token"] = null,
                    },
                }
            ),
            engine
        );
        var declaration = seen.Single();
        Assert.Null(declaration.LaunchArguments);
        Assert.Equal(["AppleLocale", "AppleLanguages", "e2e", "retries"], declaration.LaunchArgumentValues!.Keys);
        Assert.Equal(["fr"], declaration.LaunchArgumentValues["AppleLanguages"].Items);
        Assert.Equal(3, declaration.LaunchArgumentValues["retries"].Number);

        seen.Clear();
        Create(Phone(new AppConfig { BundleId = "dev.shop.app", LaunchArguments = ["-e2e", "YES"] }), engine);
        Assert.Equal(["-e2e", "YES"], seen.Single().LaunchArguments);
        Assert.Null(seen.Single().LaunchArgumentValues);
    }

    /// <summary>Partial: the flag is a <c>bool?</c> in C#, so <c>'yes'</c> cannot be declared; a manifest that spells it so is refused by the published schema.</summary>
    [Fact]
    public void Partial_Refuses_a_launchArgumentValues_flag_that_is_not_a_boolean()
    {
        Assert.False(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "launchArgumentValues": "yes" }"""
                )!
            )
        );
        Assert.True(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "launchArgumentValues": true }"""
                )!
            )
        );
    }

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
}
