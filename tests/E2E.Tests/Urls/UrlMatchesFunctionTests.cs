// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using InternalUrls = E2E.Internal.Urls;

namespace E2E.Tests.Urls;

/// <summary>
/// Upstream's <c>describe('urlMatches')</c> (<c>tests/unit/urls.test.ts</c>) as the function <c>Urls.UrlMatches</c>. The browser-level
/// cases of the same comparison are in <see cref="UrlMatchesTests"/>, which needs the browser collection. Not ported: "rejects a predicate
/// instead of matching every URL", because <c>UrlMatches</c> takes a <see cref="Regex"/> and a predicate does not compile.
/// </summary>
public sealed class UrlMatchesFunctionTests
{
    private const string Base = "http://localhost:3000/";

    [Fact]
    public void Resolves_relative_expected_strings_against_the_base_URL()
    {
        Assert.True(InternalUrls.UrlMatches("http://localhost:3000/billing", "/billing", Base));
    }

    [Fact]
    public void Compares_exactly_after_WHATWG_serialization()
    {
        Assert.True(InternalUrls.UrlMatches("http://localhost:3000/a", "http://localhost:3000/a/../a", Base));
        Assert.False(InternalUrls.UrlMatches("http://localhost:3000/a?x=1", "/a", Base));
    }

    [Fact]
    public void Tests_regexps_against_the_complete_serialized_URL()
    {
        Assert.True(InternalUrls.UrlMatches("http://localhost:3000/beta/board", new Regex("beta")));
        Assert.False(InternalUrls.UrlMatches("http://localhost:3000/alpha", new Regex("beta")));
    }

    [Fact]
    public void IgnoreCase_folds_a_string_comparison_and_sets_or_clears_the_i_flag_of_a_regexp()
    {
        const string current = "http://localhost:3000/Billing";

        Assert.False(InternalUrls.UrlMatches(current, "/billing", Base));
        Assert.True(InternalUrls.UrlMatches(current, "/billing", Base, ignoreCase: true));
        Assert.False(InternalUrls.UrlMatches(current, "/billing", Base, ignoreCase: false));
        Assert.True(InternalUrls.UrlMatches(current, new Regex("billing$"), ignoreCase: true));
        Assert.True(InternalUrls.UrlMatches(current, new Regex("billing$", RegexOptions.IgnoreCase)));
        Assert.False(InternalUrls.UrlMatches(current, new Regex("billing$", RegexOptions.IgnoreCase), ignoreCase: false));
    }
}
