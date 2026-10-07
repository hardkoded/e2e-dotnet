// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.McpServer;

public sealed class E2eMcpTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("view-source:file:///etc/passwd")]
    [InlineData("VIEW-SOURCE:http://example.test/")]
    [InlineData("about:srcdoc")]
    public async Task Opens_a_session_with_its_catalog_then_locates_acts_fills_a_secret_and_withholds_pixels_afterwards(string url)
    {
        // Only the navigate denials of this upstream test apply; the rest drives the MCP server, which the port does not have.
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(new DocumentWorld().Map("/", page => page.Heading("Home"))),
            Model = new ScriptedModel(_ => ModelResponses.Call("navigate", new { url })),
            BaseUrl = "https://billing.test",
            TestTitle = "mcp > navigate",
        });
        await session.App.OpenAsync("/");
        var denied = await Assert.ThrowsAsync<TestException>(() => session.Agent.ActAsync("open the url"));
        Assert.Equal("POLICY_DENIED", denied.Code);
        Assert.Equal("Forbidden URL scheme: " + url[..(url.IndexOf(':') + 1)].ToLowerInvariant(), denied.Message);
    }
}
