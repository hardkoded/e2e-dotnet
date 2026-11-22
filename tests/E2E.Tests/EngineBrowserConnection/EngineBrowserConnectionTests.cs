// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.EngineBrowserConnection;

/// <summary>.NET-only (no upstream <c>it</c>): the engine owns one driver and one connection for all its attempts, and its dispose releases them (upstream's surface <c>dispose</c>).</summary>
public sealed class EngineBrowserConnectionTests
{
    [Fact]
    public async Task Shares_one_driver_across_attempts_and_stops_it_when_the_engine_is_disposed()
    {
        var created = 0;
        var disposed = 0;
        var engine = new WebEngine(new WebEngineOptions())
        {
            CreatePlaywright = () =>
            {
                Interlocked.Increment(ref created);
                var (playwright, fake) = Fake<IPlaywright>.New();
                fake.Members["Dispose"] = _ =>
                {
                    Interlocked.Increment(ref disposed);
                    return null;
                };
                var (selectors, selectorsFake) = Fake<ISelectors>.New();
                selectorsFake.Members["RegisterAsync"] = _ => Task.CompletedTask;
                fake.Members["get_Selectors"] = _ => selectors;
                return Task.FromResult(playwright);
            },
            EnsureBrowser = (_, _, _) => Task.CompletedTask,
            LaunchLocal = (_, _) => throw new InvalidOperationException("no browser here"),
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.StartAsync(new EngineStartOptions(), CancellationToken.None)
            );
        }

        // A failed start leaves the driver with the engine, not closed per attempt.
        Assert.Equal(1, created);
        Assert.Equal(0, disposed);

        await ((IDisposableEngine)engine).DisposeAsync(
            new EngineDisposeContext { CleanupTimeout = TimeSpan.FromSeconds(5) },
            CancellationToken.None
        );
        Assert.Equal(1, disposed);

        // The engine starts again after a dispose, with a driver of its own.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.StartAsync(new EngineStartOptions(), CancellationToken.None)
        );
        Assert.Equal(2, created);
        await engine.DisposeAsync();
        Assert.Equal(2, disposed);
    }
}
