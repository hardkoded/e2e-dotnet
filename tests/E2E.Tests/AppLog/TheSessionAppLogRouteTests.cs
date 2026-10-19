// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AppLog;

/// <summary>
/// Upstream's <c>describe('the session app log route')</c> (<c>tests/unit/app-log.test.ts</c>). The tests build the port's
/// <see cref="AppLogRoute"/> directly: the port has no <c>createEngineSession</c> adapter.
/// </summary>
public sealed class TheSessionAppLogRouteTests
{
    private static readonly AppLogEntry Error = new(AppLogSource.Console, AppLogLevel.Error, "boom");

    // Partial: the engine hands the port a typed entry, so upstream's source that is no source (`stdout`, `navigation`) has no counterpart; only a value outside the enums, a blank text, or no entry is malformed.
    [Fact]
    public void Holds_lines_and_navigations_until_a_sink_is_routed_drops_undefined_and_blank_ones_and_sends_later_ones_straight_through()
    {
        var route = new AppLogRoute();
        route.Push(Error);
        route.Navigated("navigated to /login");
        route.Push(new AppLogEntry((AppLogSource)99, AppLogLevel.Error, "not a source"));
        route.Push(new AppLogEntry(AppLogSource.Console, (AppLogLevel)99, "not a level"));
        route.Push(new AppLogEntry(AppLogSource.Console, AppLogLevel.Error, "   "));
        route.Push(null);
        route.Navigated("  ");
        var heard = new List<string>();
        route.Route((appEvent, _) => heard.Add(appEvent is AppEvent.Log log ? log.Entry.Text : $"↪ {((AppEvent.Navigation)appEvent).Line}"));
        route.Push(new AppLogEntry(AppLogSource.Network, AppLogLevel.Warning, "late"));

        Assert.Equal(["boom", "↪ navigated to /login", "late"], heard);
    }

    [Fact]
    public void Holds_each_kind_within_its_own_quota_before_a_sink_is_routed_so_chatter_never_crowds_out_an_error()
    {
        var route = new AppLogRoute();
        for (var index = 0; index < 400; index++)
        {
            route.Push(new AppLogEntry(AppLogSource.Console, AppLogLevel.Info, $"tick {index}"));
        }

        for (var index = 0; index < 80; index++)
        {
            route.Navigated($"navigated to /{index}");
        }

        route.Push(Error);
        var heard = new List<AppEvent>();
        route.Route((appEvent, _) => heard.Add(appEvent));

        Assert.Equal(100, heard.Count(appEvent => appEvent is AppEvent.Log { Entry.Level: AppLogLevel.Info }));
        Assert.Equal(50, heard.Count(appEvent => appEvent is AppEvent.Navigation));
        Assert.Equal(new AppEvent.Log(Error), heard[^1]);
    }
}
