// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineContract;

/// <summary>
/// .NET-only: upstream's engine hands its fixture factories an <c>EngineFixtureContext</c> (<c>engine-contract.test.ts</c> has the
/// engine-contributed fixture tests, which the port does not run). The port's <see cref="IEngineFixtureHost"/> hands a session the
/// context, with the <c>locator</c> member only, once the attempt's screen exists.
/// </summary>
public sealed class EngineFixtureHostTests
{
    [Fact]
    public async Task Hands_a_session_that_hosts_a_fixture_a_context_that_mints_locators_on_the_attempts_screen()
    {
        var engine = new HostingEngine(new DocumentEngine(BillingWorld.Create()));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            BaseUrl = "https://billing.test",
            TestTitle = "fixture host",
        });
        await session.App.OpenAsync("/settings/billing");
        var context = Assert.IsType<EngineFixtureContext>(engine.Context);
        var locator = context.Locator(LocatorExpression.ForRole("heading", TextMatch.FromString("Billing"), new RoleOptions()));
        Assert.Equal(1, await locator.CountAsync());
        Assert.Throws<ArgumentNullException>(() => context.Locator(null!));
    }

    /// <summary>A document engine whose session hosts a fixture, and keeps the context it was given.</summary>
    private sealed class HostingEngine(IEngine inner) : IEngine
    {
        public EngineFixtureContext? Context { get; private set; }

        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(HostingEngine engine, IEngineSession session) : IEngineSession, IEngineFixtureHost
        {
            public string Route => session.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => session.ObserveAsync(cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => session.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

            public void CreateFixtures(EngineFixtureContext context) => engine.Context = context;

            public ValueTask DisposeAsync() => session.DisposeAsync();
        }
    }
}
