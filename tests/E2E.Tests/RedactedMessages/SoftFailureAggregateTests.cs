// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c>: upstream redacts the soft-failure aggregate at its report boundary, which the port has not.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class SoftFailureAggregateTests
{
    [Fact]
    public async Task Redacts_a_secret_the_session_learned_after_the_failure_was_kept()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "soft aggregate");
        await session.App.OpenAsync("/");
        await Expect.Soft(session.Screen.GetByText("Token", exact: false)).ToHaveTextAsync("something else", timeout: TimeSpan.FromMilliseconds(300));
        RedactionSite.Know(session);

        var aggregate = session.CloseSoftFailures();

        Assert.NotNull(aggregate);
        Assert.Contains("<secret:token>", aggregate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RedactionSite.Token, aggregate.Message, StringComparison.Ordinal);
        Assert.NotNull(aggregate.InnerException);
        Assert.DoesNotContain(RedactionSite.Token, aggregate.InnerException.Message, StringComparison.Ordinal);
    }
}
