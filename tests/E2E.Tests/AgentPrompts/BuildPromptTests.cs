// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentPrompts;

/// <summary>
/// How a judgment is shown where the screen is (upstream's <c>buildPrompt</c> tests in <c>agent-prompts.test.ts</c>). The port has no
/// <c>buildPrompt</c>: the location a judgment reads is <see cref="E2E.AgentObservation.JudgeLocation"/>, and the request's header is the
/// judge's own. The end-to-end form is in <c>AgentJudgmentTierTests</c>.
/// </summary>
public sealed class BuildPromptTests
{
    private const string App = "https://app.test";

    [Fact]
    public void names_a_location_on_the_app_as_its_path()
    {
        Assert.Equal("/tasks", E2E.AgentObservation.JudgeLocation("https://app.test/tasks", App).Text);
    }

    [Fact]
    public void keeps_the_origin_off_the_app_so_another_site_never_reads_as_an_app_route()
    {
        var judged = E2E.AgentObservation.JudgeLocation("https://idp.test/login", App);
        Assert.Equal("https://idp.test/login", judged.Text);
        Assert.True(judged.Url);
    }

    [Fact]
    public void keeps_the_query_and_fragment_as_the_acting_agent_reads_them()
    {
        // The location arrives already redacted, so its secret marker reads percent-encoded in the address.
        var judged = E2E.AgentObservation.JudgeLocation("https://app.test/tasks?status=active&token=%3Csecret:token%3E#done", App);
        Assert.Equal("/tasks?status=active&token=%3Csecret:token%3E#done", judged.Text);
    }

    [Fact]
    public void never_shows_userinfo()
    {
        var judged = E2E.AgentObservation.JudgeLocation("https://user:pw@idp.test/cb", App);
        Assert.Equal("https://idp.test/cb", judged.Text);
        Assert.DoesNotContain("user:pw", judged.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("about:blank")]
    [InlineData("blob:https://other.test/x")]
    [InlineData("chrome-error://chromewebdata/")]
    public void keeps_a_location_that_is_no_http_URL_as_it_is(string location)
    {
        var judged = E2E.AgentObservation.JudgeLocation(location, App);
        Assert.Equal(location, judged.Text);
        Assert.False(judged.Url);
    }

    [Fact]
    public void cuts_a_long_location_so_a_long_query_cannot_overrun_the_request()
    {
        var judged = E2E.AgentObservation.JudgeLocation("https://app.test/tasks?state=" + new string('a', 3_000), App);
        Assert.EndsWith("…", judged.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 300), judged.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void cuts_a_long_device_location_too()
    {
        var judged = E2E.AgentObservation.JudgeLocation("com.example / " + new string('x', 300), App);
        Assert.EndsWith("…", judged.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 300), judged.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void keeps_a_location_that_is_not_a_URL_inside_its_text()
    {
        // A device reports `<app> / <screen title>`, and the title is the app's text.
        Assert.Equal("com.example / Say \"hi\"", E2E.AgentObservation.JudgeLocation("com.example / Say \"hi\"", App).Text);
    }
}
