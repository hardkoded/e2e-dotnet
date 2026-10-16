// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TraceCache;

/// <summary>
/// Upstream tests <c>decideTraceReplay</c> directly (<see cref="DecideTraceReplayTests"/>, <c>TraceDecide.DecideTraceReplayTests</c>).
/// These run the same decision through the act over a stored recording, so each is a port test of its own, not an upstream one.
/// A page always has a route in the port, so the undefined current path is not checked.
/// </summary>
public sealed class DecideTraceReplayThroughTheActTests
{
    [Fact]
    public async Task The_act_replays_a_navigate_opening_recording_from_anywhere()
    {
        var directory = BillingSession.TempCache();
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
            await using var session = await BillingSession.StartAsync(directory, model, attempt: 1);
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

    [Fact]
    public async Task The_act_misses_on_a_wrong_context_when_the_recording_does_not_open_with_navigate()
    {
        var entry = Entry("/settings", Tap());
        Assert.NotEqual("missed", (await ActAt("/settings", entry))?.Mode);
        Assert.NotEqual("missed", (await ActAt("/orders/43917#top", Entry("/orders/42", Tap())))?.Mode);
        var miss = await ActAt("/other", entry);
        Assert.Equal(("missed", "wrong-context"), (miss?.Mode, miss?.Reason));
    }

    [Fact]
    public async Task The_act_misses_on_a_wrong_context_when_a_non_navigating_recording_has_no_start_route()
    {
        var miss = await ActAt("/settings", Entry(null, Tap()));
        Assert.Equal(("missed", "wrong-context"), (miss?.Mode, miss?.Reason));
    }

    private static RecordedAction Tap() => new() { Kind = "tap", Role = "button", Name = "Upgrade" };

    private static CacheEntry Entry(string? route, params RecordedAction[] actions) =>
        new() { Instruction = "upgrade the plan", Route = route, Actions = [.. actions] };

    /// <summary>Opens <paramref name="route"/>, acts with <paramref name="entry"/> as the stored recording, and returns how the cache served the step.</summary>
    private static async Task<CacheInfo?> ActAt(string route, CacheEntry entry)
    {
        var world = new DocumentWorld();
        foreach (var path in new[] { "/settings", "/other", "/orders/42", "/orders/43917" })
        {
            world.Map(path, page => page.Button("Upgrade"));
        }

        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "upgraded")),
            BaseUrl = "https://billing.test",
            Cache = new StoredEntry(entry),
            CacheEnabled = true,
            TestTitle = "billing > upgrades",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        });
        await session.App.OpenAsync(route);
        var result = await session.Agent.ActAsync("upgrade the plan");
        session.Complete();
        return result.Cache;
    }

    /// <summary>A store that holds one recording under every key.</summary>
    private sealed class StoredEntry(CacheEntry entry) : IStepCache
    {
        public CacheLookup Read(string key) => new() { Entry = entry };

        public void Write(string key, CacheEntry entry)
        {
        }

        public void Delete(string key)
        {
        }
    }
}
