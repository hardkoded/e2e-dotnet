// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentAct;

/// <summary>Reading the node tree an executor gets from <c>ObserveAsync(Tree = true)</c> (upstream's <c>tests/helpers/executor-tree.ts</c>).</summary>
internal static class ExecutorTree
{
    /// <summary>The first node <paramref name="match"/> accepts, depth first.</summary>
    public static ExecutorNode? FindNode(IEnumerable<ExecutorNode>? nodes, Func<ExecutorNode, bool> match)
    {
        foreach (var node in nodes ?? [])
        {
            if (match(node))
            {
                return node;
            }

            if (FindNode(node.Children, match) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
