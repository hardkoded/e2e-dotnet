// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>
/// How to start each agent with e2e's step tools as its only tools. ACP lets a client add tools but not take the agent's own away, so each
/// preset turns them off the way its agent allows. The adapter runs from the install found from the working directory up, never fetched by
/// name at run time. Upstream's <c>presets.ts</c>.
/// </summary>
internal static class Presets
{
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

    /// <summary>How long <c>codex mcp list</c> may take.</summary>
    private static readonly TimeSpan CodexListTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Claude Code with no built-in tools, no user or project settings or MCP servers, and no saved session. Upstream's <c>claudeCodeLaunch</c>.</summary>
    public static Func<CancellationToken, Task<AgentLaunch>> ClaudeCodeLaunch(AcpAgentOptions options)
    {
        var bin = AdapterBin("@agentclientprotocol/claude-agent-acp", "claude-agent-acp");
        var env = new Dictionary<string, string>();

        // The adapter starts on ANTHROPIC_MODEL ahead of the user's own settings, whose model may not start outside their CLI.
        if (options.Model is not null)
        {
            env["ANTHROPIC_MODEL"] = options.Model;
        }

        foreach (var (name, value) in options.Env ?? new Dictionary<string, string>())
        {
            env[name] = value;
        }

        var launch = new AgentLaunch(
            "node",
            [bin],
            env,
            options.Model,
            SessionMeta: new Dictionary<string, object?>
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
            SignIn: "run `claude` and log in, or set ANTHROPIC_API_KEY");
        return _ => Task.FromResult(launch);
    }

    /// <summary>
    /// Codex in its read-only mode with no shell, web search, apps, plugins, browser or computer use, and the user's own MCP servers turned off
    /// by name, as <c>codex mcp list</c> reports them. The adapter merges this over the user's Codex config for its sessions only. Codex may
    /// still start a subagent, which fails the step like any tool of its own. Upstream's <c>codexLaunch</c>.
    /// </summary>
    public static Func<CancellationToken, Task<AgentLaunch>> CodexLaunch(AcpAgentOptions options)
    {
        var package = AdapterPackage("@agentclientprotocol/codex-acp");
        var bin = BinOf(package, "codex-acp");
        Task<AgentLaunch>? launch = null;
        var gate = new object();
        return token =>
        {
            Task<AgentLaunch> shared;
            lock (gate)
            {
                // A failed list is not kept: the next step lists again. The list belongs to no caller, so one caller's cancel
                // does not fail another; it is bounded by its own timeout.
                if (launch is not { IsFaulted: false, IsCanceled: false })
                {
                    launch = StartAsync(CancellationToken.None);
                }

                shared = launch;
            }

            return shared.WaitAsync(token);
        };

        async Task<AgentLaunch> StartAsync(CancellationToken token)
        {
            var servers = await CodexServersAsync(package, options.Env, token).ConfigureAwait(false);
            var env = new Dictionary<string, string>(options.Env ?? new Dictionary<string, string>())
            {
                ["CODEX_CONFIG"] = CodexConfig(servers).ToJsonString(),
            };
            return new AgentLaunch("node", [bin], env, options.Model, "read-only", SignIn: "run `codex login`");
        }
    }

    private static JsonObject CodexConfig(IEnumerable<string> servers)
    {
        var features = new JsonObject();
        foreach (var feature in CodexFeaturesOff)
        {
            features[feature] = false;
        }

        var mcpServers = new JsonObject();
        foreach (var name in servers)
        {
            mcpServers[name] = new JsonObject { ["enabled"] = false };
        }

        return new JsonObject { ["web_search"] = "disabled", ["features"] = features, ["mcp_servers"] = mcpServers };
    }

    /// <summary>The MCP servers the user's Codex config declares, from the Codex the adapter runs. Upstream's <c>codexServers</c>.</summary>
    private static async Task<IReadOnlyList<string>> CodexServersAsync(string adapter, IReadOnlyDictionary<string, string>? env, CancellationToken token)
    {
        try
        {
            var codexPath = env is not null && env.TryGetValue("CODEX_PATH", out var configured) ? configured : Environment.GetEnvironmentVariable("CODEX_PATH");
            var command = codexPath ?? "node";
            List<string> args = codexPath is null ? [ResolveCodexBin(adapter)] : [];
            args.AddRange(["mcp", "list", "--json"]);
            var stdout = await RunAsync(command, args, env, CodexListTimeout, token).ConfigureAwait(false);
            using var list = JsonDocument.Parse(stdout);
            return [.. list.RootElement.EnumerateArray().Select(server => server.GetProperty("name").GetString() ?? throw new JsonException("a server has no name"))];
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new InvalidOperationException($"could not list your Codex MCP servers to turn them off: {error.Message}", error);
        }
    }

    /// <summary>
    /// Runs a command to the end and returns its stdout, without a shell. Stdout and stderr are read together, the wait is bounded, and the
    /// process tree is killed on a timeout or a cancel. Upstream's <c>execFileAsync</c> with <c>timeout</c>.
    /// </summary>
    private static async Task<string> RunAsync(string command, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env, TimeSpan timeout, CancellationToken token)
    {
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        AcpSession.WithoutSecrets(start, env);
        using var process = new Process { StartInfo = start };
        process.Start();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(limit.Token);
        var stderr = process.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            var output = await stdout.ConfigureAwait(false);
            var errors = (await stderr.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0
                ? output
                : throw new InvalidOperationException($"{Path.GetFileName(command)} exited with code {process.ExitCode}{(errors.Length > 0 ? $": {errors}" : "")}");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException($"{Path.GetFileName(command)} did not finish in {timeout.TotalSeconds:0} s");
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException)
            {
                // Already exited, or part of the tree could not be killed.
            }

            // The reads end with the process or the limit; observe them so a failed one is not left unobserved.
            await Task.WhenAll(stdout, stderr).ContinueWith(static _ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <summary>The file Node resolves for <c>@openai/codex/bin/codex.js</c> from the adapter's directory.</summary>
    private static string ResolveCodexBin(string adapter)
    {
        for (var directory = adapter; directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var candidate = Path.Combine(directory, "node_modules", "@openai", "codex", "bin", "codex.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"@openai/codex is not installed for {adapter}");
    }

    /// <summary>
    /// The directory of an adapter package installed in the project, found the way Node finds a dependency, with links resolved: pnpm links a
    /// package into the project, and its own dependencies resolve only from where it is. Upstream's <c>adapterPackage</c>.
    /// </summary>
    private static string AdapterPackage(string name)
    {
        for (var directory = Directory.GetCurrentDirectory(); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var candidate = Path.Combine(directory, "node_modules", name);
            if (File.Exists(Path.Combine(candidate, "package.json")))
            {
                return new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? candidate;
            }
        }

        throw new ConfigurationException(
            "INVALID_CONFIG",
            $"{name} is not installed in {Directory.GetCurrentDirectory()} or a directory above it: npm install -D {name}, and run e2e from that project");
    }

    private static string AdapterBin(string name, string bin) => BinOf(AdapterPackage(name), bin);

    /// <summary>The file a package manifest's <c>bin</c> field names for <paramref name="bin"/>. Upstream's <c>binOf</c>.</summary>
    private static string BinOf(string package, string bin)
    {
        string? path = null;
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "package.json"))))
        {
            if (manifest.RootElement.TryGetProperty("bin", out var declared))
            {
                if (declared.ValueKind == JsonValueKind.String)
                {
                    path = declared.GetString();
                }
                else if (declared.ValueKind == JsonValueKind.Object && declared.TryGetProperty(bin, out var named) && named.ValueKind == JsonValueKind.String)
                {
                    path = named.GetString();
                }
            }
        }

        return path is null ? throw new ConfigurationException("INVALID_CONFIG", $"{package} has no {bin} command") : Path.Combine(package, path);
    }
}
