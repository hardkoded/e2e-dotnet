// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>The queries a node's own fields give, in the order the skill recommends.</summary>
public sealed class NodeQueriesTests
{
    [Fact]
    public void Orders_role_and_name_label_placeholder_then_text_and_skips_a_role_GetByRole_does_not_take()
    {
        static string[] Queries(Fake node) => [.. SessionTestLines.NodeQueries(node.ToNode(), text => text).Select(Describe)];

        Assert.Equal(
            ["role textbox Email", "label Email", "placeholder you@example.test", "text Email"],
            Queries(new Fake("n1", "textbox", "Email", Placeholder: "you@example.test")));
        Assert.Equal(["label Card", "text Card body", "text Card"], Queries(new Fake("n2", "generic", "Card", "Card  body ")));
        Assert.Empty(Queries(new Fake("n3", "button")));
    }

    private static string Describe(LocateArgs query) =>
        query.Role is not null ? "role " + query.Role + " " + query.Name
        : query.Label is not null ? "label " + query.Label
        : query.Placeholder is not null ? "placeholder " + query.Placeholder
        : query.TestId is not null ? "testId " + query.TestId
        : "text " + query.Text;
}
