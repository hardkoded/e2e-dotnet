// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit.Tests.E2ETest;

/// <summary>The fixture the tests of this directory derive from: the playground at <c>/browser</c>, with the replay cache off.</summary>
public abstract class BrowserTest : E2E.TUnit.E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected static readonly TimeSpan SoftTimeout = TimeSpan.FromMilliseconds(200);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected string? Title { get; private set; }

    protected override string CacheTitle(TUnitContext test) => Title = base.CacheTitle(test);

    [Before(HookType.Test)]
    public async Task OpenBrowserPageAsync()
    {
        await App.OpenAsync("/browser");
    }
}
