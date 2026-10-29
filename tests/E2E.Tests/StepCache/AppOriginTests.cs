// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepCache.StepCacheHarness;

namespace E2E.Tests.StepCache;

/// <summary>
/// .NET-only: the session maps each screen to its path on the app's origin (<c>StepCacheOptions.AppOrigin</c>), where upstream's
/// host does in <c>observe</c>. Upstream's <c>appLocation</c> itself is tested in <c>TraceRoute/AppLocationTests</c>.
/// </summary>
public sealed class AppOriginTests
{
    private const string Origin = "http://127.0.0.1:6000";

    [Fact]
    public async Task Records_a_screen_on_the_app_origin_as_its_path()
    {
        var session = MakeSession(NoEntry(), MakeHost([Origin + "/storage"], [Node("h", "heading", "Storage")], [Node("h", "heading", "Storage"), SavedMarker]), appOrigin: Origin);
        await session.BeginAsync();
        session.Record(Tap("button", "Save marker"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Equal("/storage", session.Pending.Entry!.Route);
    }

    [Fact]
    public async Task Starts_a_recording_made_on_another_port_and_misses_without_the_app_origin()
    {
        var withOrigin = MakeSession(EntryContext("/storage", actions: [Tap("button", "Save marker")]), MakeHost([Origin + "/storage"], [Node("b", "button", "Save marker")]), appOrigin: Origin);
        await withOrigin.BeginAsync();
        Assert.NotEqual("wrong-context", withOrigin.CacheInfo?.Reason);

        var without = MakeSession(EntryContext("/storage", actions: [Tap("button", "Save marker")]), MakeHost([Origin + "/storage"], [Node("b", "button", "Save marker")]));
        await without.BeginAsync();
        Assert.Equal("wrong-context", without.CacheInfo?.Reason);
    }
}
