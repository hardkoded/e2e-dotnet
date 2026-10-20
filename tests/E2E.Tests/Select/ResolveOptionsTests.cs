// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Select;

/// <summary>Upstream's <c>describe('resolveOptions')</c> (<c>tests/unit/select.test.ts</c>).</summary>
public sealed class ResolveOptionsTests
{
    /// <summary>Partial: <c>[Agent]</c> takes one name, never a list, so only the empty name is checked.</summary>
    [Fact]
    public void Rejects_an_empty_agent_name_at_registration()
    {
        foreach (var name in new[] { "", " " })
        {
            var error = Assert.Throws<ArgumentException>(() => new AgentAttribute(name));
            Assert.Contains("agent must be the name of a configured agent", error.Message, StringComparison.Ordinal);
        }
    }
}
