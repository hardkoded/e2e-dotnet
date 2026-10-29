// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Cli;

/// <summary>A project-level MCP config file and the agents that read it.</summary>
/// <param name="File">Project-relative config file with <c>/</c> separators.</param>
/// <param name="Hint">The agents that read it.</param>
internal sealed record McpLocation(string File, string Hint);

/// <summary>A file <c>init</c> writes to register the server.</summary>
/// <param name="Relative">Project-relative file, e.g. <c>.mcp.json</c>.</param>
/// <param name="Existing">True when the file already existed, so the write is an update.</param>
/// <param name="Absolute">The file's full path.</param>
/// <param name="Content">The file's new text.</param>
internal sealed record McpRegistration(string Relative, bool Existing, string Absolute, string Content);

/// <summary>A symlink on the way to a config file, each once: a file or a directory init will not write through.</summary>
/// <param name="Relative">Project-relative path of the link with <c>/</c> separators, e.g. <c>.cursor</c>.</param>
/// <param name="Target">Absolute path the link leads to; the path as written when it dangles.</param>
internal sealed record McpLink(string Relative, string Target);

/// <summary>What init plans for the chosen config files: the writes, and the links it leaves alone.</summary>
internal sealed record McpPlan(IReadOnlyList<McpRegistration> Registrations, IReadOnlyList<McpLink> Links);

/// <summary>
/// Upstream's <c>cli/init/mcp-config.ts</c>: the <c>e2e init</c> step that registers the <c>e2e mcp</c> server with the coding
/// agents that read a project-level MCP config file. Each file is JSON with a <c>mcpServers</c> map; the entry is merged in and
/// every other server is left exactly as it was. A file reached through a symlink, or under a linked directory, is never read
/// or written through: init reports the link and skips it.
/// </summary>
internal static partial class McpConfig
{
    /// <summary>Where project-scoped MCP servers are declared.</summary>
    public static readonly IReadOnlyList<McpLocation> McpLocations =
    [
        new(".mcp.json", "Claude Code"),
        new(".cursor/mcp.json", "Cursor"),
    ];

    private const string McpServerName = "e2e";

    private static readonly JsonDocumentOptions Strict = new();

    /// <summary>The line that registers the server by hand, for a project that skipped it.</summary>
    public const string RegisterCommand = "claude mcp add --scope project e2e -- e2e mcp";

    private static JsonObject ServerEntry() => new() { ["command"] = "e2e", ["args"] = new JsonArray("mcp") };

    private static JsonObject? ReadDocument(string absolute)
    {
        if (!File.Exists(absolute))
        {
            return null;
        }

        var text = File.ReadAllText(absolute);
        JsonNode? parsed;
        try
        {
            parsed = text.Trim().Length == 0 ? new JsonObject() : JsonNode.Parse(text, null, Strict);
        }
        catch (JsonException cause)
        {
            throw new InvalidOperationException(absolute + " is not valid JSON; fix it or remove it before registering the MCP server", cause);
        }

        return parsed as JsonObject ?? throw new InvalidOperationException(absolute + " must hold a JSON object with a \"mcpServers\" map");
    }

    /// <summary>
    /// The first symlink between the project root and <paramref name="target"/>, or null when every existing component is a
    /// plain entry. A component past the link is not inspected: it lives wherever the link points.
    /// </summary>
    private static McpLink? LinkOnPath(string cwd, string target)
    {
        var current = cwd;
        foreach (var segment in Path.GetRelativePath(cwd, target).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null)
            {
                return new McpLink(Path.GetRelativePath(cwd, current).Replace('\\', '/'), LinkTarget(current));
            }

            if (!info.Exists)
            {
                return null;
            }
        }

        return null;
    }

    private static string LinkTarget(string link) =>
        AgentSkill.Exists(link)
            ? Paths.OnDiskPath(link)
            : Path.GetFullPath(new FileInfo(link).LinkTarget ?? link, Path.GetDirectoryName(link)!);

    /// <summary>Known config files that already register the e2e server. A file reached through a symlink is not read.</summary>
    public static List<string> FindRegisteredMcpFiles(string cwd) =>
        [.. McpLocations.Select(location => location.File).Where(file =>
        {
            if (LinkOnPath(cwd, Path.Combine(cwd, file)) is not null)
            {
                return false;
            }

            try
            {
                return ReadDocument(Path.Combine(cwd, file))?["mcpServers"] is JsonObject servers && servers.ContainsKey(McpServerName);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        })];

    /// <summary>
    /// Plans the writes for the given files. A file without the entry gets it merged in; one whose entry already matches needs
    /// nothing and is left out. Extra settings on the e2e entry, other servers, other keys, and the file's indentation are
    /// preserved. A file reached through a symlink is never read; it is returned among <c>Links</c> for the caller to report and skip.
    /// </summary>
    public static McpPlan PlanMcpRegistration(string cwd, IReadOnlyList<string> files)
    {
        var registrations = new List<McpRegistration>();
        var links = new List<McpLink>();
        foreach (var file in files)
        {
            var absolute = Path.GetFullPath(Path.Combine(cwd, file));
            if (LinkOnPath(cwd, absolute) is { } link)
            {
                links.Add(link);
                continue;
            }

            var document = ReadDocument(absolute) ?? new JsonObject();
            var servers = document["mcpServers"] as JsonObject ?? new JsonObject();
            var existing = servers[McpServerName];
            JsonObject entry;
            if (existing is JsonObject current)
            {
                entry = (JsonObject)current.DeepClone();
                foreach (var (name, value) in ServerEntry())
                {
                    entry[name] = value?.DeepClone();
                }
            }
            else
            {
                entry = ServerEntry();
            }

            if (existing?.ToJsonString() == entry.ToJsonString())
            {
                continue;
            }

            var merged = new JsonObject();
            foreach (var (name, value) in document)
            {
                if (name != "mcpServers")
                {
                    merged[name] = value?.DeepClone();
                }
            }

            var mergedServers = new JsonObject();
            foreach (var (name, value) in servers)
            {
                if (name != McpServerName)
                {
                    mergedServers[name] = value?.DeepClone();
                }
            }

            mergedServers[McpServerName] = entry;
            merged["mcpServers"] = mergedServers;
            var original = File.Exists(absolute) ? File.ReadAllText(absolute) : null;
            registrations.Add(new McpRegistration(file, original is not null, absolute, Serialize(merged, original) + "\n"));
        }

        return new McpPlan(registrations, links);
    }

    /// <summary>The document as <c>JSON.stringify</c> writes it, indented the way the file already was.</summary>
    private static string Serialize(JsonObject merged, string? original)
    {
        var indent = original is null ? "  " : Indentation().Match(original) is { Success: true } match ? match.Groups[1].Value : "  ";
        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        if (indent.All(character => character == indent[0]) && indent[0] is ' ' or '\t')
        {
            options.IndentCharacter = indent[0];
            options.IndentSize = indent.Length;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            merged.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [GeneratedRegex("\n([\t ]+)\"")]
    private static partial Regex Indentation();
}
