// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c>: upstream redacts a <c>waitFor</c> failure at its report boundary, which the port has not.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WaitForMessageTests
{
    [Fact]
    public async Task Redacts_a_secret_the_locator_names()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "waitFor message");
        await session.App.OpenAsync("/");
        RedactionSite.Know(session);

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            session.Screen.GetByText("Secret " + RedactionSite.Token).WaitForAsync(new LocatorWaitForOptions { Timeout = TimeSpan.FromMilliseconds(300) }));

        Assert.Equal("TIMEOUT", failure.Code);
        Assert.Contains("<secret:token>", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }
}
