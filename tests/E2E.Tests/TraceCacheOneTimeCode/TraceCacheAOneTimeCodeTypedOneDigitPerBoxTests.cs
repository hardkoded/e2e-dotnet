// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.TraceCacheOneTimeCode;

/// <summary>
/// Upstream's <c>describe('trace cache: a one-time code typed one digit per box')</c> of <c>tests/integration/trace-cache-one-time-code.test.ts</c>:
/// the page issues a new code on every load, so no digit of it may replay. The first run records the step as gaps from its first box on,
/// and the second hands the whole code to the executor without typing the last run's digits. Upstream's recorded <c>tool type (run-time
/// value)</c> action is the port's <c>tool</c> kind with the text <c>type (run-time value)</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheAOneTimeCodeTypedOneDigitPerBoxTests
{
    private sealed class CodeRecord
    {
        public int Calls { get; set; }

        /// <summary>Whether the executor's first look found a verdict on a wrong code: a replay typed one.</summary>
        public bool SawIncorrect { get; set; }
    }

    // Reads the code off the page and types it one digit per box, as a model does. No model.
    private static TestExecutor CodeExecutor(CodeRecord record) => new("code-executor", async (context, token) =>
        {
            record.Calls++;
            var observation = await context.ObserveAsync(cancellationToken: token);
            record.SawIncorrect |= observation.Text.Contains("Incorrect code", StringComparison.Ordinal);
            var code = Regex.Match(observation.Text, @"Your code is (\d{6})").Groups[1];
            if (!code.Success)
            {
                return Failed("no code on the page");
            }

            for (var index = 0; index < code.Value.Length; index++)
            {
                await context.Actions.TypeAsync(
                    TargetFor(observation.Text, $"textbox \"Digit {index + 1}\""),
                    code.Value[index].ToString(),
                    token
                );
            }

            observation = await context.ObserveAsync(cancellationToken: token);
            return observation.Text.Contains("Code verified", StringComparison.Ordinal)
                ? Passed("typed the code")
                : Failed("the code was not accepted");
        }) { Version = "test" };

    private static async Task<ActResult> RunAsync(string directory, string appUrl, CodeRecord record)
    {
        await using var session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                AgentExecutor = CodeExecutor(record),
                BaseUrl = appUrl,
                AppIdentity = "fixture-app",
                Cache = new FileStepCache(directory),
                CacheEnabled = true,
                CacheMode = CacheMode.ReadWrite,
                TestTitle = "confirms with the code",
                ActionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(30),
                TestTimeout = TimeSpan.FromSeconds(60),
            }
        );
        await session.App.OpenAsync("/one-time-code");
        var result = await session.Agent.ActAsync("type the code shown on the page into the code boxes");
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("Code verified");
        session.Complete();
        return result;
    }

    [Fact]
    public async Task Records_every_box_as_a_gap_and_never_types_the_last_runs_code()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        var first = new CodeRecord();
        await RunAsync(directory, site.Url, first);

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        // A lone digit is no word of the sentence that shows the code; only the field's purpose makes each fill this run's data.
        Assert.Equal(6, actions.Count);
        foreach (var action in actions)
        {
            Assert.Equal(
                ("tool", "type (run-time value)", DerivedReason.MintedToken),
                (action.Kind, action.Text, action.Derived)
            );
        }

        var second = new CodeRecord();
        var result = await RunAsync(directory, site.Url, second);

        Assert.Equal(1, second.Calls);
        Assert.False(second.SawIncorrect);
        Assert.Equal(("missed", "gap", 6), (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.TotalActions));
    }
}
