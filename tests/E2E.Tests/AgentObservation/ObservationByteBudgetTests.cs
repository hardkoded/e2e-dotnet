// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

/// <summary>Upstream's <c>describe('observation byte budget')</c> (<c>tests/unit/agent-observation.test.ts</c>).</summary>
public sealed class ObservationByteBudgetTests
{
    /// <summary>Partial: upstream reads <c>prepared.bytes</c> and <c>prepared.truncated</c> of <c>prepareObservation</c>. The port reads the byte size of the rendered text and the cut flag of <c>Render</c>.</summary>
    [Fact]
    public void Partial_never_exceeds_the_budget_including_the_truncation_marker()
    {
        // The budget is what keeps a request under the per-call token ceiling, so overshooting it by the marker length can tip a large page over.
        var children = Enumerable.Range(0, 400).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button number " + index }).ToList();
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = children };
        foreach (var maxBytes in new[] { 200, 512, 2_048, 4_096 })
        {
            var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, Redactor.None, null, maxBytes, out var cut);

            Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= maxBytes, "within " + maxBytes);
            Assert.True(cut);
            Assert.Contains("[observation truncated", text, StringComparison.Ordinal);
        }
    }
}
