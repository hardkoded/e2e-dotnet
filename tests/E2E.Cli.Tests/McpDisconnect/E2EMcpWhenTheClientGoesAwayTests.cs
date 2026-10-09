// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpDisconnect;

/// <summary>
/// <c>e2e mcp</c> when its client goes away: the built CLI over raw stdio pipes. A client that closes stdin, or
/// dies and takes both pipes with it, must leave nothing behind: the server closes its sessions and exits 0.
/// </summary>
public sealed class E2EMcpWhenTheClientGoesAwayTests : IAsyncLifetime
{
    private FixtureApp _app = null!;
    private FixtureProject _project = null!;
    private RawServer _server = null!;

    public async Task InitializeAsync()
    {
        _app = FixtureApp.Start();
        _project = new FixtureProject(new Dictionary<string, string> { ["e2e.config.json"] = FixtureProject.Config(_app.Url) });
        _server = RawServer.Start(_project.Directory);
        await _server.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _server.Dispose();
        _project.Dispose();
        _app.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Exits_0_when_the_client_closes_stdin_with_no_session_open()
    {
        _server.CloseStdin();
        Assert.Equal(0, await _server.ExitWithinAsync(TimeSpan.FromSeconds(10)));
        Assert.DoesNotContain("unhandled", _server.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Closes_the_open_session_and_exits_0_when_the_client_closes_stdin()
    {
        await OpenSessionAsync();
        _server.CloseStdin();
        Assert.True(await _server.ExitWithinAsync(TimeSpan.FromSeconds(20)) == 0, _server.Stderr);
        Assert.Contains("client disconnected", _server.Stderr, StringComparison.Ordinal);
        Assert.Contains("closed (server shutdown)", _server.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Closes_the_session_and_exits_0_when_the_client_dies_with_a_call_in_flight()
    {
        await OpenSessionAsync();
        await _server.SendAsync(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "call", arguments = new { tool = "navigate", args = new { url = "/slow" } } },
        });

        // A killed client takes every pipe with it: the answer to the call has nowhere to go.
        _server.Abandon();
        Assert.Equal(0, await _server.ExitWithinAsync(TimeSpan.FromSeconds(20)));
    }

    private async Task OpenSessionAsync()
    {
        await _server.SendAsync(new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "open_session", arguments = new { } } });
        var opened = await _server.ResponseAsync(2);
        Assert.False(opened.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), RawServer.ResultText(opened));
    }
}
