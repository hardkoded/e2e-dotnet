// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Internal;

namespace E2E.Cli.Tests.McpUserOutput;

/// <summary>
/// <c>describe('e2e mcp redacts what user code prints')</c> of upstream's <c>mcp-user-output.test.ts</c>. Upstream spawns a
/// real server on a TypeScript config whose top-level code prints, and on a <c>defineTool</c> tool. The port's config is
/// JSON and has no project tools, so both tests run the server's output layer (<see cref="McpOutput"/>) in process.
/// </summary>
public sealed class E2EMcpRedactsWhatUserCodePrintsTests
{
    private const string Token = "tok-secret-9";

    private static (McpOutput Output, E2E.Internal.SecretLedger Ledger, Func<string> Text) Create()
    {
        var ledger = new E2E.Internal.SecretLedger();
        var sink = new System.Text.StringBuilder();
        var output = new McpOutput(text => sink.Append(text), ledger);
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
    public async Task Partial_Keeps_the_protocol_working_and_redacts_the_config_and_a_project_tool_on_stdout_and_stderr()
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
