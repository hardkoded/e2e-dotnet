// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.PixelTools;

/// <summary>
/// The todo flow from upstream's <c>apps/testbed/tests-agent/pixel-tools.e2e.ts</c>: an ordinary marked-up page
/// where the tree alone should carry the step. Not ported: the four tests on pages drawn on a canvas
/// (<c>/canvas</c>, <c>/canvas-flow</c>, <c>/canvas-wizard</c>, <c>/canvas-form</c>), because the port has no pixel
/// tools or vision tier.
/// </summary>
[Category("RealModel")]
public sealed class PixelToolsTests : E2ETest
{
    protected override string? BaseUrl => Testbed.Url;

    [Test]
    public async Task Drives_a_marked_up_todo_page_from_the_tree()
    {
        await Browser.GotoAsync("/todos");
        var result = await Agent.ActAsync("add two todos named \"Buy milk\" and \"Walk the dog\", then mark \"Buy milk\" as done");
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("1 remaining");
        await Expect.That(Screen.GetByText("Buy milk")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Walk the dog")).ToBeVisibleAsync();
        Assert.That(result.Summary, Does.Not.Contain("screenshot"));
    }
}
