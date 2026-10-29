// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli;

/// <summary>
/// Upstream's <c>cli/init/flags.ts</c>: the <c>e2e init</c> flags, one per wizard question. A location list that names an
/// unknown location or combines <c>none</c> with others, and an <c>--endpoint</c> next to another gateway, fail as usage
/// errors before anything is written. The port reads the arguments itself where upstream uses commander.
/// </summary>
internal static class InitFlags
{
    private static readonly string[] SkillDirs = [.. AgentSkill.SkillLocations.Select(location => location.Dir)];

    private static readonly string[] McpFiles = [.. McpConfig.McpLocations.Select(location => location.File)];

    public const string Usage = """
        e2e init [directory] [--yes] [--engine <ids>] [--gateway <id>] [--endpoint <url>] [--skills <dirs>] [--mcp <files>]

        Scaffolds e2e.config.json, installs the agent skill, registers the MCP server, and updates .gitignore. Existing files are never overwritten.
          --yes             accept the defaults for every question a flag does not answer, and ask nothing
          --engine <ids>    engine for a new config: web, mobile, or none
          --gateway <id>    model gateway for agent steps in a new config: vercel, openrouter, openai-compatible, chatgpt, copilot, opencode-console, grok, or none
          --endpoint <url>  base URL of the API for --gateway openai-compatible, which it implies
          --skills <dirs>   where to install the agent skill, comma-separated: .agents/skills, .claude/skills, or none
          --mcp <files>     where to register the MCP server, comma-separated: .mcp.json, .cursor/mcp.json, or none
        """;

    /// <summary>Parses the arguments after <c>init</c>; a usage error is returned in <c>Error</c> for exit 2.</summary>
    public static (InitOptions? Options, string? Error) Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var yes = false;
        string? directory = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            var arg = e.Current;
            if (arg is "--yes" or "-y")
            {
                yes = true;
            }
            else if (arg.StartsWith('-') && !arg.StartsWith("--", StringComparison.Ordinal))
            {
                return (null, "error: unknown option '" + arg + "'");
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg[2..];
                string? value = null;
                var eq = name.IndexOf('=', StringComparison.Ordinal);
                if (eq >= 0)
                {
                    (name, value) = (name[..eq], name[(eq + 1)..]);
                }

                if (name is not ("engine" or "gateway" or "endpoint" or "skills" or "mcp"))
                {
                    return (null, "error: unknown option '--" + name + "'");
                }

                if (value is null && !e.MoveNext())
                {
                    return (null, "error: option '--" + name + "' argument missing");
                }

                if (value is null && e.Current.StartsWith('-'))
                {
                    return (null, "error: option '--" + name + "' argument missing");
                }

                value ??= e.Current;
                if (name is "engine" or "skills" or "mcp")
                {
                    // Names, comma-separated or repeated, each once; an empty value is a usage error rather than an empty list (upstream's parseNames).
                    if (value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 0)
                    {
                        return (null, "error: option '--" + name + "' argument '" + value + "' is invalid. must name at least one " + (name == "engine" ? "engine" : name == "skills" ? "skill directory" : "MCP config file"));
                    }

                    values[name] = values.TryGetValue(name, out var previous) ? previous + "," + value : value;
                }
                else if (value.Length == 0)
                {
                    return (null, "error: option '--" + name + "' argument missing");
                }
                else
                {
                    values[name] = value;
                }
            }
            else if (directory is null)
            {
                directory = arg;
            }
            else
            {
                return (null, "error: unexpected argument '" + arg + "'");
            }
        }

        if (values.TryGetValue("endpoint", out var endpoint))
        {
            if (Gateways.ValidateEndpoint(endpoint) is { } problem)
            {
                return (null, "error: option '--endpoint' " + problem);
            }

            if (values.TryGetValue("gateway", out var other) && other != "openai-compatible")
            {
                return (null, "error: --endpoint is the base URL for --gateway openai-compatible, not --gateway " + other);
            }

            values["gateway"] = "openai-compatible";
        }

        if (values.TryGetValue("gateway", out var gateway) && gateway != "none" && !Gateways.All.Any(preset => preset.Id == gateway))
        {
            return (null, "error: option '--gateway' argument '" + gateway + "' is invalid. Allowed choices are " + string.Join(", ", Gateways.All.Select(preset => preset.Id).Append("none")) + ".");
        }

        IReadOnlyList<string>? engines = null;
        IReadOnlyList<string>? skills = null;
        IReadOnlyList<string>? mcp = null;
        string? error = null;
        if (values.TryGetValue("engine", out var engineList))
        {
            (engines, error) = CheckNames("--engine", "engine", engineList, [.. Engines.GetEnginePresets().Select(preset => preset.Id)]);
            if (error is null && engines!.Count > 1)
            {
                error = "error: --engine: the .NET port runs one target; name one engine";
            }
        }

        if (error is null && values.TryGetValue("skills", out var skillList))
        {
            (skills, error) = CheckNames("--skills", "skill directory", skillList, SkillDirs);
        }

        if (error is null && values.TryGetValue("mcp", out var mcpList))
        {
            (mcp, error) = CheckNames("--mcp", "MCP config file", mcpList, McpFiles);
        }

        if (error is not null)
        {
            return (null, error);
        }

        return (
            new InitOptions
            {
                Yes = yes,
                Directory = directory,
                Engines = engines,
                Gateway = values.GetValueOrDefault("gateway"),
                Endpoint = endpoint,
                Skills = skills,
                Mcp = mcp,
            },
            null);
    }

    /// <summary>What an <c>--engine</c>, <c>--skills</c>, or <c>--mcp</c> list names: known values, each once, or none for <c>none</c> alone. Anything else is a usage error with the valid values.</summary>
    private static (IReadOnlyList<string>? Names, string? Error) CheckNames(string flag, string noun, string list, IReadOnlyList<string> known)
    {
        var names = list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Contains("none"))
        {
            return names.Length > 1 ? (null, "error: " + flag + ": none cannot be combined with other values") : ([], null);
        }

        // A location with its trailing slashes dropped, so .agents/skills/ is .agents/skills.
        var locations = names.Select(name => name.TrimEnd('/')).ToList();
        var unknown = locations.FirstOrDefault(name => !known.Contains(name));
        if (unknown is not null)
        {
            return (null, "error: " + flag + ": unknown " + noun + " \"" + unknown + "\"; expected " + string.Join(", ", known.Append("none")));
        }

        return ([.. locations.Distinct()], null);
    }
}
