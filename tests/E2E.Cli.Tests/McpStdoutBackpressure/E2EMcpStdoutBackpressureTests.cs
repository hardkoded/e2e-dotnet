// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpStdoutBackpressure;

/// <summary>
/// <c>e2e mcp</c> over a pipe whose reader falls behind. Responses that outgrow the pipe buffer (a guide resource)
/// must be queued and delivered when the client drains them; the server stays up meanwhile.
/// </summary>
public sealed class E2EMcpStdoutBackpressureTests
{
    /// <summary>Well past any pipe buffer: each guide read answers with tens of kilobytes.</summary>
    private const int Reads = 40;

    [Fact]
    public async Task Survives_responses_the_client_has_not_read_yet_and_delivers_all_of_them()
    {
        using var project = new FixtureProject(new Dictionary<string, string> { ["e2e.config.json"] = FixtureProject.Config("http://127.0.0.1:1") });
        using var server = RawServer.Start(project.Directory, readStdout: false);
        await server.SendAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "backpressure", version = "0.0.0" } },
        });
        await server.SendAsync(new { jsonrpc = "2.0", method = "notifications/initialized" });
        for (var index = 0; index < Reads; index++)
        {
            await server.SendAsync(new { jsonrpc = "2.0", id = 100 + index, method = "resources/read", @params = new { uri = "e2e://guide" } });
        }

        // Nobody reads stdout: the server's writes back up while the requests keep arriving.
        await Task.Delay(1_000);
        Assert.False(server.HasExited, "server exited early:\n" + server.Stderr);

        // The initialize answer comes first, then the reads; count the reads.
        var answered = await server.ReadAllAsync(Reads, TimeSpan.FromSeconds(20));
        Assert.False(server.HasExited, server.Stderr);
        Assert.Equal(Reads, answered);
    }
}
