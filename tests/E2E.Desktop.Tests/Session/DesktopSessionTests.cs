// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Session;

/// <summary>.NET-only: <see cref="DesktopSession"/> is the <see cref="IEngineSession"/> over a <c>DesktopSurface</c>, where upstream's engine hands the harness the surface's methods.</summary>
public sealed class DesktopSessionTests : SurfaceTestBase
{
    private async Task<(DesktopEngine Engine, IEngineSession Session, FakeDriver Fake)> Started(
        Scenario? scenario = null
    )
    {
        var fake = NewFake(scenario);
        var engine = new DesktopEngine(new DesktopOptions { Settle = false }, () => fake)
        {
            Host = new DesktopHost(true, "macOS"),
        };
        var session = await engine.StartAsync(
            new EngineStartOptions
            {
                BundleId = "dev.e2e.spike.form",
                ArtifactsDir = Artifacts(),
                ProjectRoot = Directory.GetCurrentDirectory(),
            },
            CancellationToken.None
        );
        return (engine, session, fake);
    }

    [Fact]
    public async Task Opens_the_app_observes_its_window_and_acts_on_a_node_it_found()
    {
        var (engine, session, fake) = await Started();
        await using var _ = session;
        Assert.Single(engine.ActiveSessions);
        await session.OpenAsync("", CancellationToken.None);
        var observation = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal("SpikeForm › Spike Form", observation.Route);
        Assert.Equal(observation.Route, session.Route);
        var signIn = observation.Roots[0].Children.First(node => node.TestId == "sign-in");
        await session.PerformAsync(signIn, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None);
        Assert.Equal(["click"], fake.Acted());
        var located = await session.LocateAsync(TestId("sign-in"), TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Single(located);
    }

    [Fact]
    public async Task Swipes_the_window_through_the_root_and_presses_a_key_into_it()
    {
        var scrollable = Screen(
            "w-mine",
            new
            {
                name = "List",
                bounds = new
                {
                    x = 0,
                    y = 0,
                    width = 100,
                    height = 100,
                },
                children = new object[]
                {
                    new
                    {
                        role = "scrollarea",
                        ref_id = "@s9:e1",
                        available_actions = new[] { "Scroll" },
                    },
                },
            }
        );
        var (_, session, fake) = await Started(new Scenario { ["snapshot"] = [scrollable] });
        await using var _ = session;
        await session.OpenAsync("", CancellationToken.None);
        await session.SwipeAsync(ScrollDirection.Down, null, OperationContext.Test, CancellationToken.None);
        Assert.Equal("scroll", Last<ActRequest>(fake, "act")!.Action);
        await session.PressAsync("Enter", OperationContext.Test, CancellationToken.None);
        Assert.Equal("return", Last<PressRequest>(fake, "press")!.Combo);
    }

    [Fact]
    public async Task Quits_the_app_and_forgets_the_session_when_it_is_disposed()
    {
        var (engine, session, fake) = await Started();
        await session.OpenAsync("", CancellationToken.None);
        var pid = fake.Pid;
        await session.DisposeAsync();
        Assert.True(await Gone(pid));
        Assert.Empty(engine.ActiveSessions);
    }

    [Fact]
    public async Task Gives_the_desktop_fixture_of_the_session_and_builds_fixtures_only_for_its_own_sessions()
    {
        var (engine, session, _) = await Started();
        await using var _ = session;
        var fixtures = engine.CreateFixtures(session, null!);
        Assert.Equal("macos", fixtures.Desktop.Platform);
        var other = Assert.Throws<EngineException>(() => engine.CreateFixtures(new NotADesktopSession(), null!));
        Assert.Equal("WORKER_PROTOCOL", other.Code);
    }

    private sealed class NotADesktopSession : IEngineSession
    {
        public string Route => "";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task PressAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
