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
/// Upstream's <c>describe('trace cache: a one-time code typed into each focused box')</c> of <c>tests/integration/trace-cache-one-time-code.test.ts</c>:
/// each digit is typed into the focused box (<c>typeText</c>, which names no field) on a screen that listed a one-time-code field; unmarked
/// boxes keep today's rules. Upstream's recorded <c>tool type (run-time value)</c> action is the port's <c>tool</c> kind with the text
/// <c>type (run-time value)</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheAOneTimeCodeTypedIntoEachFocusedBoxTests
{
    private sealed class CodeRecord
    {
        public int Calls { get; set; }

        public bool SawIncorrect { get; set; }
    }

    // As the per-box executor, but taps each box and types its digit into the focused field, as a model does with `type` and no target
    // after `tap`, or a custom executor that types at the caret. No model.
    private static TestExecutor FocusedCodeExecutor(CodeRecord record) =>
        new("focused-code-executor", async (context, token) =>
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
                await context.Actions.TapAsync(TargetFor(observation.Text, $"textbox \"Digit {index + 1}\""), token);
                await context.Actions.TypeTextAsync(
                    code.Value[index].ToString(),
                    new TypeTextOptions { Replace = false },
                    token
                );
            }

            observation = await context.ObserveAsync(cancellationToken: token);
            return observation.Text.Contains("Code verified", StringComparison.Ordinal)
                ? Passed("typed the code")
                : Failed("the code was not accepted");
        }) { Version = "test" };

    private static async Task<ActResult> RunAsync(string directory, string appUrl, string path, CodeRecord record)
    {
        await using var session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                AgentExecutor = FocusedCodeExecutor(record),
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
        await session.App.OpenAsync(path);
        var result = await session.Agent.ActAsync("type the code shown on the page into the code boxes");
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("Code verified");
        session.Complete();
        return result;
    }

    [Fact]
    public async Task Records_every_focused_digit_as_a_gap_on_a_screen_that_listed_a_one_time_code_field()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(directory, site.Url, "/one-time-code", new CodeRecord());

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        // Each tap names its box and replays; each digit typed after it is this run's data.
        Assert.DoesNotContain(actions, action => action.Kind == "typeText");
        var gaps = actions.Where(action => action.Kind == "tool").ToList();
        Assert.Equal(6, gaps.Count);
        foreach (var gap in gaps)
        {
            Assert.Equal(("type (run-time value)", DerivedReason.MintedToken), (gap.Text, gap.Derived));
        }

        var second = new CodeRecord();
        var result = await RunAsync(directory, site.Url, "/one-time-code", second);

        Assert.Equal(1, second.Calls);
        Assert.False(second.SawIncorrect);
        // The first tap names its box and replays; replay hands off at the first digit.
        Assert.Equal(
            ("agent-concluded", "gap", DerivedReason.MintedToken, 1),
            (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.Derived, result.Cache?.ReplayedActions)
        );
    }

    [Fact]
    public async Task Keeps_recording_focused_digits_on_boxes_that_declare_no_purpose()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(directory, site.Url, "/one-time-code-plain", new CodeRecord());

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.DoesNotContain(actions, action => action.Kind == "tool");
        Assert.Equal(6, actions.Count(action => action.Kind == "typeText"));
    }
}
