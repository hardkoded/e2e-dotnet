// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream's <c>describe('trace cache: a composed word that appears on screen is not a run-time value')</c>, run in two sessions over one
/// cache directory through the real web engine. Upstream's recorded action name <c>type</c> is the port's <c>fill</c> kind, and
/// <c>endAnchors</c> is the entry's <c>Appeared</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheAComposedWordThatAppearsOnScreenIsNotARunTimeValueTests
{
    // Types "one" and "two" into the two unnamed textboxes: words the model composed, which happen to appear inside the row labels beside
    // the fields. No model.
    private static TestExecutor RowWordsExecutor(Action onRun) =>
        new("row-words-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(cancellationToken: token);
            var fields = Regex.Matches(observation.Text, @"^\s*#(?<id>\S+) textbox\b", RegexOptions.Multiline).Select(match => match.Groups["id"].Value).ToList();
            Assert.Equal(2, fields.Count);
            await context.Actions.TypeAsync(new ExecutorTarget(fields[0]), "one", token);
            await context.Actions.TypeAsync(new ExecutorTarget(fields[1]), "two", token);
            return Passed("filled both fields");
        })
        {
            Version = "test",
        };

    private static async Task<ActResult> RunAsync(string directory, string appUrl, Action onRun)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = RowWordsExecutor(onRun),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "fills both row fields",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/row-fields");
        var result = await session.Agent.ActAsync("fill the two fields with short words of your choice");
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("one+two");
        session.Complete();
        return result;
    }

    [Fact]
    public async Task Records_both_fills_verbatim_and_replays_the_whole_step_on_the_second_run()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var pages = await TraceCacheStatePages.StartAsync();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(1, runs);
        var entry = TwoTapCounter.OnlyEntry(directory);

        // "one" sits inside "Row one" on screen, and is still the model's own word: neither fill is recorded as a run-time value gap.
        Assert.Equal(["fill", "fill"], entry.Actions.Select(action => action.Kind));
        Assert.Equal(["one", "two"], entry.Actions.Select(action => action.Value));
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "status", Name: "Filled", Text: "one+two" });

        runs = 0;
        var second = await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(0, runs);
        Assert.Equal("self-finalized", second.Cache?.Mode);
        Assert.Equal(2, second.Cache?.ReplayedActions);
        Assert.Equal(2, second.Cache?.TotalActions);
    }
}
