// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.PixelTools;

/// <summary>
/// Upstream's <c>apps/testbed/tests-agent/pixel-tools.e2e.ts</c> (tester-army/e2e <c>79e2e13</c>): the pixel tools against a real
/// model, on pages drawn on a canvas (<c>/canvas</c>, <c>/canvas-flow</c>, <c>/canvas-wizard</c>, <c>/canvas-form</c>) and on an
/// ordinary marked-up page where the tree alone should carry the step.
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

    [Test]
    public async Task Enters_a_code_on_the_drawn_keypad_through_points()
    {
        await Browser.GotoAsync("/canvas-flow");
        // Nine model turns, each with a screenshot, take 28 to 30 s: the step's default timeout is too short for the port's providers.
        await Agent.ActAsync("on the drawn keypad enter the code 31415 digit by digit, check the display shows it, then press OK", new ActOptions { Timeout = TimeSpan.FromMinutes(2) });
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("code accepted: 31415");
    }

    [Test]
    public async Task Walks_the_drawn_wizard_through_points()
    {
        await Browser.GotoAsync("/canvas-wizard");
        // Sixteen taps through a model that reads each screen: the step's default timeout is too short for the port's providers.
        await Agent.ActAsync("complete all eight steps of the drawn wizard: on each screen tap the shape its instruction names, then tap Next", new ActOptions { Timeout = TimeSpan.FromMinutes(4) });
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("wizard done: ok,ok,ok,ok,ok,ok,ok,ok");
    }

    [Test]
    public async Task Picks_a_canvas_pin_the_bare_canvas_opens_with_a_screenshot()
    {
        await Browser.GotoAsync("/canvas");
        await Agent.ActAsync("pick the red pin on the map");
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("picked the red pin");
    }

    [Test]
    public async Task Fills_a_form_drawn_on_a_canvas_through_type_at_and_the_keyboard()
    {
        await Browser.GotoAsync("/canvas-form");
        await Agent.ActAsync("fill the drawn form: Name is Ada, City is Oslo, then press Submit");
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("submitted: name=Ada city=Oslo");
    }
}
