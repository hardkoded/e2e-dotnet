// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.JudgmentParams;

/// <summary>
/// <c>agent.assert</c> and <c>agent.waitFor</c> take <c>params</c> as <c>agent.act</c> does: the judge reads a <c>unique()</c> value as
/// its string and a <c>Secret</c> by name only. Upstream runs one project for the describe and reads each result from its report. The
/// port runs each <c>it</c> in its own session, with a scripted judge in place of upstream's fake model.
/// </summary>
public sealed partial class JudgmentParamsOnTheBuiltInJudgeTests
{
    private const string SecretValue = "hunter2-secret";

    /// <summary>A registered value with a quote, so the judge prompt's JSON would spell it escaped.</summary>
    private const string QuotedSecret = "pa\"ss-7781";

    [Fact]
    public async Task Hands_assert_the_unique_value_and_plain_params_next_to_the_name_instruction()
    {
        var judge = Judge();
        await using var session = await StartAsync(judge);
        await session.App.OpenAsync("/");
        await session.Agent.AssertAsync(
            "the table shows company {name} on the {plan} plan",
            new AssertOptions { Params = new Dictionary<string, object?> { ["name"] = Values.Unique("acme-4821"), ["plan"] = "pro" } });

        Assert.Contains(
            "Statement: the table shows company {name} on the {plan} plan\nParameters: {\"name\":\"acme-4821\",\"plan\":\"pro\"}\n\n",
            judge.Requests[0].Messages[0].Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hands_waitFor_the_unique_value()
    {
        var judge = Judge();
        await using var session = await StartAsync(judge);
        await session.App.OpenAsync("/");
        await session.Agent.WaitForAsync(
            "the table shows company {name}",
            new WaitForOptions { Params = new Dictionary<string, object?> { ["name"] = Values.Unique("acme-7310") }, Interval = TimeSpan.FromMilliseconds(100) });

        Assert.Contains(
            "Statement: the table shows company {name}\nParameters: {\"name\":\"acme-7310\"}\n\n",
            judge.Requests[0].Messages[0].Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shows_the_judge_a_secret_param_as_its_name_never_its_value()
    {
        var judge = Judge();
        await using var session = await StartAsync(judge);
        await session.App.OpenAsync("/");
        await session.Agent.AssertAsync(
            "the password field holds {password}",
            new AssertOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("member.password", SecretValue, "password") } });

        Assert.Contains("\"password\":{\"kind\":\"secret\",\"name\":\"member.password\"", judge.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, Everything(judge), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_a_registered_value_a_plain_param_spells_out_quotes_included()
    {
        var judge = Judge();
        await using var session = await StartAsync(judge);
        session.Register("quoted.password", QuotedSecret);
        await session.App.OpenAsync("/");
        await session.Agent.AssertAsync(
            "the note reads {note}",
            new AssertOptions { Params = new Dictionary<string, object?> { ["note"] = QuotedSecret } });

        Assert.Matches(NoteParameter(), judge.Requests[0].Messages[0].Content);
        var sent = Everything(judge);
        Assert.DoesNotContain(QuotedSecret, sent, StringComparison.Ordinal);
        Assert.DoesNotContain(QuotedSecret.Replace("\"", "\\\"", StringComparison.Ordinal), sent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream also passes <c>params: 'acme'</c>, which is not a plain object; the port types params as a dictionary, so that
    /// shape cannot be built. The assert case refuses a non-finite number instead.
    /// </summary>
    [Fact]
    public async Task Partial_Rejects_invalid_params_naming_the_method_that_took_them()
    {
        await using var session = await StartAsync(Judge());
        await session.App.OpenAsync("/");

        var assertError = await Assert.ThrowsAsync<TestException>(() => session.Agent.AssertAsync(
            "the Home heading is visible",
            new AssertOptions { Params = new Dictionary<string, object?> { ["n"] = double.NaN } }));
        Assert.Equal("INVALID_ARGUMENT", assertError.Code);
        Assert.Contains("agent.assert params contains a non-finite number", assertError.Message, StringComparison.Ordinal);

        var looped = new Dictionary<string, object?> { ["name"] = "acme" };
        looped["self"] = looped;
        var waitForError = await Assert.ThrowsAsync<TestException>(() => session.Agent.WaitForAsync(
            "the Home heading is visible",
            new WaitForOptions { Params = looped }));
        Assert.Equal("INVALID_ARGUMENT", waitForError.Code);
        Assert.Contains("agent.waitFor params contains a cycle", waitForError.Message, StringComparison.Ordinal);
    }

    private static ScriptedModel Judge() => new(_ => ModelResponses.Judgment("holds", "the observation supports it"));

    private static string Everything(ScriptedModel judge) =>
        string.Join('\n', judge.Requests.Select(request => request.System + "\n" + string.Join('\n', request.Messages.Select(message => message.Content))));

    private static Task<E2ESession> StartAsync(ScriptedModel judge) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "judgment params",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
        });

    [GeneratedRegex("Parameters: \\{\"note\":\"<secret:[^>]+>\"\\}")]
    private static partial Regex NoteParameter();
}
