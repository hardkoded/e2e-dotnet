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
    public async Task A_teardown_check_after_a_failure_does_not_verify_acts()
    {
        var directory = TempCache();
        var failed = false;
        await using (var session = await StartAsync(directory, () => { }, attempt: 1, () => failed))
        {
            await session.App.OpenAsync("/settings/billing");
            await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            failed = true;
            await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
            await Expect.That(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync();
            session.Complete(new TestException("TEST_FAILED", "the body failed before teardown"));
        }

        Assert.Equal(0, CachedEntries(directory));
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
    public async Task A_replay_repaired_after_an_end_mismatch_evicts_the_entry()
    {
        var directory = TempCache();
        var confirm = false;
        var world = new DocumentWorld().Map("/settings/billing", page =>
        {
            page.Heading("Billing");
            var status = page.Status("Pro", hidden: true);
            var confirmButton = page.Button("Confirm upgrade", () => status.Hidden = false);
            confirmButton.Hidden = true;
            page.Button("Upgrade to Pro", () =>
            {
                if (confirm)
                {
                    confirmButton.Hidden = false;
                }
                else
                {
                    status.Hidden = false;
                }
            });
        });
        var actCalls = 0;
        var model = new ScriptedModel(request =>
        {
            actCalls++;
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "Upgraded to Pro.");
            }

            return ModelResponses.Tap("button", text.Contains("end-mismatch", StringComparison.Ordinal) ? "Confirm upgrade" : "Upgrade to Pro");
        });

        async Task<ActResult> UpgradeAndCheckAsync(E2ESession session)
        {
            await session.App.OpenAsync("/settings/billing");
            var result = await session.Agent.ActAsync("upgrade");
            await Expect.That(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync();
            session.Complete();
            return result;
        }

        await using (var session = await StartWorldAsync(world, model, directory))
        {
            await UpgradeAndCheckAsync(session);
        }

        Assert.Equal(1, CachedEntries(directory));
        confirm = true;
        actCalls = 0;
        await using (var session = await StartWorldAsync(world, model, directory))
        {
            var result = await UpgradeAndCheckAsync(session);
            Assert.Equal("agent-concluded", result.Cache?.Mode);
            Assert.Equal("end-mismatch", result.Cache?.Reason);
            Assert.True(actCalls > 0);
        }

        Assert.Equal(0, CachedEntries(directory));
    }

    private static async Task UpgradeAsync(E2ESession session)
    {
        await session.App.OpenAsync("/settings/billing");
        await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
    }

    private static Task<E2ESession> StartAsync(string directory, Action onAct, int attempt, Func<bool>? testFailed = null)
    {
        var world = new DocumentWorld().Map("/settings/billing", page =>
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
            Model = new ScriptedModel(request =>
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
            BaseUrl = "https://billing.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            TestTitle = "billing > upgrades",
            Attempt = attempt,
            TestFailed = testFailed,
        });
    }

    private static Task<E2ESession> StartWorldAsync(DocumentWorld world, IAgentModel model, string directory)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://billing.test",
            Cache = new FileStepCache(directory),
            TestTitle = "billing > confirms",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            AssertionTimeout = TimeSpan.FromSeconds(2),
        });
    }

    private static int CachedEntries(string directory) => Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length : 0;

    private static string TempCache() => Path.Combine(Path.GetTempPath(), "e2e-session", Guid.NewGuid().ToString("n"));
}
