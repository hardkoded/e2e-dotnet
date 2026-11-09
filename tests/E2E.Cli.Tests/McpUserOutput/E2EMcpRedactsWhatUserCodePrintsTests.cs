// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using E2E.Internal;
using McpOutputType = E2E.Cli.Mcp.McpOutput;

namespace E2E.Cli.Tests.McpUserOutput;

/// <summary>
/// <c>describe('e2e mcp redacts what user code prints')</c> of upstream's <c>mcp-user-output.test.ts</c>. Upstream spawns a
/// real server on a TypeScript config whose top-level code prints, and on a <c>defineTool</c> tool. The port's config is
/// JSON: it declares project tools as commands (<c>agents.&lt;name&gt;.tools</c>), which the second test spawns the server for,
/// but it runs no code when it loads, so what a config prints, and a promise nobody awaits, are checked on the server's output
/// layer (<see cref="McpOutputType"/>) in process.
/// </summary>
public sealed class E2EMcpRedactsWhatUserCodePrintsTests
{
    private const string Token = "tok-secret-9";

    private const string ServedToken = "user-output-token-42";

    [Fact]
    public async Task Partial_Keeps_the_protocol_working_and_redacts_the_config_and_a_project_tool_on_stdout_and_stderr()
    {
        using var app = FixtureApp.Start();
        var tool = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["description"] = "Print the token every way a command can.",
            ["mutates"] = false,
            ["command"] = new[] { "dotnet", Path.Combine(AppContext.BaseDirectory, "E2E.Tests.App.dll"), "leak", ServedToken },
        });
        using var project = new FixtureProject(new Dictionary<string, string>
        {
            ["e2e.config.json"] = "{ \"targets\": [{ \"name\": \"web\", \"platform\": \"web\", \"app\": { \"url\": \"" + app.Url + "\" } }], \"secrets\": { \"apiToken\": \"" + ServedToken + "\" }, "
                + "\"agents\": { \"default\": { \"tools\": { \"leak\": " + tool + " } } } }",
        });
        string stderr;
        await using (var served = await ServedCli.StartAsync(project.Directory, "--headless"))
        {
            var opened = await served.InvokeAsync("open_session");
            Assert.False(opened.IsError, opened.Text);
            var leaked = await served.CallAsync("leak");
            Assert.False(leaked.IsError, leaked.Text);
            Assert.Equal("printed", leaked.Text);

            // The line printed without a newline is released when its call ends.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!served.Stderr.Contains("tool stderr no newline: <secret:apiToken>", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            stderr = served.Stderr;
            Assert.True(stderr.Contains("tool stderr no newline: <secret:apiToken>", StringComparison.Ordinal), "the line printed without a newline was never released; stderr was: " + stderr);
        }

        Assert.DoesNotContain(ServedToken, stderr, StringComparison.Ordinal);
        var lines = stderr.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        foreach (var line in new[]
        {
            "tool stderr: <secret:apiToken>",
            "tool stderr split: <secret:apiToken>",
            "tool stderr no newline: <secret:apiToken>",
        })
        {
            Assert.Contains(line, lines);
        }
    }

    private static (McpOutputType Output, E2E.Internal.SecretLedger Ledger, Func<string> Text) Create()
    {
        var ledger = new E2E.Internal.SecretLedger();
        var sink = new System.Text.StringBuilder();
        var output = new McpOutputType(text => sink.Append(text), ledger);
        return (output, ledger, () => sink.ToString());
    }

    [Fact]
    public async Task Partial_Withholds_what_a_config_printed_before_it_failed_to_load()
    {
        var (output, _, text) = Create();
        var stdout = new McpOutputWriter(output, OutputStream.Stdout);

        await Assert.ThrowsAsync<InvalidOperationException>(() => output.WithholdDuringAsync<int>(() =>
        {
            stdout.WriteLine("config says " + Token);
            throw new InvalidOperationException("config failed");
        }));

        Assert.DoesNotContain(Token, text());
        Assert.Contains("e2e mcp: [warning] withheld", text());
    }

    [Fact]
    public async Task Redacts_what_a_config_and_a_tool_print_and_logs_an_uncaught_error_through_the_output_layer()
    {
        var (output, ledger, text) = Create();
        var stdout = new McpOutputWriter(output, OutputStream.Stdout);
        var stderr = new McpOutputWriter(output, OutputStream.Stderr);

        // A config prints before it registers its secret: held, then released redacted.
        await output.WithholdDuringAsync(() =>
        {
            stdout.WriteLine("config stdout " + Token);
            stderr.WriteLine("config stderr " + Token);
            ledger.Add([Secret.Create("apiToken", Token)]);
            return Task.FromResult(0);
        });

        // A tool call prints the value split across writes, on both streams, and leaves a line unfinished.
        await output.DuringCallAsync(() =>
        {
            stdout.Write("tool part " + Token[..5]);
            stderr.Write(Token[5..] + "\n");
            stdout.Write("no newline " + Token);
            return Task.FromResult(0);
        });

        // The server's own log line starts on a line of its own.
        output.Log("e2e mcp: ready");

        // An error nobody caught carries the value too.
        McpCommand.ReportUncaught(output, new InvalidOperationException("boom " + Token));
        output.End();

        var all = text();
        Assert.DoesNotContain(Token, all);
        Assert.Contains("config stdout <secret:apiToken>", all);
        Assert.Contains("config stderr <secret:apiToken>", all);
        Assert.Contains("tool part <secret:apiToken>", all);
        Assert.Contains("no newline <secret:apiToken>\ne2e mcp: ready\n", all);
        Assert.Contains("e2e mcp: [error] uncaught:", all);
        Assert.Contains("boom <secret:apiToken>", all);
    }
}
