// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c>: upstream redacts an <c>expect.poll</c> failure at its report boundary, which the port has not.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PollMessageTests
{
    private static readonly PollOptions Short = new() { Timeout = TimeSpan.FromMilliseconds(300), Interval = TimeSpan.FromMilliseconds(20) };

    [Fact]
    public async Task Redacts_a_secret_the_last_read_value_holds()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "poll message");
        RedactionSite.Know(session);

        var failure = await Assert.ThrowsAsync<TestException>(() => Expect.Poll(() => "Token " + RedactionSite.Token, Short).ToBeAsync("something else"));

        Assert.Contains("<secret:token>", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_a_secret_the_failed_read_names_and_the_extra_line()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        await using var session = await RedactionSite.StartAsync(site, "poll read error");
        RedactionSite.Know(session);
        var options = new PollOptions { Timeout = Short.Timeout, Interval = Short.Interval, Message = "while reading " + RedactionSite.Token };

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            Expect.Poll<string>((Func<string>)(() => throw new InvalidOperationException("no node reads " + RedactionSite.Token)), options).ToBeAsync("x"));

        Assert.Equal(2, failure.Message.Split("<secret:token>").Length - 1);
        Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_the_message_alone_once_the_session_is_gone()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        var session = await RedactionSite.StartAsync(site, "poll after the session");
        RedactionSite.Know(session);
        await session.DisposeAsync();

        var failure = await Assert.ThrowsAsync<TestException>(() => Expect.Poll(() => "Token " + RedactionSite.Token, Short).ToBeAsync("something else"));

        Assert.Contains(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
    }
}
