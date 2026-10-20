// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.ScreenTree;

/// <summary>
/// Upstream's <c>describe('screen.tree')</c> (<c>tests/unit/screen-tree.test.ts</c>). Not ported: "refuses on a locator, before observing"
/// and "refuses an unknown option" (a <see cref="Locator"/> has no <c>TreeAsync</c>, and <see cref="ActionOptions"/> is a typed class, so
/// neither call compiles), and <c>sdk-types.ts</c>'s type checks for the same reason.
/// </summary>
public sealed class ScreenTreeTests
{
    [Fact]
    public async Task Renders_one_observation_children_indented_as_a_screen_step()
    {
        var fixture = new ScreenTreeFixture();

        var tree = await fixture.Screen.TreeAsync();

        var lines = tree.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Matches(new Regex(@"^#root root", RegexOptions.CultureInvariant), lines[0]);
        Assert.Matches(new Regex(@"^ #list list ""Todos""", RegexOptions.CultureInvariant), lines[1]);
        Assert.Matches(new Regex(@"^ {2}#milk checkbox ""Milk"" \[checked\]", RegexOptions.CultureInvariant), lines[2]);
        Assert.Matches(new Regex(@"^ #add button ""Add""", RegexOptions.CultureInvariant), lines[3]);
        Assert.Equal(1, fixture.Observed);
        var step = Assert.Single(fixture.Steps.All());
        Assert.Equal("screen.tree", step.Api);
        Assert.Equal(StepStatus.Passed, step.Status);
    }
}
