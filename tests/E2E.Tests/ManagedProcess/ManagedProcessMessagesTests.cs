// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

/// <summary>.NET-only: no upstream <c>it</c> covers how a message shows the readiness URL, which the port cleans of userinfo, query and fragment.</summary>
public sealed class ManagedProcessMessagesTests
{
    [Fact]
    public async Task Shows_the_readiness_URL_without_userinfo_query_or_fragment()
    {
        await using var running = TestServer.Answering();
        var secret = new UriBuilder(running.Url) { UserName = "user", Password = "hunter2", Query = "token=abc123", Fragment = "frag" }.Uri.AbsoluteUri;
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["idle"], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
            Path.GetTempPath(),
            new Readiness(secret));
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_ALREADY_RUNNING", failure.Code);
        Assert.Equal($"{running.Url} already answered before app.command started; stop that process or set reuseExisting: true", failure.Message);
        foreach (var hidden in new[] { "hunter2", "abc123", "frag" })
        {
            Assert.DoesNotContain(hidden, failure.Message, StringComparison.Ordinal);
        }
    }
}
