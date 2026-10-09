// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.Urls;

/// <summary>
/// Upstream tests <c>urlMatches</c> as a function. The port has no such function: the
/// comparison lives in <see cref="BrowserExpect.ToHaveURLAsync(string, bool?, TimeSpan?, CancellationToken)"/>,
/// so these run it against the URL a real browser reports.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class UrlMatchesTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

    [Fact]
    public Task Resolves_relative_expected_strings_against_the_base_URL() => AtAsync("/billing", async (browser, baseUrl) =>
    {
        await Expect.That(browser).ToHaveURLAsync("/billing", timeout: Short);
    });

    [Fact]
    public Task Compares_exactly_after_WHATWG_serialization() => AtAsync("/a", async (browser, baseUrl) =>
    {
        await Expect.That(browser).ToHaveURLAsync(baseUrl + "a/../a", timeout: Short);
        await Expect.That(browser).ToHaveURLAsync("/a/../a", timeout: Short);
        await browser.GotoAsync("/a?x=1");
        await Expect.That(browser).Not.ToHaveURLAsync("/a", timeout: Short);
        await Expect.That(browser).ToHaveURLAsync("/a?x=1", timeout: Short);

        // The browser keeps what Uri would rewrite: a percent-encoded tilde, a space, and a non-ASCII letter.
        await browser.GotoAsync("/a%7Eb c/café");
        await Expect.That(browser).ToHaveURLAsync("/a%7Eb%20c/caf%C3%A9", timeout: Short);
        await Expect.That(browser).ToHaveURLAsync("/a~b c/café", timeout: Short);
    });

    [Fact]
    public Task Tests_regexps_against_the_complete_serialized_URL() => AtAsync("/beta/board", async (browser, baseUrl) =>
    {
        await Expect.That(browser).ToHaveURLAsync(new Regex("beta"), timeout: Short);
        await Expect.That(browser).ToHaveURLAsync(new Regex("^http://127\\.0\\.0\\.1:\\d+/beta/board$"), timeout: Short);
        await Expect.That(browser).Not.ToHaveURLAsync(new Regex("alpha"), timeout: Short);
    });

    [Fact]
    public Task IgnoreCase_folds_a_string_comparison_and_sets_or_clears_the_i_flag_of_a_regexp() => AtAsync("/Billing", async (browser, baseUrl) =>
    {
        await Expect.That(browser).Not.ToHaveURLAsync("/billing", timeout: Short);
        await Expect.That(browser).ToHaveURLAsync("/billing", ignoreCase: true, timeout: Short);
        await Expect.That(browser).Not.ToHaveURLAsync("/billing", ignoreCase: false, timeout: Short);
        await Expect.That(browser).ToHaveURLAsync(new Regex("billing$"), ignoreCase: true, timeout: Short);
        await Expect.That(browser).ToHaveURLAsync(new Regex("billing$", RegexOptions.IgnoreCase), timeout: Short);
        await Expect.That(browser).Not.ToHaveURLAsync(new Regex("billing$", RegexOptions.IgnoreCase), ignoreCase: false, timeout: Short);
    });

    private static async Task AtAsync(string path, Func<Browser, string, Task> body)
    {
        using var site = await TinySite.StartAsync("<!DOCTYPE html><html><head><title>Page</title></head><body></body></html>");
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync(path);
            await body(session.Browser, site.Url);
        });
    }
}
