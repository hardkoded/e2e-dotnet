// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>
/// The app half of <c>open_session</c>'s summary (upstream's <c>mcp/app-lines.ts</c>): where the app is, the port the session assigned, and the
/// process it brought up for it, so an agent never reads the config to learn what is listening. Nothing that may carry a secret is printed:
/// <c>args</c> are left out, <c>env</c> and <c>inheritEnv</c> are named without values, and the readiness URL loses userinfo and query.
/// </summary>
internal static class AppLines
{
    /// <summary>
    /// The <c>App:</c> and <c>Process:</c> lines for a session (upstream's <c>appLines</c>; a C# class cannot hold a member of its own name).
    /// <paramref name="app"/> is the target's app as the session resolved it, and <paramref name="launchesPinnedApp"/> whether the session itself
    /// launches the app a device target pins (upstream reads it from the target).
    /// </summary>
    public static List<string> For(ResolvedApp app, bool launchesPinnedApp)
    {
        ArgumentNullException.ThrowIfNull(app);
        var lines = new List<string>();
        if (app.Base is { } appBase)
        {
            lines.Add($"App: {appBase.Href}{(app.PortRequest?.Port is { } assigned ? $" (port {assigned}, assigned for the declared port 0)" : "")}.");
        }
        else if (launchesPinnedApp && (app.BundleId ?? app.AppPath) is { } pinned)
        {
            lines.Add($"App: {pinned}, launched fresh.");
        }

        if (app.Command is { } command && app.ReadyUrl is { } readyUrl)
        {
            lines.Add($"Process: {DescribeCommand(command, readyUrl)} e2e starts no other app process for this session; a database or other service the app uses runs outside e2e.");
        }
        else if (app.Base is not null)
        {
            lines.Add("Process: none; the target declares no app.command, so the app runs outside e2e.");
        }

        return lines;
    }

    /// <summary>One app command as the session ran it: its executable, where it answers, and what it was given.</summary>
    private static string DescribeCommand(CommandConfig command, string readyUrl)
    {
        var facts = new List<string> { $"ready at {Urls.PrintableUrl(readyUrl)}" };
        if (command.ReuseExisting == true)
        {
            facts.Add("reuseExisting: a server already answering there is used instead of starting one");
        }

        if (command.Env is { Count: > 0 } env)
        {
            facts.Add($"env {string.Join(", ", env.Keys)}");
        }

        if (command.InheritEnv is { Count: > 0 } inherited)
        {
            facts.Add($"inherits {string.Join(", ", inherited)}");
        }

        if (command.Cwd is { } cwd)
        {
            facts.Add($"cwd {cwd}");
        }

        if (command.Log is { } log)
        {
            facts.Add($"log {log}");
        }

        return $"app.command {SessionCatalog.Quote(command.Executable)} ({string.Join("; ", facts)}).";
    }
}
