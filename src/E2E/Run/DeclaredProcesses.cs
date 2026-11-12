// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E;

/// <summary>One app command to start, named after the first target that declared it (upstream's <c>DeclaredCommand</c>).</summary>
/// <param name="Label">The first declaring target's name for the command, <c>target "web" command</c>.</param>
/// <param name="Command">The command to start.</param>
/// <param name="ReadyUrl">The readiness probe of the first declaring target; a shared server is up when it answers.</param>
/// <param name="Key">
/// The command's identity: how it spawns, not where it is probed. A URL declared with port 0 spawns with its allocated port, so each allocation
/// is its own.
/// </param>
internal sealed record DeclaredCommand(string Label, CommandConfig Command, string ReadyUrl, string Key);

/// <summary>
/// The app commands a run owns, gathered from every target's <c>app</c> (upstream's <c>run/declared-processes.ts</c>). Declarations are
/// deduplicated across targets, so two browsers on one dev server share one process.
/// </summary>
internal static class DeclaredProcesses
{
    /// <summary>
    /// Every distinct app command in target order; identity is the command alone, not where it is probed. A target is its name and its
    /// resolved app (the port has no <c>ResolvedTarget</c>).
    /// </summary>
    public static IReadOnlyList<DeclaredCommand> DeclaredCommands(IEnumerable<(string Name, ResolvedApp App)> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var commands = new List<DeclaredCommand>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, app) in targets)
        {
            if (app.Command is not { } command || app.ReadyUrl is not { } readyUrl)
            {
                continue;
            }

            var key = Ids.CanonicalDigest(CommandIdentity(command));
            if (!keys.Add(key))
            {
                continue;
            }

            commands.Add(new DeclaredCommand("target \"" + name + "\" command", command, readyUrl, key));
        }

        return commands;
    }

    // The command as upstream's canonicalDigest reads it: every field it sets, none it leaves out.
    private static JsonObject CommandIdentity(CommandConfig command)
    {
        var identity = new JsonObject { ["executable"] = command.Executable };
        if (command.Args is { } args)
        {
            identity["args"] = new JsonArray([.. args.Select(arg => (JsonNode?)JsonValue.Create(arg))]);
        }

        if (command.Cwd is not null)
        {
            identity["cwd"] = command.Cwd;
        }

        if (command.Env is { } env)
        {
            var values = new JsonObject();
            foreach (var pair in env)
            {
                values[pair.Key] = pair.Value;
            }

            identity["env"] = values;
        }

        if (command.InheritEnv is { } inheritEnv)
        {
            identity["inheritEnv"] = new JsonArray([.. inheritEnv.Select(name => (JsonNode?)JsonValue.Create(name))]);
        }

        if (command.StartupTimeout is { } startup)
        {
            identity["startupTimeout"] = (long)startup.TotalMilliseconds;
        }

        if (command.ShutdownTimeout is { } shutdown)
        {
            identity["shutdownTimeout"] = (long)shutdown.TotalMilliseconds;
        }

        if (command.Log is not null)
        {
            identity["log"] = command.Log;
        }

        if (command.ReuseExisting is { } reuse)
        {
            identity["reuseExisting"] = reuse;
        }

        return identity;
    }
}
