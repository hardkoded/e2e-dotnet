// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.NUnit.Tests.Browser;

/// <summary>
/// <c>test.describe('browser identity in a serial group')</c> of upstream's <c>apps/testbed/tests/browser.e2e.ts</c>. A serial group's members
/// share one session, each under an attempt of its own.
/// </summary>
[Serial]
public sealed class BrowserIdentityInASerialGroupTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    private static string _first = "";

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public void The_first_member_reads_the_identity()
    {
        Assert.That(
            new[] { BrowserName.Chromium, BrowserName.Firefox, BrowserName.Webkit },
            Does.Contain(Browser.Name)
        );
        _first = Browser.Version;
    }

    [Test]
    public void The_second_member_reads_the_same_browser()
    {
        Assert.That(Browser.Version, Is.EqualTo(_first));
    }
}
