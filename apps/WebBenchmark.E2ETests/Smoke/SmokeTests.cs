// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.Smoke;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/smoke.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class SmokeTests : BenchmarkTest
{
    // Role names match exactly unless told otherwise; the home links' names carry
    // the scenario description too, so these opt into substring matching.
    [Test]
    [Category("smoke")]
    public async Task Home_lists_the_scenarios_and_opens_one()
    {
        await App.OpenAsync();
        await Expect.That(Browser).ToHaveTitleAsync("e2e Web Benchmark");
        await Expect.That(Screen.GetByRole("heading", "Benchmark Examples")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("link", "Login Form", exact: false)).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("link", "Gift Card Purchase", exact: false)).ToBeVisibleAsync();

        await Screen.GetByRole("link", "Login Form", exact: false).TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/e/login-form");
        await Expect.That(Screen.GetByRole("heading", "Login Form")).ToBeVisibleAsync();

        await Screen.GetByRole("link", "Benchmark Examples", exact: false).TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/");
    }
}
