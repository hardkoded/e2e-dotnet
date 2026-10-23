// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>Upstream's <c>describe('nodeQueries')</c> of <c>mcp-test-lines.test.ts</c>.</summary>
public sealed class NodeQueriesTests
{
    private static string[] Described(FakeNode node) =>
        [.. SessionTestLines.NodeQueries(node.Listed(), text => text).Select(query => string.Join(",", new[] { query.Role, query.Name, query.Label, query.Placeholder, query.Text }.Select(part => part ?? "-")))];

    [Fact]
    public void Orders_role_and_name_label_placeholder_then_text_and_skips_a_role_GetByRole_does_not_take()
    {
        Assert.Equal(
            ["textbox,Email,-,-,-", "-,-,Email,-,-", "-,-,-,you@example.test,-", "-,-,-,-,Email"],
            Described(new FakeNode("n1", 0, "textbox", "Email", Placeholder: "you@example.test")));
        Assert.Equal(
            ["-,-,Card,-,-", "-,-,-,-,Card body", "-,-,-,-,Card"],
            Described(new FakeNode("n2", 0, "generic", "Card", "Card  body ")));
        Assert.Empty(Described(new FakeNode("n3", 0, "button")));
    }
}
