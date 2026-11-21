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
        await BrowserConnection.LaunchLocalAsync(playwright, Options(true));
        var launch = Assert.Single(launches);
        Assert.True(launch.Headless);
        Assert.True(launch.ChromiumSandbox);
    }

    [Fact]
    public async Task Launches_chromium_with_the_sandbox_off_when_asked()
    {
        var (playwright, launches) = Launcher(BrowserName.Chromium, null);
        await BrowserConnection.LaunchLocalAsync(playwright, Options(false));
        Assert.False(Assert.Single(launches).ChromiumSandbox);
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
        var error = await Assert.ThrowsAsync<EngineException>(() =>
            BrowserConnection.LaunchLocalAsync(playwright, Options(true))
        );
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
        var thrown = await Assert.ThrowsAsync<PlaywrightException>(() =>
            BrowserConnection.LaunchLocalAsync(playwright, Options(true))
        );
        Assert.Same(failure, thrown);
    }

    [Theory]
    [InlineData(BrowserName.Firefox)]
    [InlineData(BrowserName.Webkit)]
    public async Task Does_not_pass_the_chromium_only_option_to_firefox_or_webkit(BrowserName name)
    {
        var (playwright, launches) = Launcher(name, null);
        await BrowserConnection.LaunchLocalAsync(playwright, Options(true) with { Name = name });
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
        return (playwright, launches);
    }
}
