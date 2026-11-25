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
/// Upstream's <c>describe('trace cache: a code typed across boxes that declare no purpose')</c> of
/// <c>tests/integration/trace-cache-one-time-code.test.ts</c>: the pieces typed in a row join into the code the page showed, so the
/// recording is one gap from the first piece. Upstream's recorded <c>tool type (run-time value)</c> action is the port's <c>tool</c> kind
/// with the text <c>type (run-time value)</c>, and its <c>type</c> action is the port's <c>fill</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheACodeTypedAcrossBoxesThatDeclareNoPurposeTests
{
    private const string Instruction = "type the code shown on the page into the code boxes";

    private sealed class CodeRecord
    {
        public int Calls { get; set; }

        public bool SawIncorrect { get; set; }
    }

    // Each executor reads the code off the page and types it its own way. No model.
    private static TestExecutor Executor(
        string name,
        CodeRecord record,
        Func<StepExecutorContext, string, string, CancellationToken, Task> type
    ) => new(name, async (context, token) =>
        {
            record.Calls++;
            var observation = await context.ObserveAsync(cancellationToken: token);
            record.SawIncorrect |= observation.Text.Contains("Incorrect code", StringComparison.Ordinal);
            var code = Regex.Match(observation.Text, @"Your code is (\d{6})").Groups[1];
            if (!code.Success)
            {
                return Failed("no code on the page");
            }

            await type(context, observation.Text, code.Value, token);
            observation = await context.ObserveAsync(cancellationToken: token);
            return observation.Text.Contains("Code verified", StringComparison.Ordinal)
                ? Passed("typed the code")
                : Failed("the code was not accepted");
        }) { Version = "test" };

    // Types it one digit per box, as a model does.
    private static async Task DigitsAsync(
        StepExecutorContext context,
        string text,
        string code,
        CancellationToken token
    )
    {
        for (var index = 0; index < code.Length; index++)
        {
            await context.Actions.TypeAsync(
                TargetFor(text, $"textbox \"Digit {index + 1}\""),
                code[index].ToString(),
                token
            );
        }
    }

    // Taps each box and types its digit into the focused field.
    private static async Task FocusedAsync(
        StepExecutorContext context,
        string text,
        string code,
        CancellationToken token
    )
    {
        for (var index = 0; index < code.Length; index++)
        {
            await context.Actions.TapAsync(TargetFor(text, $"textbox \"Digit {index + 1}\""), token);
            await context.Actions.TypeTextAsync(code[index].ToString(), new TypeTextOptions { Replace = false }, token);
        }
    }

    // Types it three and three: the first half into the first box, then Tab and the second half into the focused box.
    private static async Task HalvesAsync(
        StepExecutorContext context,
        string text,
        string code,
        CancellationToken token
    )
    {
        await context.Actions.TypeAsync(TargetFor(text, "textbox \"Part 1\""), code[..3], token);
        await context.Actions.PressKeyAsync("Tab", token);
        await context.Actions.TypeTextAsync(code[3..], new TypeTextOptions { Replace = false }, token);
    }

    // As HalvesAsync, but presses Enter in the first box and types the second half into the second by name: an action other than a tap or
    // Tab between two pieces, which ends their run.
    private static async Task EnterBetweenAsync(
        StepExecutorContext context,
        string text,
        string code,
        CancellationToken token
    )
    {
        await context.Actions.TypeAsync(TargetFor(text, "textbox \"Part 1\""), code[..3], token);
        await context.Actions.PressKeyAsync("Enter", token);
        await context.Actions.TypeAsync(TargetFor(text, "textbox \"Part 2\""), code[3..], token);
    }

    private static async Task<ActResult> RunAsync(
        string directory,
        string appUrl,
        string path,
        TestExecutor executor,
        string instruction = Instruction
    )
    {
        await using var session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                AgentExecutor = executor,
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
        var result = await session.Agent.ActAsync(instruction);
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("Code verified");
        session.Complete();
        return result;
    }

    private static void AssertGap(RecordedAction action) =>
        Assert.Equal(
            ("tool", "type (run-time value)", DerivedReason.MintedToken),
            (action.Kind, action.Text, action.Derived)
        );

    [Theory]
    [InlineData("one digit per box", "/one-time-code-plain", "digits")]
    [InlineData("three and three, with Tab and the focused box", "/one-time-code-plain?parts=2", "halves")]
    public async Task Records_a_split_code_as_one_gap_from_the_first_piece_and_never_types_the_last_runs_code(
        string split,
        string path,
        string how
    )
    {
        Assert.NotEmpty(split);
        Func<StepExecutorContext, string, string, CancellationToken, Task> type =
            how == "digits" ? DigitsAsync : HalvesAsync;
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(directory, site.Url, path, Executor(how, new CodeRecord(), type));

        // No piece is a word of "Your code is ...": the pieces typed in a row join into it.
        AssertGap(Assert.Single(TwoTapCounter.OnlyEntry(directory).Actions));

        var second = new CodeRecord();
        var result = await RunAsync(directory, site.Url, path, Executor(how, second, type));

        Assert.Equal(1, second.Calls);
        Assert.False(second.SawIncorrect);
        Assert.Equal(
            ("missed", "gap", DerivedReason.MintedToken, 1),
            (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.Derived, result.Cache?.TotalActions)
        );
    }

    [Fact]
    public async Task Cuts_focused_digits_typed_between_taps_at_the_first_digit_after_the_tap_that_focused_it()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(
            directory,
            site.Url,
            "/one-time-code-plain",
            Executor("focused", new CodeRecord(), FocusedAsync)
        );

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.Equal(2, actions.Count);
        Assert.Equal("tap", actions[0].Kind);
        AssertGap(actions[1]);

        var second = new CodeRecord();
        var result = await RunAsync(
            directory,
            site.Url,
            "/one-time-code-plain",
            Executor("focused", second, FocusedAsync)
        );

        Assert.Equal(1, second.Calls);
        Assert.False(second.SawIncorrect);
        Assert.Equal(
            ("agent-concluded", "gap", DerivedReason.MintedToken, 1),
            (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.Derived, result.Cache?.ReplayedActions)
        );
    }

    [Fact]
    public async Task Records_the_whole_code_as_one_gap_when_the_page_also_shows_one_of_its_digits_as_a_word()
    {
        // The "6" of 482613 is a word of "Enter the 6-digit code", so it is a gap on its own;
        // the whole code still joins, and nothing before that piece replays.
        const string path = "/one-time-code-plain?label=1&code=482613";
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(directory, site.Url, path, Executor("digits", new CodeRecord(), DigitsAsync));

        AssertGap(Assert.Single(TwoTapCounter.OnlyEntry(directory).Actions));

        var second = new CodeRecord();
        var result = await RunAsync(directory, site.Url, path, Executor("digits", second, DigitsAsync));

        Assert.Equal(1, second.Calls);
        Assert.Equal(
            ("missed", "gap", DerivedReason.MintedToken, 1),
            (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.Derived, result.Cache?.TotalActions)
        );
    }

    [Fact]
    public async Task Keeps_pieces_replayable_when_an_action_other_than_a_tap_or_Tab_sits_between_them()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(
            directory,
            site.Url,
            "/one-time-code-plain?parts=2&code=482913",
            Executor("enter-between", new CodeRecord(), EnterBetweenAsync)
        );

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.Equal(
            ["fill 482", "pressKey", "fill 913"],
            actions.Select(action => action.Value is { } value ? $"{action.Kind} {value}" : action.Kind)
        );
    }

    [Fact]
    public async Task Replays_a_code_the_instruction_spells_out_piece_by_piece()
    {
        const string path = "/one-time-code-plain?code=482913";
        const string spelled = "type the code 482913 into the code boxes";
        var directory = TwoTapCounter.NewDirectory();
        using var site = await OneTimeCodePage.StartAsync();
        await RunAsync(directory, site.Url, path, Executor("digits", new CodeRecord(), DigitsAsync), spelled);

        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.Equal(["4", "8", "2", "9", "1", "3"], actions.Select(action => action.Value ?? action.Kind));

        var second = new CodeRecord();
        var result = await RunAsync(directory, site.Url, path, Executor("digits", second, DigitsAsync), spelled);

        Assert.Equal(0, second.Calls);
        Assert.Equal(
            ("self-finalized", 6, 6),
            (result.Cache?.Mode, result.Cache?.ReplayedActions, result.Cache?.TotalActions)
        );
    }
}
