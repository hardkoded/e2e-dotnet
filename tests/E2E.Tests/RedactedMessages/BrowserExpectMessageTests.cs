// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c> covers <c>toHaveURL</c> or <c>toHaveTitle</c> redaction: upstream redacts their failures at its report boundary.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class BrowserExpectMessageTests
{
    [Fact]
    public async Task Redacts_a_secret_the_title_shows()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "title message");
        await session.App.OpenAsync("/");
        RedactionSite.Know(session);

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            Expect.That(session.Browser).ToHaveTitleAsync("something else", TimeSpan.FromMilliseconds(300)));

        Assert.Contains("<secret:token>", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_a_secret_the_URL_shows()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "url message");
        await session.App.OpenAsync("/?token=" + RedactionSite.Token);
        RedactionSite.Know(session);

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            Expect.That(session.Browser).ToHaveURLAsync("/other", timeout: TimeSpan.FromMilliseconds(300)));

        Assert.Contains("<secret:token>", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }
}
