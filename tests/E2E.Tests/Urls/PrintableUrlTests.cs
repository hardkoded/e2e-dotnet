// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Urls;

/// <summary>Upstream: packages/e2e/tests/unit/urls.test.ts, describe('printableUrl').</summary>
public sealed class PrintableUrlTests
{
    [Fact]
    public void Drops_userinfo_query_and_fragment_any_of_which_may_carry_a_credential()
    {
        Assert.Equal("http://127.0.0.1:4310/health", E2E.Internal.Urls.PrintableUrl("http://user:pw@127.0.0.1:4310/health?token=abc#x"));
        Assert.Equal("http://127.0.0.1:4310/", E2E.Internal.Urls.PrintableUrl("http://127.0.0.1:4310"));
    }
}
