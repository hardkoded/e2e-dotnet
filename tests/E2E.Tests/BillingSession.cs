// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests;

/// <summary>A billing app session over a file cache, as the trace cache ports drive it.</summary>
internal static class BillingSession
{
    internal static async Task RecordAsync(string directory)
    {
        await using var session = await StartAsync(directory, () => { }, attempt: 1);
        await UpgradeAsync(session);
        session.Complete();
    }

    internal static string[] Entries(string directory)
    {
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : [];
    }

    internal static async Task UpgradeAsync(E2ESession session)
    {
        await session.App.OpenAsync("/settings/billing");
        await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
    }

    internal static Task<E2ESession> StartAsync(string directory, Action onAct, int attempt, Func<bool>? testFailed = null) =>
        StartAsync(directory, UpgradeModel(onAct), attempt, testFailed);

    /// <summary>A model that taps Upgrade to Pro, concludes, and judges the invoice by its prorated amount. <paramref name="onAct"/> runs on each turn of the act.</summary>
    internal static ScriptedModel UpgradeModel(Action onAct)
    {
        return new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("Statement:", StringComparison.Ordinal))
            {
                return text.Contains("Prorated", StringComparison.Ordinal)
                    ? ModelResponses.Done("passed", "The invoice is prorated.")
                    : ModelResponses.Done("failed", "No prorated amount.", "ASSERTION_FAILED");
            }

            onAct();
            if (text.Contains("Tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "Upgraded to Pro.");
            }

            return ModelResponses.Tap("button", "Upgrade to Pro");
        });
    }

    internal static Task<E2ESession> StartAsync(string directory, IAgentModel model, int attempt, Func<bool>? testFailed = null) =>
        StartAsync(new FileStepCache(directory), model, attempt, testFailed);

    internal static Task<E2ESession> StartAsync(IStepCache cache, int attempt) => StartAsync(cache, UpgradeModel(() => { }), attempt);

    private static Task<E2ESession> StartAsync(IStepCache cache, IAgentModel model, int attempt, Func<bool>? testFailed = null)
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
            Cache = cache,
            CacheEnabled = true,
            TestTitle = "billing > upgrades",
            Attempt = attempt,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
            TestFailed = testFailed,
        });
    }

    internal static int CachedEntries(string directory) => Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length : 0;

    internal static string TempCache() => Path.Combine(Path.GetTempPath(), "e2e-session", Guid.NewGuid().ToString("n"));
}
