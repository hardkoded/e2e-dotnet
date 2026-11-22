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

    /// <summary>
    /// Partial: the port has no collector, so the layers (a group's <c>fail</c>, each inner group, the test) are the values of NUnit's <c>E2E.Fail</c> property
    /// from the outermost fixture to the test; <c>null</c> stands for a layer with no <c>fail</c> (<c>fail: false</c> and no option both leave the mark alone).
    /// </summary>
    [Fact]
    public void Partial_Expects_a_test_to_fail_when_it_or_any_group_marks_it_the_innermost_reason_first()
    {
        const string group = "legacy checkout";
        Assert.Equal("legacy checkout", Resolve(group, null)); // inherits
        Assert.Equal("card form", Resolve(group, "card form")); // own reason
        Assert.Equal("legacy checkout", Resolve(group, "")); // bare mark keeps the group reason
        Assert.Equal("legacy checkout", Resolve(group, null)); // false does not unmark
        Assert.Equal("legacy checkout", Resolve(group, " ")); // blank reason keeps the group reason
        Assert.Equal("legacy checkout", Resolve(group, "", null)); // inner bare, then the test
        Assert.Equal("legacy checkout", Resolve(group, null, null)); // inner false does not unmark
        Assert.Equal("coupon API", Resolve(group, "coupon API", null)); // inner reason
        Assert.Equal("expected to fail", Resolve("", null)); // describe.fail with no reason
        Assert.Null(Resolve(null, null)); // plain group
    }

    private static string? Resolve(params string?[] layers) =>
        E2E.Internal.ExpectedFailureMarks.ResolveExpectedFailure(layers);
}
