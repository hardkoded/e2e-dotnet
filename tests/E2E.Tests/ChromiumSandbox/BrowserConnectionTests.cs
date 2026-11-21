// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.ChromiumSandbox;

/// <summary>A Chromium launch takes the sandbox value it is asked for, and a sandbox that cannot start names the option.</summary>
public sealed class BrowserConnectionTests
{
    [Fact]
    public async Task Launches_chromium_with_the_sandbox_on_when_asked()
    {
        var (playwright, launches) = Launcher(BrowserName.Chromium, null);
        var connection = new BrowserConnection(playwright);
        await connection.AcquireAsync(Options(true));
        await connection.DisposeAsync();
        var launch = Assert.Single(launches);
        Assert.True(launch.Headless);
        Assert.True(launch.ChromiumSandbox);
    }

    [Fact]
    public async Task Launches_chromium_with_the_sandbox_off_when_asked()
    {
        var (playwright, launches) = Launcher(BrowserName.Chromium, null);
        var connection = new BrowserConnection(playwright);
        await connection.AcquireAsync(Options(false));
        await connection.DisposeAsync();
        Assert.False(Assert.Single(launches).ChromiumSandbox);
    }

    [Fact]
    public async Task Keeps_the_sandbox_on_when_a_waiter_joins_a_relaunch_that_is_still_in_flight()
    {
        var connected = true;
        var (first, firstFake) = Fake<IBrowser>.New();
        firstFake.Members["get_IsConnected"] = _ => connected;
        firstFake.Members["CloseAsync"] = _ => Task.CompletedTask;
        var (third, thirdFake) = Fake<IBrowser>.New();
        thirdFake.Members["get_IsConnected"] = _ => true;
        thirdFake.Members["CloseAsync"] = _ => Task.CompletedTask;
        var launches = new List<BrowserTypeLaunchOptions>();
        var (type, typeFake) = Fake<IBrowserType>.New();
        typeFake.Members["LaunchAsync"] = args =>
        {
            int call;
            lock (launches)
            {
                launches.Add((BrowserTypeLaunchOptions)args[0]!);
                call = launches.Count;
            }

            return call switch
            {
                1 => Task.FromResult(first),
                2 => Task.Delay(20)
                    .ContinueWith<IBrowser>(
                        _ => throw new PlaywrightException("browserType.launch: Chromium sandboxing failed!"),
                        TaskScheduler.Default
                    ),
                _ => Task.FromResult(third),
            };
        };
        var (playwright, fake) = Fake<IPlaywright>.New();
        fake.Members["get_Chromium"] = _ => type;
        fake.Members["get_Selectors"] = _ => Selectors();
        var connection = new BrowserConnection(playwright);
        await connection.AcquireAsync(Options(true));
        connected = false;

        // The relaunch is still in flight when the waiter joins it, so the waiter goes through the re-acquire once:
        // a flip there cannot cancel out against a second one.
        var relaunch = connection.AcquireAsync(Options(true));
        await Task.Delay(1);
        var waiter = connection.AcquireAsync(Options(true));
        await Assert.ThrowsAsync<EngineException>(() => relaunch);
        Assert.Same(third, await waiter);
        Assert.Equal(3, launches.Count);
        Assert.All(launches, launch => Assert.True(launch.ChromiumSandbox));
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Turns_a_sandbox_launch_failure_into_an_error_naming_the_option()
    {
        var (playwright, _) = Launcher(
            BrowserName.Chromium,
            new PlaywrightException(
                "browserType.launch: Target page, context or browser has been closed\n"
                    + "Chromium sandboxing failed!\n"
                    + "Call log:\n"
                    + "  - launching Chromium with --no-sandbox --flag-a --flag-b"
            )
        );
        var connection = new BrowserConnection(playwright);
        var error = await Assert.ThrowsAsync<EngineException>(() => connection.AcquireAsync(Options(true)));
        await connection.DisposeAsync();
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.False(error.Retryable);
        Assert.Contains("ChromiumSandbox", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Call log", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--flag-a", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Passes_any_other_launch_failure_through_unchanged()
    {
        var failure = new PlaywrightException("browserType.launch: executable does not exist");
        var (playwright, _) = Launcher(BrowserName.Chromium, failure);
        var connection = new BrowserConnection(playwright);
        var thrown = await Assert.ThrowsAsync<PlaywrightException>(() => connection.AcquireAsync(Options(true)));
        await connection.DisposeAsync();
        Assert.Same(failure, thrown);
    }

    [Theory]
    [InlineData(BrowserName.Firefox)]
    [InlineData(BrowserName.Webkit)]
    public async Task Does_not_pass_the_chromium_only_option_to_firefox_or_webkit(BrowserName name)
    {
        var (playwright, launches) = Launcher(name, null);
        var connection = new BrowserConnection(playwright);
        await connection.AcquireAsync(Options(true) with { Name = name });
        await connection.DisposeAsync();
        Assert.Null(Assert.Single(launches).ChromiumSandbox);
    }

    private static BrowserAcquireOptions Options(bool chromiumSandbox) =>
        new()
        {
            Name = BrowserName.Chromium,
            Headed = false,
            ChromiumSandbox = chromiumSandbox,
        };

    private static (IPlaywright Playwright, List<BrowserTypeLaunchOptions> Launches) Launcher(
        BrowserName name,
        Exception? failure
    )
    {
        var launches = new List<BrowserTypeLaunchOptions>();
        var (type, typeFake) = Fake<IBrowserType>.New();
        typeFake.Members["LaunchAsync"] = args =>
        {
            launches.Add((BrowserTypeLaunchOptions)args[0]!);
            if (failure is not null)
            {
                return Task.FromException<IBrowser>(failure);
            }

            var (browser, _) = Fake<IBrowser>.New();
            return Task.FromResult(browser);
        };
        var (playwright, fake) = Fake<IPlaywright>.New();
        var member = name switch
        {
            BrowserName.Firefox => "get_Firefox",
            BrowserName.Webkit => "get_Webkit",
            _ => "get_Chromium",
        };
        fake.Members[member] = _ => type;
        fake.Members["get_Selectors"] = _ => Selectors();
        return (playwright, launches);
    }

    private static ISelectors Selectors()
    {
        var (selectors, fake) = Fake<ISelectors>.New();
        fake.Members["RegisterAsync"] = _ => Task.CompletedTask;
        return selectors;
    }
}
