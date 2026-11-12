// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorList;

/// <summary>
/// <c>test.describe('locator list')</c> of upstream's <c>apps/testbed/tests/locator-list.e2e.ts</c> (Playwright's
/// <c>locator-list.spec.ts</c>). <c>div &gt;&gt; p</c> is the CSS <c>div p</c>.
/// </summary>
public sealed class LocatorListTests : PlaywrightTest
{
    [Test]
    public async Task Locator_all_should_work()
    {
        await Browser.GotoAsync(Content("<div><p>A</p><p>B</p><p>C</p></div>"));
        var texts = new List<string?>();
        foreach (var p in await Browser.Locator("div p").AllAsync())
        {
            texts.Add(await p.TextContentAsync());
        }

        Assert.That(texts, Is.EqualTo(new[] { "A", "B", "C" }));
    }
}
