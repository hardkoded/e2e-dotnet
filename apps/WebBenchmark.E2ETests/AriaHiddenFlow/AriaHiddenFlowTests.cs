// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.AriaHiddenFlow;

/// <summary>
/// Ported from <c>describe('aria hidden flow')</c> in upstream's <c>apps/web-benchmark/tests/aria-hidden-flow.e2e.ts</c>. The whole flow sits
/// under one aria-hidden container of bare divs, so no role, label, or test id query can reach it; visible text is the only handle.
/// </summary>
public sealed class AriaHiddenFlowTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheAriaHiddenFlow() => await App.OpenAsync("/e/aria-hidden-flow");

    [Test]
    public async Task Picks_the_Pro_plan_by_its_visible_text_and_confirms_it()
    {
        await Expect.That(Screen.GetByText("Choose the Pro plan")).ToBeVisibleAsync();
        await Screen.GetByText("Pro").TapAsync();
        await Expect.That(Screen.GetByText("Pro plan - $29/mo")).ToBeVisibleAsync();
        await Screen.GetByText("Confirm Pro plan").TapAsync();
        await Expect.That(Screen.GetByText("Plan activated")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Another_plan_shows_the_error_and_keeps_the_picker_open()
    {
        await Screen.GetByText("Basic").TapAsync();
        await Expect.That(Screen.GetByText("That is not the Pro plan")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Choose the Pro plan")).ToBeVisibleAsync();
    }
}
