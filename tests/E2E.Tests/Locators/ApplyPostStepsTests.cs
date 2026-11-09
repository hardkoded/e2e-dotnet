// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Locators;

/// <summary>Upstream's <c>describe('applyPostSteps')</c> (<c>packages/web/tests/unit/locators.test.ts</c>).</summary>
public sealed class ApplyPostStepsTests
{
    private static readonly string[] Matches = ["a", "b", "c"];

    private static PostStep Index(int value) => new PostStep.Position(value);

    private static Task<bool> Never(string match, LocatorFilterOptions options) => Task.FromResult(false);

    // Upstream's it.each: the steps, then the matches they leave. A step is spelled as its position: a number, or first or last. A
    // PostStep is internal, so the rows spell it as text and the test builds it.
    [Theory]
    [InlineData("3", "")]
    [InlineData("last first", "c")]
    [InlineData("", "a b c")]
    public async Task Applies_steps_to_yield_matches(string steps, string expected)
    {
        PostStep[] built =
        [
            .. steps.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(step => Index(step switch
            {
                "first" => 0,
                "last" => LocatorExpression.LastIndex,
                _ => int.Parse(step, System.Globalization.CultureInfo.InvariantCulture),
            })),
        ];
        Assert.Equal(expected.Split(' ', StringSplitOptions.RemoveEmptyEntries), await WebLocators.ApplyPostSteps(Matches, built, Never));
    }

    [Fact]
    public async Task Runs_a_filter_step_only_on_the_matches_the_steps_before_it_selected()
    {
        List<string> asked = [];
        PostStep filter = new PostStep.Filter(new LocatorFilterOptions { HasText = "x" });
        var kept = await WebLocators.ApplyPostSteps(
            Matches,
            [Index(LocatorExpression.LastIndex), filter],
            (match, options) =>
            {
                asked.Add($"{match}:{options.HasText}");
                return Task.FromResult(match == "c");
            });
        Assert.Equal(["c"], kept);
        Assert.Equal(["c:x"], asked);
        Assert.Empty(await WebLocators.ApplyPostSteps(Matches, [Index(0), filter], Never));
    }
}
