// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// A project tool that <c>e2e.config.json</c> can declare (<c>agents.&lt;name&gt;.tools</c>), where upstream's config holds a
/// <c>defineTool</c> call: .NET-only, since a JSON file has no code. The tool runs a command with the project root as its working directory,
/// writes the model's input as JSON on its stdin, and answers what the command wrote on stdout. What it writes on stderr is printed on the
/// process's stderr, line by line, as user code's output is (the MCP server redacts it; a test run prints it as it is). A command that exits with a code other than 0 fails the call.
/// </summary>
internal static class CommandTool
{
    /// <summary>How long a command may run before it is killed.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan PipeGrace = TimeSpan.FromSeconds(2);

    private static readonly string[] Keys = ["description", "command", "inputSchema", "mutates", "platforms"];

    /// <summary>Reads <paramref name="raw"/>, the <c>tools</c> of the agent <paramref name="label"/>: an object of tools by name.</summary>
    public static IReadOnlyDictionary<string, AgentTool> Read(JsonElement raw, string label, string projectRoot)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(label + " must be an object of tools by name");
        }

        var tools = new Dictionary<string, AgentTool>(StringComparer.Ordinal);
        foreach (var property in raw.EnumerateObject())
        {
            var where = label + "." + property.Name;
            var tool = property.Value;
            if (tool.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(where + " must be an object");
            }

            foreach (var key in tool.EnumerateObject())
            {
                if (!Keys.Contains(key.Name, StringComparer.Ordinal))
                {
                    throw Invalid(where + " has an unknown key \"" + key.Name + "\"; keys: " + string.Join(", ", Keys));
                }
            }

            var description = tool.TryGetProperty("description", out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString())
                ? text.GetString()!
                : throw Invalid(where + ".description must be a non-empty string");
            if (!tool.TryGetProperty("command", out var command) || command.ValueKind != JsonValueKind.Array || command.GetArrayLength() == 0
                || command.EnumerateArray().Any(part => part.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(part.GetString())))
            {
                throw Invalid(where + ".command must be a non-empty array of strings: the program, then its arguments");
            }

            if (!tool.TryGetProperty("mutates", out var mutates) || mutates.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Invalid(where + ".mutates must be true or false: whether running the tool can change the application");
            }

            var schema = tool.TryGetProperty("inputSchema", out var given)
                ? given.ValueKind == JsonValueKind.Object ? JsonNode.Parse(given.GetRawText())!.AsObject() : throw Invalid(where + ".inputSchema must be a JSON schema object")
                : new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false };
            string[]? platforms = null;
            if (tool.TryGetProperty("platforms", out var named))
            {
                if (named.ValueKind != JsonValueKind.Array || named.GetArrayLength() == 0
                    || named.EnumerateArray().Any(name => name.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(name.GetString())))
                {
                    throw Invalid(where + ".platforms must be a non-empty array of platform names");
                }

                platforms = [.. named.EnumerateArray().Select(name => name.GetString()!)];
            }

            string[] argv = [.. command.EnumerateArray().Select(part => part.GetString()!)];
            tools[property.Name] = new AgentTool(description, schema, mutates.GetBoolean(), (input, token) => RunAsync(argv, projectRoot, input, token), platforms);
        }

        return tools;
    }

    private static async Task<string> RunAsync(string[] argv, string projectRoot, JsonObject input, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(argv[0])
        {
            WorkingDirectory = projectRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in argv.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Exception cause) when (cause is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new TestException("TOOL_FAILED", "could not start " + argv[0] + ": " + cause.Message);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);
        Task<string>? stdout = null;
        Task? stderr = null;
        try
        {
            // Both pipes are read at once, so a command that fills one cannot block on the other.
            stdout = process.StandardOutput.ReadToEndAsync(limit.Token);
            stderr = ForwardStderrAsync(process.StandardError, limit.Token);
            try
            {
                await process.StandardInput.WriteAsync(input.ToJsonString().AsMemory(), limit.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // A command that never reads its input closes the pipe; its exit code says whether it worked.
            }

            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            try
            {
                // A grandchild that kept a pipe open must not hold the call: give the pipes a moment, then kill the tree and read what came.
                await Task.WhenAll(stdout, stderr).WaitAsync(PipeGrace, limit.Token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                KillTree(process);
                await Task.WhenAll(stdout, stderr).WaitAsync(PipeGrace, limit.Token).ConfigureAwait(false);
            }

            var text = await stdout.ConfigureAwait(false);
            return process.ExitCode == 0
                ? text.TrimEnd((char)13, (char)10)
                : throw new TestException("TOOL_FAILED", argv[0] + " exited with code " + process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TestException("TOOL_FAILED", argv[0] + " did not finish within " + (int)Timeout.TotalSeconds + " seconds");
        }
        finally
        {
            KillTree(process);
            foreach (var read in new Task?[] { stdout, stderr })
            {
                if (read is not null)
                {
                    // The reads end with the process; observe how, so none of them is left unobserved.
                    await Task.WhenAny(read, Task.Delay(PipeGrace)).ConfigureAwait(false);
                    _ = read.Exception;
                }
            }
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited already.
        }
    }

    private static async Task ForwardStderrAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            Console.Error.WriteLine(line);
        }
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", message);
}
