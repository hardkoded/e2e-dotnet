// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AppLinkBase;

/// <summary>Upstream's <c>describe('refusesLink')</c> (<c>tests/unit/app-link-base.test.ts</c>).</summary>
public sealed class RefusesLinkTests
{
    [Fact]
    public void Refuses_the_schemes_that_read_local_content_or_inline_a_document_and_what_does_not_parse()
    {
        foreach (
            var link in new[]
            {
                "file:///etc/passwd",
                "data:text/html,hi",
                "javascript:alert(1)",
                "view-source:file:///etc",
                "blob:x",
                "filesystem:x",
                "not a link",
            }
        )
        {
            Assert.True(TargetApps.RefusesLink(link), link);
        }

        Assert.False(TargetApps.RefusesLink("shopapp://orders/42"));
        Assert.False(TargetApps.RefusesLink("https://shop.example.com/"));
    }

    // Partial: upstream imports isRefusedLinkScheme from '@e2e-dev/core/engine'; the port's engines reach it as E2E.Internal.Urls.IsRefusedLinkScheme of the E2E assembly.
    [Fact]
    public void Partial_Shares_its_list_with_engines_by_scheme_in_any_case()
    {
        Assert.True(E2E.Internal.Urls.IsRefusedLinkScheme("JavaScript:"));
        Assert.False(E2E.Internal.Urls.IsRefusedLinkScheme("shopapp:"));
    }
}
