// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Locators;

/// <summary>
/// Upstream's <c>describe('frameSelectors')</c> (<c>packages/web/tests/unit/locators.test.ts</c>).
/// </summary>
public sealed class FrameSelectorsTests
{
    private static readonly LocatorExpression Textbox = new() { Kind = "role", Role = "textbox" };

    private static LocatorExpression InFrame(string selector) => new() { Kind = "frame", Selector = selector, Source = Textbox };

    [Fact]
    public void Lists_the_frames_along_an_expression_outermost_first()
    {
        Assert.Equal(
            ["#outer", "#inner"],
            WebLocators.FrameSelectors(new LocatorExpression { Kind = "frame", Selector = "#outer", Source = InFrame("#inner") }));
        Assert.Equal(
            ["#left"],
            WebLocators.FrameSelectors(new LocatorExpression { Kind = "union", Left = InFrame("#left"), Right = InFrame("#left") }));
    }

    [Fact]
    public void Rejects_a_union_whose_branches_live_in_different_documents()
    {
        foreach (var right in new[] { InFrame("#right"), Textbox })
        {
            var thrown = Assert.Throws<EngineException>(
                () => WebLocators.FrameSelectors(new LocatorExpression { Kind = "union", Left = InFrame("#left"), Right = right }));
            Assert.Equal("UNSUPPORTED_CAPABILITY", thrown.Code);
            Assert.False(thrown.Retryable);
        }
    }
}
