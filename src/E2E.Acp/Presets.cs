// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>
/// How to start each agent with e2e's step tools as its only tools (upstream's <c>presets.ts</c>). ACP lets a client add tools but not
/// take the agent's own away, so each preset turns them off the way its agent allows. The adapter runs from the install found from the
/// working directory up, never fetched by name at run time. The adapters are npm packages, so the agent runs on the <c>node</c> on
/// the path (.NET-only: upstream runs it on the Node that runs e2e).
/// </summary>
internal static class Presets
{
    private static readonly TimeSpan CodexListTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The Codex features that give the agent a tool of its own.</summary>
    private static readonly string[] CodexFeaturesOff =
    [
        "apps",
        "browser_use",
        "browser_use_external",
        "computer_use",
        "goals",
        "image_generation",
        "multi_agent",
        "multi_agent_v2",
        "plugins",
        "shell_tool",
        "skill_search",
        "tool_suggest",
        "unified_exec",
        "view_image",
    ];

    /// <summary>Claude Code with no built-in tools, no user or project settings or MCP servers, and no saved session.</summary>
    public static Func<Task<AgentLaunch>> ClaudeCodeLaunch(AcpAgentOptions options)
    {
        var bin = AdapterBin("@agentclientprotocol/claude-agent-acp", "claude-agent-acp");
        // The adapter starts on ANTHROPIC_MODEL ahead of the user's own settings, whose model may not start outside their CLI.
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (options.Model is not null)
        {
            env["ANTHROPIC_MODEL"] = options.Model;
        }

        foreach (var (name, value) in options.Env ?? new Dictionary<string, string>())
        {
            env[name] = value;
        }

        var launch = new AgentLaunch
        {
            Command = NodeCommand,
            Args = [bin],
            Env = env,
            Model = options.Model,
            SessionMeta = new Dictionary<string, object?>
            {
                ["claudeCode"] = new Dictionary<string, object?>
                {
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["tools"] = Array.Empty<string>(),
                        ["settingSources"] = Array.Empty<string>(),
                        ["strictMcpConfig"] = true,
                        ["persistSession"] = false,
                        ["allowDangerouslySkipPermissions"] = false,
                    },
                },
            },
            SignIn = "run `claude` and log in, or set ANTHROPIC_API_KEY",
        };
        return () => Task.FromResult(launch);
    }

    /// <summary>
    /// Codex in its read-only mode with no shell, web search, apps, plugins, browser or computer use, and the user's own MCP servers
    /// turned off by name, as <c>codex mcp list</c> reports them. The adapter merges this over the user's Codex config for its sessions
    /// only. Codex may still start a subagent, which fails the step like any tool of its own.
    /// </summary>
    public static Func<Task<AgentLaunch>> CodexLaunch(AcpAgentOptions options)
    {
        var package = AdapterPackage("@agentclientprotocol/codex-acp");
        var bin = BinOf(package, "codex-acp");
        Task<AgentLaunch>? launch = null;
        var gate = new object();
        return () =>
        {
            lock (gate)
            {
                launch ??= Task.Run(async () =>
                {
                    var servers = await CodexServersAsync(package, options.Env).ConfigureAwait(false);
                    var env = new Dictionary<string, string>(options.Env ?? new Dictionary<string, string>(), StringComparer.Ordinal)
                    {
                        ["CODEX_CONFIG"] = CodexConfig(servers).ToJsonString(),
                    };
                    return new AgentLaunch { Command = NodeCommand, Args = [bin], Env = env, Model = options.Model, Mode = "read-only", SignIn = "run `codex login`" };
                });
                var current = launch;
                _ = current.ContinueWith(
                    failed =>
                    {
                        lock (gate)
                        {
                            if (ReferenceEquals(launch, failed))
                            {
                                launch = null;
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
                return current;
            }
        };
    }

    internal static JsonObject CodexConfig(IEnumerable<string> servers)
    {
        var features = new JsonObject();
        foreach (var feature in CodexFeaturesOff)
        {
            features[feature] = false;
        }

        var mcp = new JsonObject();
        foreach (var name in servers)
        {
            mcp[name] = new JsonObject { ["enabled"] = false };
        }

        return new JsonObject { ["web_search"] = "disabled", ["features"] = features, ["mcp_servers"] = mcp };
    }

    private static string NodeCommand => "node";

    /// <summary>The MCP servers the user's Codex config declares, from the Codex the adapter runs.</summary>
    private static async Task<List<string>> CodexServersAsync(string adapter, IReadOnlyDictionary<string, string>? env)
    {
        var codexPath = env is not null && env.TryGetValue("CODEX_PATH", out var configured) ? configured : Environment.GetEnvironmentVariable("CODEX_PATH");
        string command;
        var args = new List<string>();
        if (codexPath is null)
        {
            command = NodeCommand;
            args.Add(ResolveFrom(adapter, Path.Combine("@openai", "codex", "bin", "codex.js"))
                ?? throw new InvalidOperationException("could not list your Codex MCP servers to turn them off: @openai/codex is not installed beside the adapter"));
        }
        else
        {
            command = codexPath;
        }

        args.AddRange(["mcp", "list", "--json"]);
        try
        {
            var stdout = await RunAsync(command, args, env).ConfigureAwait(false);
            using var document = JsonDocument.Parse(stdout);
            return [.. document.RootElement.EnumerateArray().Select(server => server.GetProperty("name").GetString()!)];
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            throw new InvalidOperationException("could not list your Codex MCP servers to turn them off: " + error.Message, error);
        }
    }

    // Reads stdout and stderr together, bounded by a timeout that kills the process tree.
    private static async Task<string> RunAsync(string command, List<string> args, IReadOnlyDictionary<string, string>? env)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            merged[(string)entry.Key] = (string)(entry.Value ?? "");
        }

        foreach (var (name, value) in env ?? new Dictionary<string, string>())
        {
            merged[name] = value;
        }

        info.Environment.Clear();
        foreach (var (name, value) in AcpConnection.WithoutSecrets(merged))
        {
            info.Environment[name] = value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("could not start " + command);
        using var timeout = new CancellationTokenSource(CodexListTimeout);
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await stdout.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(command + " exited with code " + process.ExitCode + ": " + (await stderr.ConfigureAwait(false)).Trim());
            }

            return output;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(command + " did not answer within " + CodexListTimeout.TotalSeconds + " s");
        }
    }

    /// <summary>
    /// The directory of an adapter package installed in the project, found the way Node finds a dependency, with links resolved: pnpm
    /// links a package into the project, and its own dependencies resolve only from where it is.
    /// </summary>
    internal static string AdapterPackage(string name)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (; directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "node_modules", name);
            if (File.Exists(Path.Combine(candidate, "package.json")))
            {
                return new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? candidate;
            }
        }

        throw new ConfigurationException(
            "INVALID_CONFIG",
            name + " is not installed in " + Directory.GetCurrentDirectory() + " or a directory above it: npm install -D " + name + ", and run e2e from that project");
    }

    // A file of a package some node_modules at or above the directory holds.
    private static string? ResolveFrom(string directory, string file)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "node_modules", file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string AdapterBin(string name, string bin) => BinOf(AdapterPackage(name), bin);

    internal static string BinOf(string package, string bin)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "package.json")));
        string? path = null;
        if (manifest.RootElement.TryGetProperty("bin", out var declared))
        {
            path = declared.ValueKind switch
            {
                JsonValueKind.String => declared.GetString(),
                JsonValueKind.Object when declared.TryGetProperty(bin, out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                _ => null,
            };
        }

        return path is null
            ? throw new ConfigurationException("INVALID_CONFIG", package + " has no " + bin + " command")
            : Path.Combine(package, path);
    }
}
