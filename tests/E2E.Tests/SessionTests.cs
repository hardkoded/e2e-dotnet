// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task Complete_records_a_verified_act_for_the_next_session()
    {
        var directory = TempCache();
        var firstCalls = 0;
        await using (var session = await StartAsync(directory, () => firstCalls++, attempt: 1))
        {
            await UpgradeAsync(session);
            session.Complete();
            Assert.True(firstCalls > 0);
        }

        var secondCalls = 0;
        await using (var session = await StartAsync(directory, () => secondCalls++, attempt: 1))
        {
            await UpgradeAsync(session);
            session.Complete();
            Assert.Equal(0, secondCalls);
            Assert.Equal(1, session.Replayed);
        }
    }

    [Fact]
    public async Task A_failed_session_does_not_replay()
    {
        var directory = TempCache();
        await using (var session = await StartAsync(directory, () => { }, attempt: 1))
        {
            await session.App.OpenAsync("/settings/billing");
            await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            session.Complete(new TestException("TEST_FAILED", "stopped before the check"));
        }

        var calls = 0;
        await using (var session = await StartAsync(directory, () => calls++, attempt: 1))
        {
            await UpgradeAsync(session);
            session.Complete();
            Assert.True(calls > 0);
            Assert.Equal(0, session.Replayed);
        }
    }

    [Fact]
    public async Task A_later_attempt_does_not_replay()
    {
        var directory = TempCache();
        await using (var session = await StartAsync(directory, () => { }, attempt: 1))
        {
            await UpgradeAsync(session);
            session.Complete();
        }

        var calls = 0;
        await using (var session = await StartAsync(directory, () => calls++, attempt: 2))
        {
            await UpgradeAsync(session);
            session.Complete();
            Assert.True(calls > 0);
            Assert.Equal(0, session.Replayed);
        }
    }

    [Fact]
    public async Task A_retry_records_without_replaying()
    {
        var directory = TempCache();
        await using (var session = await StartAsync(directory, () => { }, attempt: 2))
        {
            await session.App.OpenAsync("/settings/billing");
            var result = await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
            session.Complete();
            Assert.Equal("missed", result.Cache?.Mode);
            Assert.Equal("retry", result.Cache?.Reason);
        }

        var calls = 0;
        await using (var session = await StartAsync(directory, () => calls++, attempt: 1))
        {
            await UpgradeAsync(session);
            session.Complete();
            Assert.Equal(0, calls);
            Assert.Equal(1, session.Replayed);
        }
    }

    [Fact]
    public async Task A_skip_evicts_an_unverified_replay()
    {
        var directory = TempCache();
        await RecordAsync(directory);
        await using (var session = await StartAsync(directory, () => { }, attempt: 1))
        {
            await session.App.OpenAsync("/settings/billing");
            await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            Assert.Equal(1, session.Replayed);
            session.Complete(new SkipException("not today"));
        }

        Assert.Empty(Entries(directory));
    }

    [Fact]
    public async Task A_failure_keeps_an_entry_the_act_never_replayed()
    {
        var directory = TempCache();
        await RecordAsync(directory);
        var model = new ScriptedModel(_ => ModelResponses.Done("failed", "No upgrade button here."));
        await using (var session = await StartAsync(directory, model, attempt: 1))
        {
            await session.App.OpenAsync("/");
            var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("upgrade the workspace to the Pro plan"));
            Assert.Equal(1, session.Missed);
            session.Complete(error);
        }

        Assert.Single(Entries(directory));
    }

    [Fact]
    public async Task A_recording_that_opens_with_navigate_replays_from_another_route()
    {
        var directory = TempCache();
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
            await using var session = await StartAsync(directory, model, attempt: 1);
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
    public async Task An_unchanged_flow_does_not_rewrite_the_entry()
    {
        var directory = TempCache();
        await RecordAsync(directory);
        var file = Assert.Single(Entries(directory));
        var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);

        await RecordAsync(directory);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));

        // A retry runs live and records the same flow again.
        await using (var session = await StartAsync(directory, () => { }, attempt: 2))
        {
            await UpgradeAsync(session);
            session.Complete();
        }

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public void Defaults_match_upstream()
    {
        var options = new E2ESessionOptions { Engine = new DocumentEngine(new DocumentWorld()), TestTitle = "t" };
        Assert.Equal(TimeSpan.FromSeconds(120), options.TestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ActionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.AssertionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), options.ReplayTimeout);
        Assert.Equal(25, options.MaxModelCalls);
    }

    private static async Task RecordAsync(string directory)
    {
        await using var session = await StartAsync(directory, () => { }, attempt: 1);
        await UpgradeAsync(session);
        session.Complete();
    }

    private static string[] Entries(string directory)
    {
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : [];
    }

    private static async Task UpgradeAsync(E2ESession session)
    {
        await session.App.OpenAsync("/settings/billing");
        await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
    }

    private static Task<E2ESession> StartAsync(string directory, Action onAct, int attempt)
    {
        return StartAsync(
            directory,
            new ScriptedModel(request =>
            {
                var text = string.Join('\n', request.Messages.Select(message => message.Content));
                if (text.Contains("Statement:", StringComparison.Ordinal))
                {
                    return text.Contains("Prorated", StringComparison.Ordinal)
                        ? ModelResponses.Done("passed", "The invoice is prorated.")
                        : ModelResponses.Done("failed", "No prorated amount.", "ASSERTION_FAILED");
                }

                onAct();
                if (text.Contains("tapped", StringComparison.Ordinal))
                {
                    return ModelResponses.Done("passed", "Upgraded to Pro.");
                }

                return ModelResponses.Tap("button", "Upgrade to Pro");
            }),
            attempt);
    }

    private static Task<E2ESession> StartAsync(string directory, IAgentModel model, int attempt)
    {
        var world = new DocumentWorld()
            .Map("/", page => page.Heading("Home"))
            .Map("/home", page => page.Heading("Welcome"))
            .Map("/settings/billing", page =>
            {
                page.Heading("Billing");
                var status = page.Status("Pro", hidden: true);
                var invoice = page.Paragraph("Invoice preview");
                page.Button("Upgrade to Pro", () =>
                {
                    status.Hidden = false;
                    invoice.Text = "Prorated amount: $12";
                });
            });
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://billing.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            TestTitle = "billing > upgrades",
            Attempt = attempt,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        });
    }

    private static string TempCache() => Path.Combine(Path.GetTempPath(), "e2e-session", Guid.NewGuid().ToString("n"));
}
