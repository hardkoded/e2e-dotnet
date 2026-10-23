// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// One node on a fake screen: what observe lists, and what the locator engine reads. Every node is 100 by 20 at x 0, so no two boxes
/// overlap. Upstream's <c>Fake</c> of <c>mcp-test-lines.test.ts</c>.
/// </summary>
internal sealed record FakeNode(string Id, double Y, string? Role = null, string? Name = null, string? Text = null, string? TestId = null, string? Placeholder = null, IReadOnlyList<FakeNode>? Children = null)
{
    /// <summary>The node as observe lists it: no test id, which only the locator engine's read carries.</summary>
    public SemanticNode Listed() => Node(Id, withTestId: false, Children?.Select(child => child.Listed()) ?? []);

    /// <summary>The node as the locator engine reads it, by an id of its own.</summary>
    public SemanticNode Read() => Node("located-" + Id, withTestId: true, []);

    private SemanticNode Node(string reference, bool withTestId, IEnumerable<SemanticNode> children) => new()
    {
        Ref = reference,
        Role = Role,
        Name = Name,
        Text = Text,
        Placeholder = Placeholder,
        TestId = withTestId ? TestId : null,
        Rect = new BoundingBox(0, Y, 100, 20),
        Children = [.. children],
    };
}
