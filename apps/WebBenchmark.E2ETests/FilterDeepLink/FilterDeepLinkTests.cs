// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.FilterDeepLink;

/// <summary>Ported from <c>describe('filter deep link')</c> in upstream's <c>apps/web-benchmark/tests/filter-deep-link.e2e.ts</c>.</summary>
public sealed class FilterDeepLinkTests : BenchmarkTest
{
    [Test]
    public async Task Filtering_writes_the_query_to_the_URL()
    {
        await App.OpenAsync("/e/filter-deep-link");
        await Screen.GetByPlaceholder("Filter products").FillAsync("mug");
        await Expect.That(Browser).ToHaveURLAsync("/e/filter-deep-link?q=mug");
        await Expect.That(Screen.GetByTestId("product-list")).ToHaveTextAsync("Titanium Mug");
    }

    [Test]
    public async Task Opening_a_shared_link_restores_the_filter()
    {
        await App.OpenAsync("/e/filter-deep-link?q=mug");
        await Expect.That(Screen.GetByPlaceholder("Filter products")).ToHaveValueAsync("mug");
        await Expect.That(Screen.GetByTestId("product-list")).ToHaveTextAsync("Titanium Mug");
    }
}
