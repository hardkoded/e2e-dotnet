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
            DisposeOwned = true,
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

        // A host owns the dispose, so a failed start leaves the driver with the engine.
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

    [Fact]
    public async Task Stops_the_driver_when_the_start_of_an_engine_nobody_disposes_fails()
    {
        var disposed = 0;
        var engine = new WebEngine(new WebEngineOptions())
        {
            CreatePlaywright = () =>
            {
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.StartAsync(new EngineStartOptions(), CancellationToken.None)
        );

        // No host owns this engine's dispose, so the attempt closes what it started, as a start did before the engine owned the connection.
        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task Stops_the_driver_that_lands_after_the_disposal_budget_ran_out()
    {
        var disposed = 0;
        var landing = new TaskCompletionSource<IPlaywright>();
        var engine = new WebEngine(new WebEngineOptions())
        {
            DisposeOwned = true,
            CreatePlaywright = () => landing.Task,
            EnsureBrowser = (_, _, _) => Task.CompletedTask,
            LaunchLocal = (_, _) => throw new InvalidOperationException("no browser here"),
        };
        var starting = engine.StartAsync(new EngineStartOptions(), CancellationToken.None);
        using var spent = new CancellationTokenSource();
        await spent.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IDisposableEngine)engine).DisposeAsync(
                new EngineDisposeContext { CleanupTimeout = TimeSpan.Zero },
                spent.Token
            )
        );

        var (playwright, fake) = Fake<IPlaywright>.New();
        fake.Members["Dispose"] = _ =>
        {
            Interlocked.Increment(ref disposed);
            return null;
        };
        var (selectors, selectorsFake) = Fake<ISelectors>.New();
        selectorsFake.Members["RegisterAsync"] = _ => Task.CompletedTask;
        fake.Members["get_Selectors"] = _ => selectors;
        landing.SetResult(playwright);

        // The start that was waiting for the driver fails on its launch; the driver it received is stopped by the dispose that gave up.
        await Assert.ThrowsAsync<InvalidOperationException>(() => starting);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref disposed) == 1, TimeSpan.FromSeconds(5)));
    }
}
