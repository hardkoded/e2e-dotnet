// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>Options for <see cref="ExpressionResolution.ResolveExpression"/>: upstream's <c>ResolveExpressionOptions</c> (<c>engine/resolve.ts</c>).</summary>
public sealed record ResolveExpressionOptions
{
    /// <summary>
    /// Answers a <c>selector</c> expression: the nodes the platform-native selector addresses among the candidates, in document
    /// order. Without it a selector is <c>UNSUPPORTED_CAPABILITY</c>, since a semantic tree has no selector language of its own.
    /// </summary>
    public Func<string, IReadOnlyList<SemanticNode>, IReadOnlyList<SemanticNode>>? Selector { get; init; }
}

/// <summary>
/// The reference locator semantics over a semantic tree, as upstream's <c>resolveExpression</c> in <c>engine/resolve.ts</c>. An engine over such a
/// tree (a device's accessibility snapshot, an in-memory fake) calls this instead of interpreting the expression itself; an engine with a native
/// query engine (a browser) reproduces these rules. One immediate pass, every match in document order; polling, strictness, and staleness stay
/// with the runner. .NET-only holder name: C# has no free functions.
/// </summary>
public static class ExpressionResolution
{
    /// <summary>
    /// Resolves one expression to the nodes it currently matches, in document order. <paramref name="nodes"/> are the tree's top-level nodes
    /// (an observation root's children, or the root itself); every node under them is a candidate.
    /// <c>frame</c> is <c>FRAME_NOT_FOUND</c>: a semantic tree has no nested documents.
    /// </summary>
    public static IReadOnlyList<SemanticNode> ResolveExpression(LocatorExpression expression, IReadOnlyList<SemanticNode> nodes, ResolveExpressionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(nodes);
        return LocatorResolver.ResolveExpression(expression, nodes, options?.Selector);
    }
}
