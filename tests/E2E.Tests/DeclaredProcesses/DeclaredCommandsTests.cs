// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Declared = E2E.DeclaredProcesses;

namespace E2E.Tests.DeclaredProcesses;

/// <summary>
/// <c>tests/unit/declared-processes.test.ts</c>. The port runs one target per config, so each target here is a name and the app that
/// upstream's <c>resolveConfig</c> (and <c>assignPorts</c>) would resolve for it.
/// </summary>
public sealed class DeclaredCommandsTests
{
    private const string Root = "/tmp/e2e-declared-processes";

    private static readonly CommandConfig Dev = new() { Executable = "pnpm", Args = ["dev"] };

    // One target per app declaration, named t0, t1, ..., the way two browsers on one app would be configured; ports are those assigned by name.
    private static List<(string Name, ResolvedApp App)> ConfigOf(IReadOnlyDictionary<string, int>? ports, params AppConfig[] apps) =>
        [.. apps.Select((app, index) =>
        {
            var name = "t" + index.ToString(CultureInfo.InvariantCulture);
            return (name, TargetApps.ResolveTargetApp(name, app, Root, ports is not null && ports.TryGetValue(name, out var port) ? port : null));
        })];

    [Fact]
    public void Starts_identical_commands_once_in_target_order_probing_the_first_readyUrl()
    {
        var api = new CommandConfig { Executable = "pnpm", Args = ["api"] };
        var commands = Declared.DeclaredCommands(ConfigOf(
            null,
            new AppConfig { Url = "http://localhost:3000", Command = Dev },
            new AppConfig { Url = "http://localhost:3000/admin", Command = Dev },
            new AppConfig { Url = "http://localhost:4000", Command = api }));
        Assert.Collection(
            commands,
            first =>
            {
                Assert.Equal("target \"t0\" command", first.Label);
                Assert.Equal("pnpm", first.Command.Executable);
                Assert.Equal(Dev.Args, first.Command.Args);
                Assert.Equal("http://localhost:3000/", first.ReadyUrl);
                Assert.NotEmpty(first.Key);
            },
            second =>
            {
                Assert.Equal("target \"t2\" command", second.Label);
                Assert.Equal("pnpm", second.Command.Executable);
                Assert.Equal(api.Args, second.Command.Args);
                Assert.Equal("http://localhost:4000/", second.ReadyUrl);
                Assert.NotEmpty(second.Key);
            });
        Assert.Empty(Declared.DeclaredCommands(ConfigOf(null, new AppConfig { Url = "http://localhost:3000" }, new AppConfig())));
    }

    [Fact]
    public void Dedupes_commands_on_their_expanded_values_once_ports_are_assigned()
    {
        var shared = new AppConfig
        {
            Url = "http://127.0.0.1:0",
            Command = new CommandConfig { Executable = "pnpm", Args = ["dev", "--port", "{port}"], Env = new Dictionary<string, string> { ["PORT"] = "{port}" } },
        };
        var onePort = Declared.DeclaredCommands(ConfigOf(new Dictionary<string, int> { ["t0"] = 4321, ["t1"] = 4321 }, shared, shared));
        var only = Assert.Single(onePort);
        Assert.Equal("target \"t0\" command", only.Label);
        Assert.Equal("pnpm", only.Command.Executable);
        Assert.Equal(["dev", "--port", "4321"], only.Command.Args);
        Assert.Equal("4321", Assert.Single(only.Command.Env!).Value);
        Assert.Equal("http://127.0.0.1:4321/", only.ReadyUrl);

        // Two ports are two servers, each probed where it listens.
        var twoPorts = Declared.DeclaredCommands(ConfigOf(new Dictionary<string, int> { ["t0"] = 4321, ["t1"] = 4322 }, shared, shared));
        Assert.Equal(["http://127.0.0.1:4321/", "http://127.0.0.1:4322/"], twoPorts.Select(command => command.ReadyUrl));
    }

    [Fact]
    public void Gives_a_command_whose_port_0_was_allocated_per_run_a_key_of_its_own()
    {
        var declaration = new AppConfig { Url = "http://127.0.0.1:0", Command = new CommandConfig { Executable = "pnpm", Args = ["dev", "--port", "{port}"] } };
        var first = Declared.DeclaredCommands(ConfigOf(new Dictionary<string, int> { ["t0"] = 4321 }, declaration))[0];
        var again = Declared.DeclaredCommands(ConfigOf(new Dictionary<string, int> { ["t0"] = 4321 }, declaration))[0];
        var second = Declared.DeclaredCommands(ConfigOf(new Dictionary<string, int> { ["t0"] = 4322 }, declaration))[0];
        Assert.Equal(first.Key, again.Key);
        Assert.NotEqual(first.Key, second.Key);
    }
}
