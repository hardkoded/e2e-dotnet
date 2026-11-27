// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.Clock;

/// <summary>
/// Upstream's <c>tests/types/clock.ts</c> (tester-army/e2e#2256), the compile-time assertions for <c>browser.clock</c>: Playwright's arguments, a time as
/// a number, string, or date and ticks as a number or string. Upstream checks them with the type checker; here the accepted calls are compiled
/// and the refused ones are reflection checks of the public API.
/// </summary>
public sealed class ClockTests
{
    // Never called: it compiles only while every accepted argument below is accepted.
    private static void Accepted(Browser browser)
    {
        _ = browser.Clock.InstallAsync();
        _ = browser.Clock.InstallAsync(
            new BrowserClockInstallOptions { Time = new DateTimeOffset(2024, 2, 2, 8, 0, 0, TimeSpan.Zero) }
        );
        _ = browser.Clock.InstallAsync(new BrowserClockInstallOptions { Time = "2024-02-02T08:00:00" });
        _ = browser.Clock.PauseAtAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _ = browser.Clock.RunForAsync("01:30");
        _ = browser.Clock.FastForwardAsync(1000);
        _ = browser.Clock.SetFixedTimeAsync(DateTimeOffset.UtcNow);
        _ = browser.Clock.SetSystemTimeAsync("2024-02-02");
        _ = browser.Clock.ResumeAsync();
    }

    [Fact]
    public void Takes_playwrights_arguments_a_time_as_a_number_string_or_date_and_ticks_as_a_number_or_string()
    {
        Assert.NotNull(typeof(ClockTests).GetMethod(nameof(Accepted), BindingFlags.NonPublic | BindingFlags.Static));

        // install takes an options object, not the time itself.
        var install = typeof(BrowserClock).GetMethod(nameof(BrowserClock.InstallAsync))!;
        Assert.Equal(typeof(BrowserClockInstallOptions), install.GetParameters()[0].ParameterType);

        // ticks are a number or a string, not a date.
        var ticksFrom = typeof(ClockTicks)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit")
            .Select(method => method.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(typeof(DateTimeOffset), ticksFrom);
        Assert.DoesNotContain(typeof(DateTime), ticksFrom);

        // pauseAt needs the time to pause at.
        var pauseAt = typeof(BrowserClock).GetMethod(nameof(BrowserClock.PauseAtAsync))!;
        Assert.Equal(typeof(ClockTime), pauseAt.GetParameters()[0].ParameterType);
        Assert.False(pauseAt.GetParameters()[0].IsOptional);

        // there is no uninstall: the clock ends with the attempt.
        Assert.Null(typeof(BrowserClock).GetMethod("UninstallAsync"));
    }
}
