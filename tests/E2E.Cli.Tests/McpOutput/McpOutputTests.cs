// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Internal;
using McpOutputType = E2E.Cli.Mcp.McpOutput;

namespace E2E.Cli.Tests.McpOutput;

/// <summary>
/// <c>describe('McpOutput')</c> of upstream's <c>mcp-output.test.ts</c>. Not ported: "releases a character cut short at
/// shutdown as a replacement character", because the port's <see cref="McpOutputType"/> takes text from a <see cref="TextWriter"/>,
/// not bytes, so no character is ever cut in the middle.
/// </summary>
public sealed class McpOutputTests
{
    private const string Token = "mcp-output-token-1";

    private static (McpOutputType Out, E2E.Internal.SecretLedger Ledger, Func<string> Text) Output()
    {
        var ledger = new E2E.Internal.SecretLedger();
        var written = new System.Text.StringBuilder();
        return (new McpOutputType(text => written.Append(text), ledger), ledger, () => written.ToString());
    }

    [Fact]
    public async Task Holds_what_is_printed_during_a_load_and_releases_it_redacted_with_the_secrets_the_load_registered()
    {
        var (output, ledger, text) = Output();
        await output.WithholdDuringAsync(() =>
        {
            output.Write(OutputStream.Stdout, $"config: {Token}\n");
            Assert.Equal("", text());
            ledger.Add([Secret.Create("apiToken", Token)]);
            return Task.FromResult(0);
        });
        Assert.Equal("config: <secret:apiToken>\n", text());
    }

    [Fact]
    public async Task Withholds_everything_printed_while_a_load_that_failed_was_in_flight_other_loads_included()
    {
        var (output, ledger, text) = Output();
        var finishOther = new TaskCompletionSource<int>();
        var other = output.WithholdDuringAsync(() => finishOther.Task);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => output.WithholdDuringAsync<int>(() =>
        {
            output.Write(OutputStream.Stderr, $"broken: {Token}\n");
            throw new InvalidOperationException("config refuses to load");
        }));
        Assert.Equal("config refuses to load", failure.Message);
        ledger.Add([Secret.Create("other", "other-secret-value")]);
        finishOther.SetResult(0);
        await other;
        Assert.DoesNotContain(Token, text());
        Assert.Contains("withheld", text());

        output.Write(OutputStream.Stdout, "after the loads\n");
        Assert.Contains("after the loads\n", text());
    }

    [Fact]
    public void Withholds_output_held_for_a_load_still_in_flight_at_shutdown_a_report_of_an_uncaught_error_included()
    {
        var (output, _, text) = Output();
        _ = output.WithholdDuringAsync(() => new TaskCompletionSource<int>().Task);
        output.Write(OutputStream.Stdout, $"mid-load: {Token}\n");
        output.Report($"e2e mcp: [error] uncaught: Error: {Token}");
        Assert.Equal("", text());
        output.End();
        Assert.DoesNotContain(Token, text());
        Assert.Contains("withheld", text());
    }

    [Fact]
    public async Task Releases_a_held_tail_redacted_once_no_tool_call_is_in_flight()
    {
        var (output, ledger, text) = Output();
        ledger.Add([Secret.Create("apiToken", Token)]);
        var finishOther = new TaskCompletionSource<int>();
        var other = output.DuringCallAsync(() => finishOther.Task);
        await output.DuringCallAsync(() =>
        {
            output.Write(OutputStream.Stdout, $"tail: {Token[..6]}");
            return Task.FromResult(0);
        });
        Assert.DoesNotContain(Token[..6], text());
        output.Write(OutputStream.Stdout, Token[6..]);
        finishOther.SetResult(0);
        await other;
        Assert.Equal("tail: <secret:apiToken>", text());
    }

    [Fact]
    public void Redacts_a_value_split_across_stdout_and_stderr_which_land_on_one_stream()
    {
        var (output, ledger, text) = Output();
        ledger.Add([Secret.Create("apiToken", Token)]);
        output.Write(OutputStream.Stdout, Token[..6]);
        output.Write(OutputStream.Stderr, $"{Token[6..]}\n");
        output.End();
        Assert.Equal("<secret:apiToken>\n", text());
    }

    [Fact]
    public void Puts_its_own_lines_on_a_line_of_their_own_after_an_unfinished_user_line()
    {
        var (output, _, text) = Output();
        output.Write(OutputStream.Stdout, "progress");
        output.Log("e2e mcp: [info] closing");
        output.End();
        Assert.Equal("progress\ne2e mcp: [info] closing\n", text());
    }
}
