// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Tests.Crosscheck;

// describe('web-benchmark scenarios') of upstream's crosscheck.test.ts: it shares the page and the results of the outer describe.
public sealed partial class CrossCheckTests
{
    private static readonly string? Benchmark = Environment.GetEnvironmentVariable("CROSSCHECK_BENCHMARK_URL");

    [BenchmarkFact]
    public async Task Every_scenario()
    {
        await check.Page.GotoAsync(Benchmark!);
        var slugs = await check
            .Page.Locator("a[href^=\"/e/\"]")
            .EvaluateAllAsync<string[]>(
                "(links) => [...new Set(links.map((link) => new URL(link.href).pathname.slice('/e/'.length)))]"
            );
        Assert.NotEmpty(slugs);
        foreach (var slug in slugs.Order(StringComparer.Ordinal))
        {
            await check.Page.GotoAsync(
                new Uri(new Uri(Benchmark!), $"/e/{slug}").AbsoluteUri,
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }
            );
            await CheckAsync($"benchmark: {slug}");
        }
    }
}

/// <summary>A fact that runs only when <c>CROSSCHECK_BENCHMARK_URL</c> names a running benchmark app (upstream's <c>describe.skipIf</c>). .NET-only: xUnit decides a skip at discovery.</summary>
public sealed class BenchmarkFactAttribute : FactAttribute
{
    public BenchmarkFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CROSSCHECK_BENCHMARK_URL") is null)
        {
            Skip = "CROSSCHECK_BENCHMARK_URL is not set.";
        }
    }
}
