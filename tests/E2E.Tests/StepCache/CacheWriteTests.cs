// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.StepCache;

/// <summary>
/// What became of each step's recording once the attempt settled (upstream's <c>settleStagedTraces</c> outcomes). Upstream has no
/// test of the outcomes by name, they show on the report; the port shows them on the trace page, so these tests read the step
/// records. .NET-only tests.
/// </summary>
public sealed class CacheWriteTests
{
    private static CacheWrite? WriteOf(E2ESession session) => Assert.Single(session.Steps, step => step.Api == "agent.act").CacheWrite;

    [Fact]
    public async Task Saves_a_recording_a_later_check_confirmed_and_keeps_it_when_the_next_run_replays_it()
    {
        var directory = BillingSession.TempCache();
        await using (var recording = await BillingSession.StartAsync(directory, () => { }, attempt: 1))
        {
            await BillingSession.UpgradeAsync(recording);
            recording.Complete();
            Assert.Equal(CacheWrite.Saved, WriteOf(recording));
        }

        await using var replaying = await BillingSession.StartAsync(directory, () => { }, attempt: 1);
        await BillingSession.UpgradeAsync(replaying);
        replaying.Complete();

        Assert.Equal(CacheWrite.Kept, WriteOf(replaying));
    }

    [Fact]
    public async Task Does_not_save_a_recording_no_check_confirmed()
    {
        var directory = BillingSession.TempCache();
        await using var session = await BillingSession.StartAsync(directory, () => { }, attempt: 1);
        await session.App.OpenAsync("/settings/billing");
        await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        session.Complete();

        Assert.Equal(CacheWrite.Unconfirmed, WriteOf(session));
        Assert.Equal(0, BillingSession.CachedEntries(directory));
    }

    [Fact]
    public async Task Evicts_an_entry_a_failure_implicated()
    {
        var directory = BillingSession.TempCache();
        await BillingSession.RecordAsync(directory);

        await using var session = await BillingSession.StartAsync(directory, () => { }, attempt: 1);
        await session.App.OpenAsync("/settings/billing");
        await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.That(session.Screen.GetByRole("status", "Enterprise")).ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(100)));
        session.Complete(error);

        Assert.Equal(CacheWrite.Evicted, WriteOf(session));
        Assert.Empty(BillingSession.Entries(directory));
    }

    [Fact]
    public async Task Tells_a_step_that_changed_nothing_a_replay_could_check()
    {
        var world = new DocumentWorld().Map("/", page => page.Button("Nothing", () => { }));
        var executor = new TestExecutor("taps-nothing", async (context, token) =>
        {
            var observation = await context.ObserveAsync();
            await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Nothing\""), token);
            return Passed("tapped");
        })
        {
            Version = "1",
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            AgentExecutor = executor,
            BaseUrl = "https://nothing.test",
            Cache = new FileStepCache(BillingSession.TempCache()),
            CacheEnabled = true,
            TestTitle = "changes nothing",
        });
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("tap the button that does nothing");
        session.Complete();

        Assert.Equal(CacheWrite.NoChange, WriteOf(session));
    }

    [Fact]
    public async Task Tells_a_recording_the_store_did_not_write()
    {
        var store = new FailingWrites();
        await using var session = await BillingSession.StartAsync(store, attempt: 1);
        await BillingSession.UpgradeAsync(session);
        session.Complete();

        Assert.Equal(CacheWrite.NotWritten, WriteOf(session));
    }

    private sealed class FailingWrites : IStepCache
    {
        public CacheLookup Read(string key) => new() { Reason = "no-entry" };

        public void Write(string key, CacheEntry entry) => throw new IOException("the store is full");

        public void Delete(string key)
        {
        }
    }
}
