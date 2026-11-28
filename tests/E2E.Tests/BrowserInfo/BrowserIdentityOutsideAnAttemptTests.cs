// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.BrowserInfo.BrowserInfoHarness;

namespace E2E.Tests.BrowserInfo;

/// <summary><c>describe('browser identity outside an attempt')</c> of upstream's <c>browser-info.test.ts</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class BrowserIdentityOutsideAnAttemptTests
{
    [Fact]
    public async Task Is_INVALID_STATE_once_the_attempt_ended_not_a_stale_answer()
    {
        using var app = await FixtureApp.StartAsync();
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-browser-info-").FullName;
        var engine = new WebEngine(new WebEngineOptions());
        try
        {
            await using var session = await StartAttemptAsync(engine, app.Url, artifactsDir);
            var browser = FixtureOf(session, app.Url, CancellationToken.None);
            Assert.NotEqual("", browser.Version);
            await session.DisposeAsync();
            AssertInvalidState(browser);
        }
        finally
        {
            await engine.DisposeAsync();
            Directory.Delete(artifactsDir, recursive: true);
        }
    }

    [Fact]
    public async Task Stays_INVALID_STATE_on_a_fixture_kept_into_the_next_attempt_whose_browser_is_not_its_own()
    {
        using var app = await FixtureApp.StartAsync();
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-browser-info-").FullName;
        var engine = new WebEngine(new WebEngineOptions());
        try
        {
            var session = await StartAttemptAsync(engine, app.Url, artifactsDir);
            var browser = FixtureOf(session, app.Url, CancellationToken.None);
            await session.DisposeAsync();
            await using var next = await StartAttemptAsync(engine, app.Url, artifactsDir);
            AssertInvalidState(browser);
        }
        finally
        {
            await engine.DisposeAsync();
            Directory.Delete(artifactsDir, recursive: true);
        }
    }

    [Fact]
    public async Task Is_INVALID_STATE_on_a_fixture_first_made_after_its_attempt_ended_while_the_next_one_runs()
    {
        using var app = await FixtureApp.StartAsync();
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-browser-info-").FullName;
        var engine = new WebEngine(new WebEngineOptions());
        try
        {
            var first = await StartAttemptAsync(engine, app.Url, artifactsDir);
            using var ended = new CancellationTokenSource();
            await first.DisposeAsync();
            await ended.CancelAsync();
            await using var next = await StartAttemptAsync(engine, app.Url, artifactsDir);
            var late = FixtureOf(next, app.Url, ended.Token);
            AssertInvalidState(late);
        }
        finally
        {
            await engine.DisposeAsync();
            Directory.Delete(artifactsDir, recursive: true);
        }
    }

    private static void AssertInvalidState(Browser browser)
    {
        Assert.Equal(EngineErrorCodes.InvalidState, Assert.Throws<EngineException>(() => browser.Name).Code);
        Assert.Equal(EngineErrorCodes.InvalidState, Assert.Throws<EngineException>(() => browser.Version).Code);
    }
}
