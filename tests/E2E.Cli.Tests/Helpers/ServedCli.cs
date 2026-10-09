// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace E2E.Cli.Tests.Helpers;

/// <summary>A tool's result as the tests read it: the text parts joined, whether it is an error, and how many images it carries.</summary>
internal sealed record ToolText(string Text, bool IsError, int Images);

/// <summary>The built <c>e2e mcp</c> process serving a project to a real MCP client, as upstream's <c>beforeAll</c> starts it.</summary>
internal sealed partial class ServedCli : IAsyncDisposable
{
    private readonly StringBuilder _stderr;

    private ServedCli(McpClient client, StringBuilder stderr)
    {
        Client = client;
        _stderr = stderr;
    }

    public McpClient Client { get; }

    /// <summary>Everything the server wrote to stderr so far.</summary>
    public string Stderr
    {
        get
        {
            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    public static async Task<ServedCli> StartAsync(string workingDirectory, params string[] flags)
    {
        var stderr = new StringBuilder();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "e2e",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "E2E.Cli.dll"), "mcp", .. flags],
            WorkingDirectory = workingDirectory,
            EnvironmentVariables = new Dictionary<string, string?> { ["CI"] = "" },
            StandardErrorLines = line =>
            {
                lock (stderr)
                {
                    stderr.AppendLine(line);
                }
            },
        });
        var client = await McpClient.CreateAsync(transport, new McpClientOptions { ClientInfo = new Implementation { Name = "e2e-mcp-test", Version = "0.0.0" } });
        return new ServedCli(client, stderr);
    }

    /// <summary>A fixed tool's result.</summary>
    public async Task<ToolText> InvokeAsync(string name, Dictionary<string, object?>? args = null)
    {
        var result = await Client.CallToolAsync(name, args ?? []);
        return new ToolText(
            string.Join('\n', result.Content.OfType<TextContentBlock>().Select(part => part.Text)),
            result.IsError == true,
            result.Content.OfType<ImageContentBlock>().Count());
    }

    /// <summary>A catalog tool through the server's <c>call</c> tool.</summary>
    public Task<ToolText> CallAsync(string tool, Dictionary<string, object?>? args = null)
    {
        var arguments = new Dictionary<string, object?> { ["tool"] = tool };
        if (args is not null)
        {
            arguments["args"] = args;
        }

        return InvokeAsync("call", arguments);
    }

    /// <summary>The ref of the first observation line matching <paramref name="pattern"/>.</summary>
    public static string RefOf(string text, string pattern)
    {
        var line = text.Split('\n').FirstOrDefault(candidate => Regex.IsMatch(candidate, pattern, RegexOptions.None, TimeSpan.FromSeconds(5)))
            ?? throw new InvalidOperationException("no node matches " + pattern + " in:\n" + text);
        return Ref().Match(line).Groups[1].Value;
    }

    /// <summary>The catalog lines of an opening or a <c>tools</c> text.</summary>
    public static string[] CatalogLines(string text) =>
        [.. text.Split('\n').SkipWhile(line => !line.StartsWith("- ", StringComparison.Ordinal)).TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal))];

    public static string[] CatalogNames(string text) => [.. CatalogLines(text).Select(line => CatalogName().Match(line).Groups[1].Value)];

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
    }

    [GeneratedRegex(@"\[ref=([^\]]+)\]")]
    private static partial Regex Ref();

    [GeneratedRegex(@"^- (\S+?)(?: \{|:)")]
    private static partial Regex CatalogName();
}
