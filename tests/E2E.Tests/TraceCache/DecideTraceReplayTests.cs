// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCache;

public sealed class DecideTraceReplayTests
{
    [Fact]
    public async Task Replays_a_navigate_opening_trace_from_anywhere()
    {
        var directory = SessionTests.TempCache();
        var calls = 0;
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("Statement:", StringComparison.Ordinal))
            {
                return ModelResponses.Done(text.Contains("Prorated", StringComparison.Ordinal) ? "passed" : "failed", "judged", text.Contains("Prorated", StringComparison.Ordinal) ? null : "ASSERTION_FAILED");
            }

            calls++;
            if (text.Contains("tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "Upgraded to Pro.");
            }

            return text.Contains("navigated", StringComparison.Ordinal)
                ? ModelResponses.Tap("button", "Upgrade to Pro")
                : ModelResponses.Call("navigate", new { url = "/settings/billing" });
        });

        async Task RunAsync(string start)
        {
            await using var session = await SessionTests.StartAsync(directory, model, attempt: 1);
            await session.App.OpenAsync(start);
            await session.Agent.ActAsync("open billing and upgrade to Pro");
            await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
            session.Complete();
        }

        await RunAsync("/");
        Assert.True(calls > 0);
        calls = 0;
        await RunAsync("/home");
        Assert.Equal(0, calls);
    }
}
