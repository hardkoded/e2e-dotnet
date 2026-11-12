// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

/// <summary>Upstream's <c>describe('observation byte budget')</c> (<c>tests/unit/agent-observation.test.ts</c>).</summary>
public sealed class ObservationByteBudgetTests
{
    [Fact]
    public void Never_exceeds_the_budget_including_the_truncation_marker()
    {
        // The budget is what keeps a request under the per-call token ceiling, so overshooting it by the marker length can tip a large page over.
        var children = Enumerable.Range(0, 400).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button number " + index }).ToList();
        foreach (var maxBytes in new[] { 200, 512, 2_048, 4_096 })
        {
            var prepared = global::E2E.AgentObservation.PrepareObservation(
                new Observation { Route = "/", Roots = [new SemanticNode { Ref = "n1", Role = "document", Children = children }] },
                new PrepareObservationOptions { Redactor = Redactor.None, MaxBytes = maxBytes });

            Assert.Equal(PreparedKind.Semantic, prepared.Kind);
            Assert.True(prepared.Bytes <= maxBytes, "within " + maxBytes);
            Assert.True(prepared.Truncated);
            Assert.Contains("[observation truncated", prepared.Text, StringComparison.Ordinal);
        }
    }
}
