// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TraceIdentity;

/// <summary>
/// .NET-only: upstream keys on <c>target.engine.name</c> in <c>cache/identity.ts</c>, which its tests reach only through whole runs.
/// This runs a session over an engine whose name differs from its platform, as <c>MobileEngine</c> does.
/// </summary>
public sealed class TraceCacheKeyEngineNameTests
{
    private sealed class NamedEngine(IEngine inner) : IEngine
    {
        public string Name => "mobile";

        public string Platform => "ios";

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => inner.StartAsync(options, cancellationToken);
    }

    [Fact]
    public async Task Keys_a_recording_on_the_engine_name_and_not_its_platform()
    {
        var directory = ConfigTests.TempCache();
        var world = new DocumentWorld().Map("/", page =>
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
        await using (var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new NamedEngine(new DocumentEngine(world)),
            Model = BillingSession.UpgradeModel(() => { }),
            BaseUrl = "https://app.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            TestTitle = "keys > engine",
            Attempt = 1,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        }))
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
            session.Complete();
        }

        var keyedBy = new FileStepCache(directory).Read(Path.GetFileNameWithoutExtension(Directory.GetFiles(directory).Single())).Entry!.KeyedBy!;
        Assert.Equal("mobile", keyedBy.EngineName);
        Assert.Equal("ios", keyedBy.Platform);
    }
}
